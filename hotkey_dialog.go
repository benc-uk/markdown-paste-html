//go:build windows

package main

import (
	"fmt"
	"runtime"
	"syscall"
	"time"
	"unsafe"
)

const (
	wmKeydown    = 0x0100
	wmCommand    = 0x0111
	wmCreate     = 0x0001
	wmSetfont    = 0x0030
	wmGetdlgcode = 0x0087

	wsVisible     = 0x10000000
	wsChild       = 0x40000000
	wsOverlapped  = 0x00000000
	wsCaption     = 0x00C00000
	wsSysmenu     = 0x00080000
	wsMinimizeBox = 0x00020000

	wsExDlgmodalframe = 0x00000001

	ssLeft = 0x00000000

	bsPushbutton = 0x00000000

	swShow = 5

	dlgcWantallkeys = 0x0004

	idOK     = 1
	idCancel = 2

	colorBtnface = 15

	defaultGuiFont = 17
)

var (
	pSetWindowLongPtrW = user32.NewProc("SetWindowLongPtrW")
	pGetWindowLongPtrW = user32.NewProc("GetWindowLongPtrW")
	pShowWindow        = user32.NewProc("ShowWindow")
	pUpdateWindow      = user32.NewProc("UpdateWindow")
	pSetFocus          = user32.NewProc("SetFocus")
	pSetForegroundWnd  = user32.NewProc("SetForegroundWindow")
	pSendMessageW      = user32.NewProc("SendMessageW")
	pEnableWindow      = user32.NewProc("EnableWindow")
	pSetWindowTextW    = user32.NewProc("SetWindowTextW")
	pPeekMessageW      = user32.NewProc("PeekMessageW")
	pGetDC             = user32.NewProc("GetDC")
	pReleaseDC         = user32.NewProc("ReleaseDC")

	gdi32           = syscall.NewLazyDLL("gdi32.dll")
	pGetStockObject = gdi32.NewProc("GetStockObject")
	pGetDeviceCaps  = gdi32.NewProc("GetDeviceCaps")
)

const logPixelsY = 90

// hotkeyDialogState holds dialog state passed via window long ptr
type hotkeyDialogState struct {
	modifiers   uint32
	key         uint32
	confirmed   bool
	hotkeyLabel uintptr
	parentHWND  uintptr
	done        bool // set true to break the modal message loop
}

var dialogState *hotkeyDialogState

// ShowHotkeyDialog shows a modal dialog for capturing a hotkey combination.
// Returns (modifiers, key, ok). The dialog runs on its own OS-locked thread
// with a dedicated message pump so it doesn't interfere with systray.
func ShowHotkeyDialog(currentModifiers, currentKey uint32) (uint32, uint32, bool) {
	type dialogResult struct {
		modifiers uint32
		key       uint32
		ok        bool
	}

	resultCh := make(chan dialogResult, 1)

	go func() {
		defer func() {
			if r := recover(); r != nil {
				Log(fmt.Sprintf("PANIC in hotkey dialog: %v", r))
				dialogState = nil
				resultCh <- dialogResult{currentModifiers, currentKey, false}
			}
		}()

		// Lock this goroutine to an OS thread — Win32 windows and their
		// message pumps must stay on the thread that created them
		runtime.LockOSThread()
		defer runtime.UnlockOSThread()

		state := &hotkeyDialogState{
			modifiers: currentModifiers,
			key:       currentKey,
		}
		dialogState = state

		Log("Creating dialog window...")
		hwnd := createDialogWindow(state, currentModifiers, currentKey)
		if hwnd == 0 {
			Log("Failed to create dialog window")
			dialogState = nil
			resultCh <- dialogResult{currentModifiers, currentKey, false}
			return
		}
		Log(fmt.Sprintf("Dialog window created: hwnd=%v", hwnd))

		// Message pump — this thread owns the dialog and all child windows
		// PeekMessage with hwnd=0 is safe here because this is a dedicated thread
		const pmRemove = 0x0001
		var m msg
		for !state.done {
			ret, _, _ := pPeekMessageW.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0, pmRemove)
			if ret == 0 {
				time.Sleep(10 * time.Millisecond)
				continue
			}
			pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
			pDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
		}

		Log(fmt.Sprintf("Dialog closed: confirmed=%v mods=0x%x key=0x%x", state.confirmed, state.modifiers, state.key))
		dialogState = nil
		resultCh <- dialogResult{state.modifiers, state.key, state.confirmed}
	}()

	r := <-resultCh
	return r.modifiers, r.key, r.ok
}

