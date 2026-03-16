//go:build windows

package main

import (
	"fmt"
	"time"
	"unsafe"
)

var (
	pSendInput        = user32.NewProc("SendInput")
	pGetAsyncKeyState = user32.NewProc("GetAsyncKeyState")
	pGetForegroundWnd = user32.NewProc("GetForegroundWindow")
)

const (
	inputKeyboard    = 1
	keyeventfKeydown = 0x0000
	keyeventfKeyup   = 0x0002

	vkShift   = 0x10
	vkControl = 0x11
	vkMenu    = 0x12 // Alt
	vkV       = 0x56
)

// INPUT struct for SendInput (keyboard variant)
type keyboardInput struct {
	inputType uint32
	ki        keybdInput
	padding   [8]byte // Pad to match INPUT union size on x64
}

type keybdInput struct {
	wVk         uint16
	wScan       uint16
	dwFlags     uint32
	time        uint32
	dwExtraInfo uintptr
}

// Paste simulates Ctrl+V after waiting for all modifier keys to be released
func Paste() error {
	// Check foreground window exists
	hwnd, _, _ := pGetForegroundWnd.Call()
	if hwnd == 0 {
		return fmt.Errorf("no foreground window")
	}

	// Wait for user to release modifier keys from the hotkey
	// If Ctrl+V is sent while user holds Ctrl+Shift, it becomes Ctrl+Shift+V (paste plain text)
	maxWait := 2000
	waited := 0
	for waited < maxWait {
		ctrlDown := isKeyDown(vkControl)
		shiftDown := isKeyDown(vkShift)
		altDown := isKeyDown(vkMenu)

		if !ctrlDown && !shiftDown && !altDown {
			break
		}

		time.Sleep(20 * time.Millisecond)
		waited += 20
	}

	// Small extra delay for key state to fully settle
	time.Sleep(50 * time.Millisecond)

	// Simulate Ctrl+V keypress
	inputs := [4]keyboardInput{
		makeKeyInput(vkControl, keyeventfKeydown),
		makeKeyInput(vkV, keyeventfKeydown),
		makeKeyInput(vkV, keyeventfKeyup),
		makeKeyInput(vkControl, keyeventfKeyup),
	}

	ret, _, _ := pSendInput.Call(
		4,
		uintptr(unsafe.Pointer(&inputs[0])),
		unsafe.Sizeof(inputs[0]),
	)
	if ret != 4 {
		return fmt.Errorf("SendInput returned %d, expected 4", ret)
	}

	return nil
}

func isKeyDown(vk int) bool {
	ret, _, _ := pGetAsyncKeyState.Call(uintptr(vk))
	return ret&0x8000 != 0
}

func makeKeyInput(vk uint16, flags uint32) keyboardInput {
	return keyboardInput{
		inputType: inputKeyboard,
		ki: keybdInput{
			wVk:     vk,
			dwFlags: flags,
		},
	}
}
