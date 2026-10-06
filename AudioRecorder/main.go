//go:build windows

// Audio Recorder: a small native Windows window that records a microphone to
// WAV or MP3. Pure Go + Win32 (user32/winmm), no cgo or external DLLs.
package main

import (
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"syscall"
	"time"
	"unsafe"
)

var (
	user32   = syscall.NewLazyDLL("user32.dll")
	gdi32    = syscall.NewLazyDLL("gdi32.dll")
	kernel32 = syscall.NewLazyDLL("kernel32.dll")
	shell32  = syscall.NewLazyDLL("shell32.dll")
	comctl32 = syscall.NewLazyDLL("comctl32.dll")

	pRegisterClassEx    = user32.NewProc("RegisterClassExW")
	pCreateWindowEx     = user32.NewProc("CreateWindowExW")
	pDefWindowProc      = user32.NewProc("DefWindowProcW")
	pGetMessage         = user32.NewProc("GetMessageW")
	pTranslateMessage   = user32.NewProc("TranslateMessage")
	pDispatchMessage    = user32.NewProc("DispatchMessageW")
	pPostQuitMessage    = user32.NewProc("PostQuitMessage")
	pShowWindow         = user32.NewProc("ShowWindow")
	pSetTimer           = user32.NewProc("SetTimer")
	pKillTimer          = user32.NewProc("KillTimer")
	pSendMessage        = user32.NewProc("SendMessageW")
	pSetWindowText      = user32.NewProc("SetWindowTextW")
	pEnableWindow       = user32.NewProc("EnableWindow")
	pLoadCursor         = user32.NewProc("LoadCursorW")
	pAdjustWindowRect   = user32.NewProc("AdjustWindowRectEx")
	pMessageBox         = user32.NewProc("MessageBoxW")
	pSetProcessDPIAware = user32.NewProc("SetProcessDPIAware")
	pGetDpiForSystem    = user32.NewProc("GetDpiForSystem")
	pCreateFont         = gdi32.NewProc("CreateFontW")
	pGetModuleHandle    = kernel32.NewProc("GetModuleHandleW")
	pShellExecute       = shell32.NewProc("ShellExecuteW")
	pInitCommonControls = comctl32.NewProc("InitCommonControlsEx")
)

const (
	wmCreate  = 0x0001
	wmDestroy = 0x0002
	wmClose   = 0x0010
	wmSetFont = 0x0030
	wmCommand = 0x0111
	wmTimer   = 0x0113

	wsOverlapped = 0x00000000
	wsCaption    = 0x00C00000
	wsSysMenu    = 0x00080000
	wsMinimize   = 0x00020000
	wsVisible    = 0x10000000
	wsChild      = 0x40000000
	wsTabStop    = 0x00010000
	wsVScroll    = 0x00200000

	cbsDropDownList = 0x0003
	bsPushButton    = 0x0000
	bsDefPush       = 0x0001
	cbAddString     = 0x0143
	cbGetCurSel     = 0x0147
	cbSetCurSel     = 0x014E
	pbmSetPos       = 0x0402
	pbmSetRange32   = 0x0406

	idDevice = 101
	idFormat = 102
	idChans  = 103
	idRecord = 104
	idLevel  = 105
	idStatus = 106
	idOpen   = 107
	timerID  = 1
	timerMs  = 20
)

type wndClassEx struct {
	Size       uint32
	Style      uint32
	WndProc    uintptr
	ClsExtra   int32
	WndExtra   int32
	Instance   uintptr
	Icon       uintptr
	Cursor     uintptr
	Background uintptr
	MenuName   *uint16
	ClassName  *uint16
	IconSm     uintptr
}

type point struct{ X, Y int32 }

type msg struct {
	Hwnd    uintptr
	Message uint32
	WParam  uintptr
	LParam  uintptr
	Time    uint32
	Pt      point
	Private uint32
}

type rect struct{ Left, Top, Right, Bottom int32 }

// app holds all window state (everything runs on the single UI thread).
var app struct {
	hwnd, font            uintptr
	device, format, chans uintptr
	record, level, status uintptr
	dpi                   int
	deviceIDs             []uintptr
	rec                   capture
	path                  string
	started               time.Time
	smooth                int
}

func u16(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }

func px(v int) int { return v * app.dpi / 96 }

func recordingsDir() string {
	if exe, err := os.Executable(); err == nil {
		return filepath.Join(filepath.Dir(exe), "Recordings")
	}
	return "Recordings"
}

