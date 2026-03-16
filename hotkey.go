//go:build windows

package main

import (
	"fmt"
	"runtime"
	"syscall"
	"unsafe"
)

const (
	wmHotkey = 0x0312
	hotkeyID = 1

	// Custom messages for cross-thread hotkey management
	wmUser           = 0x0400
	wmRegisterHotkey = wmUser + 1
	wmUnregHotkey    = wmUser + 2
)

var (
	pRegisterHotKey   = user32.NewProc("RegisterHotKey")
	pUnregisterHotKey = user32.NewProc("UnregisterHotKey")
	pCreateWindowExW  = user32.NewProc("CreateWindowExW")
	pDefWindowProcW   = user32.NewProc("DefWindowProcW")
	pRegisterClassExW = user32.NewProc("RegisterClassExW")
	pGetMessageW      = user32.NewProc("GetMessageW")
	pTranslateMessage = user32.NewProc("TranslateMessage")
	pDispatchMessageW = user32.NewProc("DispatchMessageW")
	pPostMessageW     = user32.NewProc("PostMessageW")
	pDestroyWindow    = user32.NewProc("DestroyWindow")
	pGetModuleHandleW = kernel32.NewProc("GetModuleHandleW")
)

const (
	wmDestroy = 0x0002
	wmQuit    = 0x0012
	wmClose   = 0x0010
)

type wndClassExW struct {
	cbSize        uint32
	style         uint32
	lpfnWndProc   uintptr
	cbClsExtra    int32
	cbWndExtra    int32
	hInstance     syscall.Handle
	hIcon         syscall.Handle
	hCursor       syscall.Handle
	hbrBackground syscall.Handle
	lpszMenuName  *uint16
	lpszClassName *uint16
	hIconSm       syscall.Handle
}

type msg struct {
	hwnd    syscall.Handle
	message uint32
	wParam  uintptr
	lParam  uintptr
	time    uint32
	pt      point
}

type point struct {
	x, y int32
}

// GlobalHotkey manages a system-wide hotkey registration with a hidden message window.
// All Win32 calls are marshalled to the window's owning thread via custom messages.
type GlobalHotkey struct {
	hwnd       syscall.Handle
	registered bool
	pressed    chan struct{}
	quit       chan struct{}
	result     chan error // receives result from register/unregister on the window thread
}

// NewGlobalHotkey creates a new hotkey manager with a hidden message window
// running its own message pump on a dedicated OS thread.
func NewGlobalHotkey() (*GlobalHotkey, error) {
	gh := &GlobalHotkey{
		pressed: make(chan struct{}, 1),
		quit:    make(chan struct{}),
		result:  make(chan error, 1),
	}

	ready := make(chan error, 1)
	go gh.messageLoop(ready)

	if err := <-ready; err != nil {
		return nil, err
	}

	return gh, nil
}

// Pressed returns a channel that receives when the hotkey is pressed
func (gh *GlobalHotkey) Pressed() <-chan struct{} {
	return gh.pressed
}

// Register registers the global hotkey. The actual RegisterHotKey call
// is executed on the window's owning thread via a custom WM_USER message.
func (gh *GlobalHotkey) Register(modifiers, key uint32) error {
	gh.Unregister()

	// Pack modifiers and key into wParam (mod in low 16, key in high 16)
	wparam := uintptr(modifiers) | (uintptr(key) << 16)
	pPostMessageW.Call(uintptr(gh.hwnd), wmRegisterHotkey, wparam, 0)

	// Wait for result from the window thread
	return <-gh.result
}

// Unregister removes the currently registered hotkey.
func (gh *GlobalHotkey) Unregister() {
	if !gh.registered {
		return
	}
	pPostMessageW.Call(uintptr(gh.hwnd), wmUnregHotkey, 0, 0)
	<-gh.result
}

// Destroy unregisters the hotkey and destroys the message window
func (gh *GlobalHotkey) Destroy() {
	gh.Unregister()
	pPostMessageW.Call(uintptr(gh.hwnd), wmClose, 0, 0)
	<-gh.quit
}

func (gh *GlobalHotkey) messageLoop(ready chan<- error) {
	// Win32 message pumps are thread-affine — lock this goroutine to its OS thread
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()
	defer close(gh.quit)

	hInstance, _, _ := pGetModuleHandleW.Call(0)

	className, _ := syscall.UTF16PtrFromString("MarkdownPasteHtml_HotkeyWnd")

	wc := wndClassExW{
		lpfnWndProc:   syscall.NewCallback(gh.wndProc),
		hInstance:     syscall.Handle(hInstance),
		lpszClassName: className,
	}
	wc.cbSize = uint32(unsafe.Sizeof(wc))

	ret, _, err := pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc)))
	if ret == 0 {
		ready <- fmt.Errorf("RegisterClassExW failed: %v", err)
		return
	}

	// HWND_MESSAGE (-3) creates a message-only window
	hwndMessage := ^uintptr(2)
	hwnd, _, err := pCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(className)),
		0,
		0,
		0, 0, 0, 0,
		hwndMessage,
		0,
		hInstance,
		0,
	)
	if hwnd == 0 {
		ready <- fmt.Errorf("CreateWindowExW failed: %v", err)
		return
	}

	gh.hwnd = syscall.Handle(hwnd)
	ready <- nil

	// Message pump
	var m msg
	for {
		ret, _, _ := pGetMessageW.Call(
			uintptr(unsafe.Pointer(&m)),
			0, 0, 0,
		)
		if ret == 0 || int32(ret) == -1 {
			break
		}
		pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		pDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
	}
}

func (gh *GlobalHotkey) wndProc(hwnd, umsg, wparam, lparam uintptr) uintptr {
	switch umsg {
	case wmHotkey:
		select {
		case gh.pressed <- struct{}{}:
		default:
		}
		return 0

	case wmRegisterHotkey:
		// Unpack: modifiers in low 16 bits, key in high 16 bits
		modifiers := wparam & 0xFFFF
		key := (wparam >> 16) & 0xFFFF

		ret, _, err := pRegisterHotKey.Call(hwnd, hotkeyID, modifiers, key)
		if ret == 0 {
			gh.result <- fmt.Errorf("RegisterHotKey failed (mod=0x%x, key=0x%x): %v", modifiers, key, err)
		} else {
			gh.registered = true
			gh.result <- nil
		}
		return 0

	case wmUnregHotkey:
		pUnregisterHotKey.Call(hwnd, hotkeyID)
		gh.registered = false
		gh.result <- nil
		return 0

	case wmDestroy:
		pPostMessageW.Call(0, wmQuit, 0, 0)
		return 0
	case wmClose:
		pDestroyWindow.Call(hwnd)
		return 0
	}

	ret, _, _ := pDefWindowProcW.Call(hwnd, umsg, wparam, lparam)
	return ret
}
