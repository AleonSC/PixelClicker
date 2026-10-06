//go:build windows

package main

import (
	"encoding/binary"
	"os"

	"github.com/braheezy/shine-mp3/pkg/mp3"
)

// sink receives raw 16-bit little-endian PCM and stores it in some format.
type sink interface {
	Write(pcm []byte) error
	Close() error
}

// ---- WAV ----

type wavSink struct {
	f        *os.File
	channels int
	rate     int
	n        uint32
}

func newWavSink(path string, rate, channels int) (*wavSink, error) {
	f, err := os.Create(path)
	if err != nil {
		return nil, err
	}
	s := &wavSink{f: f, channels: channels, rate: rate}
	if err := s.header(); err != nil {
		f.Close()
		return nil, err
	}
	return s, nil
}

func (s *wavSink) Write(pcm []byte) error {
	n, err := s.f.Write(pcm)
	s.n += uint32(n)
	return err
}

// header writes a canonical 44-byte WAV header at offset 0 and seeks back to the end.
func (s *wavSink) header() error {
	if _, err := s.f.Seek(0, 0); err != nil {
		return err
	}
	le := binary.LittleEndian
	blockAlign := uint16(s.channels * 2)
	h := make([]byte, 0, 44)
	h = append(h, "RIFF"...)
	h = le.AppendUint32(h, 36+s.n)
	h = append(h, "WAVEfmt "...)
	h = le.AppendUint32(h, 16)
	h = le.AppendUint16(h, 1) // PCM
	h = le.AppendUint16(h, uint16(s.channels))
	h = le.AppendUint32(h, uint32(s.rate))
	h = le.AppendUint32(h, uint32(s.rate)*uint32(blockAlign))
	h = le.AppendUint16(h, blockAlign)
	h = le.AppendUint16(h, 16)
	h = append(h, "data"...)
	h = le.AppendUint32(h, s.n)
	if _, err := s.f.Write(h); err != nil {
		return err
	}
	_, err := s.f.Seek(0, 2)
	return err
}

func (s *wavSink) Close() error {
	err := s.header()
	if cerr := s.f.Close(); err == nil {
		err = cerr
	}
	return err
}

// ---- MP3 (pure-Go shine encoder, 128 kbit/s) ----

type mp3Sink struct {
	dup   bool // mono input is encoded as dual-mono stereo (the library's mono mode is broken)
	f     *os.File
	enc   *mp3.Encoder
	frame int // int16 values per MP3 frame (1152 per channel)
	pend  []int16
}

func newMp3Sink(path string, rate, channels int) (*mp3Sink, error) {
	f, err := os.Create(path)
	if err != nil {
		return nil, err
	}
	return &mp3Sink{f: f, dup: channels == 1, enc: mp3.NewEncoder(rate, 2), frame: 1152 * 2}, nil
}

func (s *mp3Sink) encodeFrame(samples []int16) error {
	data, n := s.enc.EncodeBufferInterleaved(samples)
	_, err := s.f.Write(data[:n])
	return err
}

func (s *mp3Sink) Write(pcm []byte) error {
	for i := 0; i+1 < len(pcm); i += 2 {
		v := int16(binary.LittleEndian.Uint16(pcm[i:]))
		s.pend = append(s.pend, v)
		if s.dup {
			s.pend = append(s.pend, v)
		}
	}
	for len(s.pend) >= s.frame {
		if err := s.encodeFrame(s.pend[:s.frame]); err != nil {
			return err
		}
		s.pend = s.pend[:copy(s.pend, s.pend[s.frame:])]
	}
	return nil
}

func (s *mp3Sink) Close() error {
	var err error
	if len(s.pend) > 0 {
		last := make([]int16, s.frame) // zero-padded final frame
		copy(last, s.pend)
		err = s.encodeFrame(last)
	}
	if cerr := s.f.Close(); err == nil {
		err = cerr
	}
	return err
}
