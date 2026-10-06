//go:build windows

// Simple audio recorder for Windows. Records the default microphone to a
// 16-bit PCM .wav file using the built-in winmm waveIn API (no dependencies).
//
// Usage: recorder.exe [output.wav]
// Press Enter to start recording, Enter again to stop and save.
package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	sampleRate   = 44100
	channels     = 1
	bitsPerSamp  = 16
	bufferMs     = 100
	numBuffers   = 6
	waveMapper   = 0xFFFFFFFF
	whdrDone     = 0x00000001
	waveFormatPC = 1
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

var (
	winmm                = syscall.NewLazyDLL("winmm.dll")
	procWaveInOpen       = winmm.NewProc("waveInOpen")
	procWaveInClose      = winmm.NewProc("waveInClose")
	procWaveInPrepare    = winmm.NewProc("waveInPrepareHeader")
	procWaveInUnprepare  = winmm.NewProc("waveInUnprepareHeader")
	procWaveInAddBuffer  = winmm.NewProc("waveInAddBuffer")
	procWaveInStart      = winmm.NewProc("waveInStart")
	procWaveInStop       = winmm.NewProc("waveInStop")
	procWaveInReset      = winmm.NewProc("waveInReset")
	procWaveInGetNumDevs = winmm.NewProc("waveInGetNumDevs")
)

func mmCheck(what string, r uintptr) error {
	if r != 0 {
		return fmt.Errorf("%s failed (MMSYSERR %d)", what, r)
	}
	return nil
}

func main() {
	out := ""
	if len(os.Args) > 1 {
		out = os.Args[1]
	} else {
		dir := "Recordings"
		if exe, err := os.Executable(); err == nil {
			dir = filepath.Join(filepath.Dir(exe), "Recordings")
		}
		out = filepath.Join(dir, "recording_"+time.Now().Format("20060102_150405")+".wav")
	}

	err := run(out)
	if err != nil {
		fmt.Println("Error:", err)
	}
	fmt.Print("\nPress Enter to exit...")
	bufio.NewReader(os.Stdin).ReadString('\n')
}

func run(out string) error {
	fmt.Println("=== Audio Recorder ===")
	if n, _, _ := procWaveInGetNumDevs.Call(); n == 0 {
		return fmt.Errorf("no audio input device found")
	}
	fmt.Println("Output file:", out)
	fmt.Print("\nPress Enter to START recording...")
	in := bufio.NewReader(os.Stdin)
	in.ReadString('\n')

	if err := os.MkdirAll(filepath.Dir(out), 0o755); err != nil {
		return err
	}
	f, err := os.Create(out)
	if err != nil {
		return err
	}
	defer f.Close()
	if err := writeHeader(f, 0); err != nil {
		return err
	}

	format := waveFormatEx{
		FormatTag:      waveFormatPC,
		Channels:       channels,
		SamplesPerSec:  sampleRate,
		BitsPerSample:  bitsPerSamp,
		BlockAlign:     channels * bitsPerSamp / 8,
		AvgBytesPerSec: sampleRate * channels * bitsPerSamp / 8,
	}
	var h uintptr
	r, _, _ := procWaveInOpen.Call(uintptr(unsafe.Pointer(&h)), waveMapper,
		uintptr(unsafe.Pointer(&format)), 0, 0, 0)
	if err := mmCheck("waveInOpen", r); err != nil {
		return err
	}

	bufSize := sampleRate * channels * bitsPerSamp / 8 * bufferMs / 1000
	bufs := make([][]byte, numBuffers)
	hdrs := make([]waveHdr, numBuffers)
	hdrSize := unsafe.Sizeof(hdrs[0])
	for i := range hdrs {
		bufs[i] = make([]byte, bufSize)
		hdrs[i].Data = uintptr(unsafe.Pointer(&bufs[i][0]))
		hdrs[i].BufferLength = uint32(bufSize)
		r, _, _ = procWaveInPrepare.Call(h, uintptr(unsafe.Pointer(&hdrs[i])), hdrSize)
		if err := mmCheck("waveInPrepareHeader", r); err != nil {
			return err
		}
		r, _, _ = procWaveInAddBuffer.Call(h, uintptr(unsafe.Pointer(&hdrs[i])), hdrSize)
		if err := mmCheck("waveInAddBuffer", r); err != nil {
			return err
		}
	}
	if r, _, _ = procWaveInStart.Call(h); r != 0 {
		return mmCheck("waveInStart", r)
	}

	stop := make(chan struct{})
	go func() {
		in.ReadString('\n')
		close(stop)
	}()
	fmt.Println("Recording... press Enter to STOP.")

	var total uint32
	start := time.Now()
	drain := func(requeue bool) {
		for i := range hdrs {
			if hdrs[i].Flags&whdrDone == 0 {
				continue
			}
			n := hdrs[i].BytesRecorded
			if n > 0 {
				f.Write(bufs[i][:n])
				total += n
				showLevel(bufs[i][:n], time.Since(start))
			}
			if requeue {
				procWaveInAddBuffer.Call(h, uintptr(unsafe.Pointer(&hdrs[i])), hdrSize)
			} else {
				hdrs[i].Flags &^= whdrDone
			}
		}
	}

loop:
	for {
		select {
		case <-stop:
			break loop
		case <-time.After(20 * time.Millisecond):
			drain(true)
		}
	}

	procWaveInStop.Call(h)
	procWaveInReset.Call(h) // returns all pending buffers as done
	drain(false)
	for i := range hdrs {
		procWaveInUnprepare.Call(h, uintptr(unsafe.Pointer(&hdrs[i])), hdrSize)
	}
	procWaveInClose.Call(h)

	if err := writeHeader(f, total); err != nil {
		return err
	}
	fmt.Printf("\nSaved %.1f s to %s\n", float64(total)/float64(format.AvgBytesPerSec), out)
	return nil
}

func showLevel(b []byte, elapsed time.Duration) {
	peak := 0
	for i := 0; i+1 < len(b); i += 2 {
		v := int(int16(binary.LittleEndian.Uint16(b[i:])))
		if v < 0 {
			v = -v
		}
		if v > peak {
			peak = v
		}
	}
	bars := peak * 30 / 32768
	fmt.Printf("\r  %02d:%02d [%-30s]", int(elapsed.Minutes()), int(elapsed.Seconds())%60,
		strings.Repeat("#", bars))
}

// writeHeader writes (or rewrites at offset 0) a canonical 44-byte WAV header.
func writeHeader(f *os.File, dataLen uint32) error {
	if _, err := f.Seek(0, 0); err != nil {
		return err
	}
	byteRate := uint32(sampleRate * channels * bitsPerSamp / 8)
	hdr := make([]byte, 0, 44)
	le := binary.LittleEndian
	hdr = append(hdr, "RIFF"...)
	hdr = le.AppendUint32(hdr, 36+dataLen)
	hdr = append(hdr, "WAVEfmt "...)
	hdr = le.AppendUint32(hdr, 16)
	hdr = le.AppendUint16(hdr, waveFormatPC)
	hdr = le.AppendUint16(hdr, channels)
	hdr = le.AppendUint32(hdr, sampleRate)
	hdr = le.AppendUint32(hdr, byteRate)
	hdr = le.AppendUint16(hdr, channels*bitsPerSamp/8)
	hdr = le.AppendUint16(hdr, bitsPerSamp)
	hdr = append(hdr, "data"...)
	hdr = le.AppendUint32(hdr, dataLen)
	_, err := f.Write(hdr)
	if err == nil {
		_, err = f.Seek(0, 2)
	}
	return err
}
