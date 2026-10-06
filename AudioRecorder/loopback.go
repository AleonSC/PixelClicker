//go:build windows

package main

import (
	"encoding/binary"
	"fmt"
	"math"
	"time"
	"unsafe"

	"github.com/go-ole/go-ole"
	"github.com/moutend/go-wca/pkg/wca"
)

// loopbackID marks the "System sound" entry in the device list.
const loopbackID = 0xFFFFFFFE

// capture is anything the UI can poll for audio (microphone or system sound).
type capture interface {
	Poll() (peak int, err error)
	Stop() error
	Stats() (bytes uint64, bytesPerSec uint32)
}

// loopback records whatever the default playback device is playing (WASAPI loopback).
type loopback struct {
	dev     *wca.IMMDevice
	ac      *wca.IAudioClient
	acc     *wca.IAudioCaptureClient
	out     sink
	ch      int
	perSec  uint32
	total   uint64
	started time.Time

	// Used only when the driver refuses automatic format conversion.
	convert bool
	srcFmt  wca.WAVEFORMATEX
	srcRate float64
	pos     float64 // fractional read position in the source stream
	last    [2]float64
}

func initCOM() { ole.CoInitializeEx(0, ole.COINIT_APARTMENTTHREADED) }

func openLoopbackClient() (*wca.IMMDevice, *wca.IAudioClient, error) {
	var mmde *wca.IMMDeviceEnumerator
	if err := wca.CoCreateInstance(wca.CLSID_MMDeviceEnumerator, 0, wca.CLSCTX_ALL,
		wca.IID_IMMDeviceEnumerator, &mmde); err != nil {
		return nil, nil, fmt.Errorf("device enumerator: %w", err)
	}
	defer mmde.Release()
	var dev *wca.IMMDevice
	if err := mmde.GetDefaultAudioEndpoint(wca.ERender, wca.EConsole, &dev); err != nil {
		return nil, nil, fmt.Errorf("no default playback device: %w", err)
	}
	var ac *wca.IAudioClient
	if err := dev.Activate(wca.IID_IAudioClient, wca.CLSCTX_ALL, nil, &ac); err != nil {
		dev.Release()
		return nil, nil, fmt.Errorf("activate audio client: %w", err)
	}
	return dev, ac, nil
}

func startLoopback(channels int, out sink) (capture, error) {
	l := &loopback{out: out, ch: channels, perSec: uint32(sampleRate * channels * 2)}

	// Attempt 1: ask Windows to convert to our format (44.1 kHz, 16-bit).
	want := wca.WAVEFORMATEX{
		WFormatTag: 1, NChannels: uint16(channels), NSamplesPerSec: sampleRate,
		NAvgBytesPerSec: l.perSec, NBlockAlign: uint16(channels * 2), WBitsPerSample: 16,
	}
	dev, ac, err := openLoopbackClient()
	if err != nil {
		return nil, err
	}
	flags := uint32(wca.AUDCLNT_STREAMFLAGS_LOOPBACK | wca.AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM |
		wca.AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY)
	if err := ac.Initialize(wca.AUDCLNT_SHAREMODE_SHARED, flags, 10000000, 0, &want, nil); err != nil {
		// Attempt 2: capture in the device's own mix format and convert ourselves.
		ac.Release()
		dev.Release()
		if dev, ac, err = openLoopbackClient(); err != nil {
			return nil, err
		}
		var mix *wca.WAVEFORMATEX
		if err := ac.GetMixFormat(&mix); err != nil {
			ac.Release()
			dev.Release()
			return nil, fmt.Errorf("mix format: %w", err)
		}
		l.srcFmt = *mix
		ole.CoTaskMemFree(uintptr(unsafe.Pointer(mix)))
		if b := l.srcFmt.WBitsPerSample; b != 16 && b != 24 && b != 32 {
			ac.Release()
			dev.Release()
			return nil, fmt.Errorf("unsupported system sound format (%d-bit)", b)
		}
		l.convert, l.srcRate = true, float64(l.srcFmt.NSamplesPerSec)
		if err := ac.Initialize(wca.AUDCLNT_SHAREMODE_SHARED, wca.AUDCLNT_STREAMFLAGS_LOOPBACK,
			10000000, 0, &l.srcFmt, nil); err != nil {
			ac.Release()
			dev.Release()
			return nil, fmt.Errorf("initialize loopback: %w", err)
		}
	}
	l.dev, l.ac = dev, ac
	if err := ac.GetService(wca.IID_IAudioCaptureClient, &l.acc); err != nil {
		l.release()
		return nil, fmt.Errorf("capture client: %w", err)
	}
	if err := ac.Start(); err != nil {
		l.release()
		return nil, fmt.Errorf("start: %w", err)
	}
	l.started = time.Now()
	return l, nil
}

func (l *loopback) Stats() (uint64, uint32) { return l.total, l.perSec }