func child(class, text string, style uintptr, x, y, w, h int, id uintptr) uintptr {
	hw, _, _ := pCreateWindowEx.Call(0, uintptr(unsafe.Pointer(u16(class))), uintptr(unsafe.Pointer(u16(text))),
		wsChild|wsVisible|style, uintptr(px(x)), uintptr(px(y)), uintptr(px(w)), uintptr(px(h)),
		app.hwnd, id, 0, 0)
	pSendMessage.Call(hw, wmSetFont, app.font, 1)
	return hw
}

func addItems(combo uintptr, items ...string) {
	for _, it := range items {
		pSendMessage.Call(combo, cbAddString, 0, uintptr(unsafe.Pointer(u16(it))))
	}
	pSendMessage.Call(combo, cbSetCurSel, 0, 0)
}

func setText(h uintptr, s string) { pSetWindowText.Call(h, uintptr(unsafe.Pointer(u16(s)))) }

func buildUI() {
	const left, labelW, comboX, comboW = 16, 100, 120, 284
	child("STATIC", "Record from:", 0, left, 20, labelW, 22, 0)
	app.device = child("COMBOBOX", "", wsTabStop|wsVScroll|cbsDropDownList, comboX, 16, comboW, 200, idDevice)
	child("STATIC", "Format:", 0, left, 56, labelW, 22, 0)
	app.format = child("COMBOBOX", "", wsTabStop|wsVScroll|cbsDropDownList, comboX, 52, comboW, 200, idFormat)
	child("STATIC", "Channels:", 0, left, 92, labelW, 22, 0)
	app.chans = child("COMBOBOX", "", wsTabStop|wsVScroll|cbsDropDownList, comboX, 88, comboW, 200, idChans)

	addItems(app.device, "Default microphone")
	addItems2(app.device, "System sound (what you hear)")
	app.deviceIDs = []uintptr{waveMapper, loopbackID}
	for i, n := range inputDevices() {
		addItems2(app.device, n)
		app.deviceIDs = append(app.deviceIDs, uintptr(i))
	}
	addItems(app.format, "WAV (lossless)", "MP3 (128 kbit/s)")
	addItems(app.chans, "Mono", "Stereo")

	app.record = child("BUTTON", "Start recording", wsTabStop|bsDefPush, 16, 130, 388, 44, idRecord)
	app.level = child("msctls_progress32", "", 0, 16, 186, 388, 16, idLevel)
	pSendMessage.Call(app.level, pbmSetRange32, 0, 100)
	app.status = child("STATIC", "Ready.", 0, 16, 212, 388, 22, idStatus)
	child("BUTTON", "Open recordings folder", wsTabStop|bsPushButton, 16, 244, 388, 30, idOpen)
}

// addItems2 appends without resetting the selection.
func addItems2(combo uintptr, it string) {
	pSendMessage.Call(combo, cbAddString, 0, uintptr(unsafe.Pointer(u16(it))))
}

func msgBox(text string) {
	pMessageBox.Call(app.hwnd, uintptr(unsafe.Pointer(u16(text))), uintptr(unsafe.Pointer(u16("Audio Recorder"))), 0x10)
}

func setControlsEnabled(on bool) {
	v := uintptr(0)
	if on {
		v = 1
	}
	for _, c := range []uintptr{app.device, app.format, app.chans} {
		pEnableWindow.Call(c, v)
	}
}

func startRecording() {
	sel, _, _ := pSendMessage.Call(app.device, cbGetCurSel, 0, 0)
	format, _, _ := pSendMessage.Call(app.format, cbGetCurSel, 0, 0)
	chSel, _, _ := pSendMessage.Call(app.chans, cbGetCurSel, 0, 0)
	channels := int(chSel) + 1

	if err := os.MkdirAll(recordingsDir(), 0o755); err != nil {
		msgBox("Cannot create the Recordings folder:\n" + err.Error())
		return
	}
	ext := ".wav"
	if format == 1 {
		ext = ".mp3"
	}
	path := filepath.Join(recordingsDir(), "recording_"+time.Now().Format("20060102_150405")+ext)

	var out sink
	var err error
	if format == 1 {
		out, err = newMp3Sink(path, sampleRate, channels)
	} else {
		out, err = newWavSink(path, sampleRate, channels)
	}
	if err != nil {
		msgBox("Cannot create the file:\n" + err.Error())
		return
	}
	var rec capture
	if app.deviceIDs[sel] == loopbackID {
		rec, err = startLoopback(channels, out)
	} else {
		rec, err = startRecorder(app.deviceIDs[sel], channels, out)
	}
	if err != nil {
		out.Close()
		os.Remove(path)
		msgBox("Could not start recording:\n" + err.Error() + "\n\nTry another device or Mono.")
		return
	}
	app.rec, app.path, app.started, app.smooth = rec, path, time.Now(), 0
	setControlsEnabled(false)
	setText(app.record, "Stop recording")
	pSetTimer.Call(app.hwnd, timerID, timerMs, 0)
}

