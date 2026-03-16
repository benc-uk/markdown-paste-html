//go:build windows

package main

import (
	"syscall"
	"unsafe"
)

var (
	shell32           = syscall.NewLazyDLL("shell32.dll")
	pShellNotifyIconW = shell32.NewProc("Shell_NotifyIconW")
)

const (
	nimModify = 0x00000001
	nifInfo   = 0x00000010
	nifTip    = 0x00000004

	// NIIF_INFO = 1 (info icon for balloon)
	niifInfo    = 0x00000001
	niifWarning = 0x00000002
	niifError   = 0x00000003
)

// NOTIFYICONDATAW structure (simplified for balloon notifications)
// We need this because systray doesn't expose balloon notification API
type notifyIconDataW struct {
	cbSize           uint32
	hWnd             uintptr
	uID              uint32
	uFlags           uint32
	uCallbackMessage uint32
	hIcon            uintptr
	szTip            [128]uint16
	dwState          uint32
	dwStateMask      uint32
	szInfo           [256]uint16
	uVersion         uint32 // union with uTimeout
	szInfoTitle      [64]uint16
	dwInfoFlags      uint32
	guidItem         [16]byte // GUID
	hBalloonIcon     uintptr
}

// trayHWND will be set by the tray module after systray starts.
// systray uses Shell_NotifyIconW internally, so we need its HWND to modify the notification.
// We'll find it via FindWindowW.
var trayHWND uintptr

var pFindWindowW = user32.NewProc("FindWindowW")

func findSystrayHWND() uintptr {
	// systray creates a window with class "SystsayClass" (yes, that's the actual typo in the library)
	className, _ := syscall.UTF16PtrFromString("SystrayClass")
	hwnd, _, _ := pFindWindowW.Call(uintptr(unsafe.Pointer(className)), 0)
	if hwnd == 0 {
		// Try alternate class name
		className2, _ := syscall.UTF16PtrFromString("Systray")
		hwnd, _, _ = pFindWindowW.Call(uintptr(unsafe.Pointer(className2)), 0)
	}
	return hwnd
}

// ShowBalloon shows a balloon notification from the tray icon
func ShowBalloon(title, message string, flags uint32) {
	if trayHWND == 0 {
		trayHWND = findSystrayHWND()
	}
	if trayHWND == 0 {
		// Can't find tray window, silently fail
		return
	}

	var nid notifyIconDataW
	nid.cbSize = uint32(unsafe.Sizeof(nid))
	nid.hWnd = trayHWND
	nid.uID = 100 // must match the ID used by getlantern/systray
	nid.uFlags = nifInfo

	// Set balloon title
	titleUTF16, _ := syscall.UTF16FromString(title)
	copy(nid.szInfoTitle[:], titleUTF16)

	// Set balloon message
	msgUTF16, _ := syscall.UTF16FromString(message)
	copy(nid.szInfo[:], msgUTF16)

	nid.dwInfoFlags = flags

	pShellNotifyIconW.Call(nimModify, uintptr(unsafe.Pointer(&nid)))
}

// ShowBalloonInfo shows an info balloon notification
func ShowBalloonInfo(title, message string) {
	ShowBalloon(title, message, niifInfo)
}

// ShowBalloonWarning shows a warning balloon notification
func ShowBalloonWarning(title, message string) {
	ShowBalloon(title, message, niifWarning)
}

// ShowBalloonError shows an error balloon notification
func ShowBalloonError(title, message string) {
	ShowBalloon(title, message, niifError)
}