func (l *loopback) release() {
	if l.acc != nil {
		l.acc.Release()
	}
	if l.ac != nil {
		l.ac.Release()
	}
	if l.dev != nil {
		l.dev.Release()
	}
}

func (l *loopback) emit(pcm []byte, peak *int) error {
	for i := 0; i+1 < len(pcm); i += 2 {
		v := int(int16(binary.LittleEndian.Uint16(pcm[i:])))
		if v < 0 {
			v = -v
		}
		if v > *peak {
			*peak = v
		}
	}
	l.total += uint64(len(pcm))
	return l.out.Write(pcm)
}

// readPackets moves every pending packet to the sink.
func (l *loopback) readPackets(peak *int) error {
	for {
		var n uint32
		if err := l.acc.GetNextPacketSize(&n); err != nil {
			return err
		}
		if n == 0 {
			return nil
		}
		var data *byte
		var frames, flags uint32
		if err := l.acc.GetBuffer(&data, &frames, &flags, nil, nil); err != nil {
			return err
		}
		silent := flags&wca.AUDCLNT_BUFFERFLAGS_SILENT != 0
		var pcm []byte
		if l.convert {
			pcm = l.convertPacket(data, frames, silent)
		} else {
			pcm = make([]byte, int(frames)*l.ch*2)
			if !silent && frames > 0 {
				copy(pcm, unsafe.Slice(data, len(pcm)))
			}
		}
		l.acc.ReleaseBuffer(frames)
		if err := l.emit(pcm, peak); err != nil {
			return err
		}
	}
}

func (l *loopback) Poll() (int, error) {
	peak := 0
	if err := l.readPackets(&peak); err != nil {
		return peak, err
	}

	// Loopback delivers nothing while nothing is playing; fill the gap with
	// silence so the file keeps real-time length.
	expected := uint64(time.Since(l.started).Seconds() * float64(l.perSec))
	if deficit := int64(expected) - int64(l.total); deficit > int64(l.perSec)/5 {
		pad := (uint64(deficit) - uint64(l.perSec)/20) / uint64(l.ch*2) * uint64(l.ch*2)
		if err := l.emit(make([]byte, pad), &peak); err != nil {
			return peak, err
		}
	}
	return peak, nil
}

// convertPacket turns a packet in the device mix format into 16-bit, 44.1 kHz, l.ch channels.
func (l *loopback) convertPacket(data *byte, frames uint32, silent bool) []byte {
	srcCh := int(l.srcFmt.NChannels)
	bytesPer := int(l.srcFmt.WBitsPerSample) / 8
	block := srcCh * bytesPer
	raw := unsafe.Slice(data, int(frames)*block)

	sample := func(f, c int) float64 {
		if silent {
			return 0
		}
		o := f*block + c*bytesPer
		switch bytesPer {
		case 2:
			return float64(int16(binary.LittleEndian.Uint16(raw[o:]))) / 32768
		case 3:
			v := int32(raw[o])<<8 | int32(raw[o+1])<<16 | int32(raw[o+2])<<24
			return float64(v) / 2147483648
		default: // 32-bit float (the usual WASAPI mix format)
			return float64(math.Float32frombits(binary.LittleEndian.Uint32(raw[o:])))
		}
	}
	// Frame f as l.ch channels (mix down / duplicate).
	frame := func(f int) (a, b float64) {
		if srcCh == 1 {
			v := sample(f, 0)
			return v, v
		}
		a, b = sample(f, 0), sample(f, 1)
		if l.ch == 1 {
			m := (a + b) / 2
			return m, m
		}
		return a, b
	}

	step := l.srcRate / sampleRate
	var out []byte
	put := func(v float64) {
		if v > 1 {
			v = 1
		} else if v < -1 {
			v = -1
		}
		out = binary.LittleEndian.AppendUint16(out, uint16(int16(v*32767)))
	}
	// Linear interpolation. Index -1 is the last frame of the previous packet,
	// so pos runs over [-1, frames-1) and carries over between packets.
	for l.pos < float64(frames-1) {
		i := int(math.Floor(l.pos))
		fr := l.pos - float64(i)
		var a0, b0 float64
		if i < 0 {
			a0, b0 = l.last[0], l.last[1]
		} else {
			a0, b0 = frame(i)
		}
		a1, b1 := frame(i + 1)
		put(a0 + (a1-a0)*fr)
		if l.ch == 2 {
			put(b0 + (b1-b0)*fr)
		}
		l.pos += step
	}
	l.pos -= float64(frames)
	if frames > 0 {
		l.last[0], l.last[1] = frame(int(frames) - 1)
	}
	return out
}

func (l *loopback) Stop() error {
	l.ac.Stop()
	var peak int
	l.readPackets(&peak) // flush what is still buffered (no silence padding)
	l.release()
	return l.out.Close()
}