func stopRecording() {
	if app.rec == nil {
		return
	}
	pKillTimer.Call(app.hwnd, timerID)
	rec := app.rec
	app.rec = nil
	err := rec.Stop()
	setControlsEnabled(true)
	setText(app.record, "Start recording")
	pSendMessage.Call(app.level, pbmSetPos, 0, 0)
	if err != nil {
		setText(app.status, "Error while saving.")
		msgBox("Problem while saving the recording:\n" + err.Error())
		return
	}
	setText(app.status, "Saved: "+filepath.Base(app.path))
}

func poll() {
	peak, err := app.rec.Poll()
	if err != nil {
		stopRecording()
		msgBox("Recording stopped: " + err.Error())
		return
	}
	lvl := peak * 100 / 32768
	if lvl < app.smooth {
		lvl = app.smooth * 85 / 100 // slow fall-off
	}
	app.smooth = lvl
	pSendMessage.Call(app.level, pbmSetPos, uintptr(lvl), 0)
	bytes, perSec := app.rec.Stats()
	secs := int(bytes / uint64(perSec))
	setText(app.status, fmt.Sprintf("Recording  %02d:%02d   (%.1f MB)", secs/60, secs%60, float64(bytes)/1e6))
}

func wndProc(hwnd, m, wParam, lParam uintptr) uintptr {
	switch m {
	case wmCreate:
		app.hwnd = hwnd
		buildUI()
		return 0
	case wmCommand:
		id, code := wParam&0xFFFF, (wParam>>16)&0xFFFF
		if code == 0 { // BN_CLICKED
			switch id {
			case idRecord:
				if app.rec == nil {
					startRecording()
				} else {
					stopRecording()
				}
			case idOpen:
				os.MkdirAll(recordingsDir(), 0o755)
				pShellExecute.Call(0, uintptr(unsafe.Pointer(u16("open"))),
					uintptr(unsafe.Pointer(u16(recordingsDir()))), 0, 0, 1)
			}
		}
		return 0
	case wmTimer:
		if app.rec != nil {
			poll()
		}
		return 0
	case wmClose:
		stopRecording() // never lose a running recording
	case wmDestroy:
		pPostQuitMessage.Call(0)
		return 0
	}
	r, _, _ := pDefWindowProc.Call(hwnd, m, wParam, lParam)
	return r
}

func main() {
	runtime.LockOSThread()
	initCOM()

	app.dpi = 96
	pSetProcessDPIAware.Call()
	if pGetDpiForSystem.Find() == nil {
		if d, _, _ := pGetDpiForSystem.Call(); d != 0 {
			app.dpi = int(d)
		}
	}
	// ICC_PROGRESS_CLASS so the progress bar class is registered.
	icc := struct{ Size, Mask uint32 }{8, 0x20}
	pInitCommonControls.Call(uintptr(unsafe.Pointer(&icc)))

	app.font, _, _ = pCreateFont.Call(^uintptr(px(16))+1, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0,
		uintptr(unsafe.Pointer(u16("Segoe UI"))))

	inst, _, _ := pGetModuleHandle.Call(0)
	cursor, _, _ := pLoadCursor.Call(0, 32512)
	class := u16("AudioRecorderWindow")
	wc := wndClassEx{
		WndProc: syscall.NewCallback(wndProc), Instance: inst, Cursor: cursor,
		Background: 16, // COLOR_BTNFACE + 1
		ClassName:  class,
	}
	wc.Size = uint32(unsafe.Sizeof(wc))
	if r, _, _ := pRegisterClassEx.Call(uintptr(unsafe.Pointer(&wc))); r == 0 {
		os.Exit(1)
	}

	style := uintptr(wsOverlapped | wsCaption | wsSysMenu | wsMinimize)
	r := rect{0, 0, int32(px(420)), int32(px(290))}
	pAdjustWindowRect.Call(uintptr(unsafe.Pointer(&r)), style, 0, 0)
	const useDefault = 0x80000000
	pCreateWindowEx.Call(0, uintptr(unsafe.Pointer(class)), uintptr(unsafe.Pointer(u16("Audio Recorder"))),
		style, useDefault, useDefault, uintptr(r.Right-r.Left), uintptr(r.Bottom-r.Top), 0, 0, inst, 0)
	pShowWindow.Call(app.hwnd, 1)

	var m msg
	for {
		if r, _, _ := pGetMessage.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0); int32(r) <= 0 {
			break
		}
		pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		pDispatchMessage.Call(uintptr(unsafe.Pointer(&m)))
	}
}
