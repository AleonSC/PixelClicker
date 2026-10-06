//go:build windows

package main

import (
	"encoding/binary"
	"fmt"
	"syscall"
	"unsafe"
)

const (
	sampleRate = 44100
	bufferMs   = 100
	numBuffers = 6
	waveMapper = 0xFFFFFFFF
	whdrDone   = 0x1
)

type waveFormatEx struct {
	FormatTag      uint16
	Channels       uint16
	SamplesPerSec  uint32
	AvgBytesPerSec uint32
	BlockAlign     uint16
	BitsPerSample  uint16
	CbSize         uint16
}

type waveHdr struct {
	Data          uintptr
	BufferLength  uint32
	BytesRecorded uint32
	User          uintptr
	Flags         uint32
	Loops         uint32
	Next          uintptr
	Reserved      uintptr
}

type waveInCaps struct {
	Mid           uint16
	Pid           uint16
	DriverVersion uint32
	Name          [32]uint16
	Formats       uint32
	Channels      uint16
	Reserved      uint16
}

var (
	winmm               = syscall.NewLazyDLL("winmm.dll")
	procWaveInOpen      = winmm.NewProc("waveInOpen")
	procWaveInClose     = winmm.NewProc("waveInClose")
	procWaveInPrepare   = winmm.NewProc("waveInPrepareHeader")
	procWaveInUnprepare = winmm.NewProc("waveInUnprepareHeader")
	procWaveInAddBuffer = winmm.NewProc("waveInAddBuffer")
	procWaveInStart     = winmm.NewProc("waveInStart")
	procWaveInStop      = winmm.NewProc("waveInStop")
	procWaveInReset     = winmm.NewProc("waveInReset")
	procWaveInNumDevs   = winmm.NewProc("waveInGetNumDevs")
	procWaveInDevCaps   = winmm.NewProc("waveInGetDevCapsW")
)

// inputDevices returns the names of all capture devices (index = device id).
func inputDevices() []string {
	n, _, _ := procWaveInNumDevs.Call()
	names := make([]string, 0, n)
	for i := uintptr(0); i < n; i++ {
		var caps waveInCaps
		r, _, _ := procWaveInDevCaps.Call(i, uintptr(unsafe.Pointer(&caps)), unsafe.Sizeof(caps))
		if r != 0 {
			names = append(names, fmt.Sprintf("Device %d", i))
			continue
		}
		names = append(names, syscall.UTF16ToString(caps.Name[:]))
	}
	return names
}

// recorder captures PCM from one device; call Poll regularly (~20 ms).
type recorder struct {
	h           uintptr
	bufs        [][]byte
	hdrs        []waveHdr
	out         sink
	closed      bool
	Bytes       uint64
	BytesPerSec uint32
}

func mmErr(what string, r uintptr) error {
	return fmt.Errorf("%s failed (MMSYSERR %d)", what, r)
}

func startRecorder(device uintptr, channels int, out sink) (*recorder, error) {
	format := waveFormatEx{
		FormatTag:      1,
		Channels:       uint16(channels),
		SamplesPerSec:  sampleRate,
		BitsPerSample:  16,
		BlockAlign:     uint16(channels * 2),
		AvgBytesPerSec: uint32(sampleRate * channels * 2),
	}
	r := &recorder{out: out, BytesPerSec: format.AvgBytesPerSec}
	if rc, _, _ := procWaveInOpen.Call(uintptr(unsafe.Pointer(&r.h)), device,
		uintptr(unsafe.Pointer(&format)), 0, 0, 0); rc != 0 {
		return nil, mmErr("waveInOpen", rc)
	}
	size := int(format.AvgBytesPerSec) * bufferMs / 1000
	r.bufs = make([][]byte, numBuffers)
	r.hdrs = make([]waveHdr, numBuffers)
	for i := range r.hdrs {
		r.bufs[i] = make([]byte, size)
		r.hdrs[i].Data = uintptr(unsafe.Pointer(&r.bufs[i][0]))
		r.hdrs[i].BufferLength = uint32(size)
		hp := uintptr(unsafe.Pointer(&r.hdrs[i]))
		if rc, _, _ := procWaveInPrepare.Call(r.h, hp, unsafe.Sizeof(r.hdrs[0])); rc != 0 {
			r.release()
			return nil, mmErr("waveInPrepareHeader", rc)
		}
		if rc, _, _ := procWaveInAddBuffer.Call(r.h, hp, unsafe.Sizeof(r.hdrs[0])); rc != 0 {
			r.release()
			return nil, mmErr("waveInAddBuffer", rc)
		}
	}
	if rc, _, _ := procWaveInStart.Call(r.h); rc != 0 {
		r.release()
		return nil, mmErr("waveInStart", rc)
	}
	return r, nil
}

// drain writes every finished buffer to the sink and returns the peak amplitude seen (0-32768).
func (r *recorder) drain(requeue bool) (int, error) {
	peak := 0
	var err error
	for i := range r.hdrs {
		hp := uintptr(unsafe.Pointer(&r.hdrs[i]))
		if r.hdrs[i].Flags&whdrDone == 0 {
			continue
		}
		b := r.bufs[i][:r.hdrs[i].BytesRecorded]
		for j := 0; j+1 < len(b); j += 2 {
			v := int(int16(binary.LittleEndian.Uint16(b[j:])))
			if v < 0 {
				v = -v
			}
			if v > peak {
				peak = v
			}
		}
		if len(b) > 0 && err == nil {
			err = r.out.Write(b)
			r.Bytes += uint64(len(b))
		}
		if requeue {
			procWaveInAddBuffer.Call(r.h, hp, unsafe.Sizeof(r.hdrs[0]))
		} else {
			r.hdrs[i].Flags &^= whdrDone
		}
	}
	return peak, err
}

func (r *recorder) Poll() (int, error) { return r.drain(true) }

// Stop ends capture, flushes the remaining audio and closes the sink.
func (r *recorder) Stop() error {
	procWaveInStop.Call(r.h)
	procWaveInReset.Call(r.h) // hands back all pending buffers
	_, err := r.drain(false)
	r.release()
	if cerr := r.out.Close(); err == nil {
		err = cerr
	}
	return err
}

func (r *recorder) release() {
	for i := range r.hdrs {
		procWaveInUnprepare.Call(r.h, uintptr(unsafe.Pointer(&r.hdrs[i])), unsafe.Sizeof(r.hdrs[0]))
	}
	procWaveInClose.Call(r.h)
}