func createDialogWindow(state *hotkeyDialogState, currentModifiers, currentKey uint32) uintptr {
	hInstance, _, _ := pGetModuleHandleW.Call(0)

	className, _ := syscall.UTF16PtrFromString("MarkdownPasteHtml_HotkeyDlg")
	wndTitle, _ := syscall.UTF16PtrFromString("Change Shortcut")

	wc := wndClassExW{
		lpfnWndProc:   syscall.NewCallback(hotkeyDlgProc),
		hInstance:     syscall.Handle(hInstance),
		lpszClassName: className,
		hbrBackground: syscall.Handle(colorBtnface + 1),
	}
	wc.cbSize = uint32(unsafe.Sizeof(wc))

	pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc)))

	// Center on screen (approximate)
	width := int32(350)
	height := int32(165)

	hwnd, _, _ := pCreateWindowExW.Call(
		wsExDlgmodalframe,
		uintptr(unsafe.Pointer(className)),
		uintptr(unsafe.Pointer(wndTitle)),
		wsOverlapped|wsCaption|wsSysmenu,
		0x80000000, 0x80000000, // CW_USEDEFAULT
		uintptr(width), uintptr(height),
		0, 0,
		hInstance, 0,
	)
	if hwnd == 0 {
		return 0
	}

	state.parentHWND = hwnd

	// Get default GUI font
	font, _, _ := pGetStockObject.Call(defaultGuiFont)

	// Create child controls
	staticClass, _ := syscall.UTF16PtrFromString("STATIC")
	buttonClass, _ := syscall.UTF16PtrFromString("BUTTON")

	// Instruction label
	instrText, _ := syscall.UTF16PtrFromString("Press a new shortcut combo (e.g. Ctrl+Shift+K):")
	instrHwnd, _, _ := pCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(staticClass)),
		uintptr(unsafe.Pointer(instrText)),
		wsChild|wsVisible|ssLeft,
		20, 15, 300, 20,
		hwnd, 0, hInstance, 0,
	)
	pSendMessageW.Call(instrHwnd, wmSetfont, font, 1)

	// Hotkey display label (bold, larger)
	hotkeyText, _ := syscall.UTF16PtrFromString(formatHotkey(currentModifiers, currentKey))
	hotkeyHwnd, _, _ := pCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(staticClass)),
		uintptr(unsafe.Pointer(hotkeyText)),
		wsChild|wsVisible|ssLeft,
		20, 50, 300, 30,
		hwnd, 0, hInstance, 0,
	)
	pSendMessageW.Call(hotkeyHwnd, wmSetfont, font, 1)
	state.hotkeyLabel = hotkeyHwnd

	// OK button
	okText, _ := syscall.UTF16PtrFromString("OK")
	okHwnd, _, _ := pCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(buttonClass)),
		uintptr(unsafe.Pointer(okText)),
		wsChild|wsVisible|bsPushbutton,
		150, 100, 80, 30,
		hwnd, idOK, hInstance, 0,
	)
	pSendMessageW.Call(okHwnd, wmSetfont, font, 1)

	// Cancel button
	cancelText, _ := syscall.UTF16PtrFromString("Cancel")
	cancelHwnd, _, _ := pCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(buttonClass)),
		uintptr(unsafe.Pointer(cancelText)),
		wsChild|wsVisible|bsPushbutton,
		240, 100, 80, 30,
		hwnd, idCancel, hInstance, 0,
	)
	pSendMessageW.Call(cancelHwnd, wmSetfont, font, 1)

	pShowWindow.Call(hwnd, swShow)
	pUpdateWindow.Call(hwnd)
	pSetForegroundWnd.Call(hwnd)
	pSetFocus.Call(hwnd)

	return hwnd
}

func hotkeyDlgProc(hwnd, umsg, wparam, lparam uintptr) uintptr {
	switch umsg {
	case wmGetdlgcode:
		return dlgcWantallkeys

	case wmKeydown:
		if dialogState == nil {
			break
		}

		vk := uint32(wparam)

		// Ignore lone modifier presses
		switch vk {
		case 0xA0, 0xA1, // VK_LSHIFT, VK_RSHIFT
			0xA2, 0xA3, // VK_LCONTROL, VK_RCONTROL
			0xA4, 0xA5, // VK_LMENU, VK_RMENU
			0x10, 0x11, 0x12, // VK_SHIFT, VK_CONTROL, VK_MENU
			0x5B, 0x5C: // VK_LWIN, VK_RWIN
			return 0
		}

		// Build modifiers from current key state
		var mods uint32
		if isKeyDown(vkControl) {
			mods |= ModControl
		}
		if isKeyDown(vkMenu) {
			mods |= ModAlt
		}
		if isKeyDown(vkShift) {
			mods |= ModShift
		}

		// Require at least one modifier
		if mods == 0 {
			return 0
		}

		dialogState.modifiers = mods
		dialogState.key = vk

		// Update label
		text, _ := syscall.UTF16PtrFromString(formatHotkey(mods, vk))
		pSetWindowTextW.Call(dialogState.hotkeyLabel, uintptr(unsafe.Pointer(text)))

		return 0

	case wmCommand:
		id := wparam & 0xFFFF
		switch id {
		case idOK:
			if dialogState != nil {
				dialogState.confirmed = true
				dialogState.done = true
			}
			pDestroyWindow.Call(hwnd)
			return 0
		case idCancel:
			if dialogState != nil {
				dialogState.done = true
			}
			pDestroyWindow.Call(hwnd)
			return 0
		}

	case wmDestroy:
		// Do NOT call PostQuitMessage here — that would kill systray's message loop
		if dialogState != nil {
			dialogState.done = true
		}
		return 0
	}

	ret, _, _ := pDefWindowProcW.Call(hwnd, umsg, wparam, lparam)
	return ret
}

func formatHotkey(modifiers, key uint32) string {
	parts := ""
	if modifiers&ModControl != 0 {
		parts += "Ctrl + "
	}
	if modifiers&ModAlt != 0 {
		parts += "Alt + "
	}
	if modifiers&ModShift != 0 {
		parts += "Shift + "
	}

	parts += vkKeyName(key)
	return parts
}
