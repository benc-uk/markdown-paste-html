//go:build windows

package main

import (
	"os"
	"syscall"
	"unsafe"

	"github.com/getlantern/systray"
)

var (
	kernel32      = syscall.NewLazyDLL("kernel32.dll")
	pCreateMutexW = kernel32.NewProc("CreateMutexW")
	pReleaseMutex = kernel32.NewProc("ReleaseMutex")
	pCloseHandle  = kernel32.NewProc("CloseHandle")

	user32      = syscall.NewLazyDLL("user32.dll")
	pMessageBox = user32.NewProc("MessageBoxW")
)

const errorAlreadyExists = 183

func main() {
	// Single instance enforcement via named mutex
	mutexName, _ := syscall.UTF16PtrFromString("MarkdownPasteHtml_SingleInstance_Mutex")

	// The third return value from .Call() captures GetLastError() immediately after the syscall
	handle, _, callErr := pCreateMutexW.Call(0, 1, uintptr(unsafe.Pointer(mutexName)))
	if handle == 0 {
		os.Exit(1)
	}
	defer pCloseHandle.Call(handle)
	defer pReleaseMutex.Call(handle)

	// Check if mutex already existed (another instance is running)
	if callErr == syscall.Errno(errorAlreadyExists) {
		title, _ := syscall.UTF16PtrFromString("Already Running")
		msg, _ := syscall.UTF16PtrFromString("Markdown Paste HTML is already running. Check the system tray.")
		pMessageBox.Call(0, uintptr(unsafe.Pointer(msg)), uintptr(unsafe.Pointer(title)), 0x00000040) // MB_ICONINFORMATION
		return
	}

	systray.Run(onReady, onExit)
}
