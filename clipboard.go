//go:build windows

package main

import (
	"encoding/binary"
	"fmt"
	"syscall"
	"time"
	"unsafe"
)

var (
	pOpenClipboard              = user32.NewProc("OpenClipboard")
	pCloseClipboard             = user32.NewProc("CloseClipboard")
	pEmptyClipboard             = user32.NewProc("EmptyClipboard")
	pSetClipboardData           = user32.NewProc("SetClipboardData")
	pGetClipboardData           = user32.NewProc("GetClipboardData")
	pIsClipboardFormatAvailable = user32.NewProc("IsClipboardFormatAvailable")
	pRegisterClipboardFormatW   = user32.NewProc("RegisterClipboardFormatW")

	pGlobalAlloc  = kernel32.NewProc("GlobalAlloc")
	pGlobalLock   = kernel32.NewProc("GlobalLock")
	pGlobalUnlock = kernel32.NewProc("GlobalUnlock")
)

const (
	gmemMoveable  = 0x0002
	cfUnicodeText = 13
)

func tryOpenClipboard(maxRetries int, delayMs int) bool {
	for i := 0; i < maxRetries; i++ {
		ret, _, _ := pOpenClipboard.Call(0)
		if ret != 0 {
			return true
		}
		time.Sleep(time.Duration(delayMs) * time.Millisecond)
	}
	return false
}

// GetClipboardText reads Unicode text from the clipboard
func GetClipboardText() (string, error) {
	ret, _, _ := pIsClipboardFormatAvailable.Call(cfUnicodeText)
	if ret == 0 {
		return "", fmt.Errorf("no unicode text in clipboard")
	}

	if !tryOpenClipboard(10, 50) {
		return "", fmt.Errorf("could not open clipboard")
	}
	defer pCloseClipboard.Call()

	hData, _, _ := pGetClipboardData.Call(cfUnicodeText)
	if hData == 0 {
		return "", fmt.Errorf("GetClipboardData returned null")
	}

	pData, _, _ := pGlobalLock.Call(hData)
	if pData == 0 {
		return "", fmt.Errorf("GlobalLock failed")
	}
	defer pGlobalUnlock.Call(hData)

	// Read UTF-16 null-terminated string
	// pData is a valid pointer returned by GlobalLock - conversion from uintptr is safe here
	ptr := (*[1 << 20]uint16)(unsafe.Pointer(pData)) //nolint:govet
	text := syscall.UTF16ToString(ptr[:])
	if text == "" {
		return "", fmt.Errorf("clipboard text is empty")
	}

	return text, nil
}

// SetClipboardMultiFormat writes both HTML Format and CF_UNICODETEXT to the clipboard
func SetClipboardMultiFormat(plainText, html string) error {
	// Build the CF_HTML clipboard format string
	htmlClipboard := buildHTMLClipboard(html)

	// Register HTML Format
	formatName, _ := syscall.UTF16PtrFromString("HTML Format")
	cfHTML, _, _ := pRegisterClipboardFormatW.Call(uintptr(unsafe.Pointer(formatName)))
	if cfHTML == 0 {
		return fmt.Errorf("could not register HTML Format")
	}

	if !tryOpenClipboard(10, 50) {
		return fmt.Errorf("could not open clipboard")
	}
	defer pCloseClipboard.Call()

	pEmptyClipboard.Call()

	// 1) Set HTML Format - UTF-8 encoded bytes with null terminator
	htmlBytes := append([]byte(htmlClipboard), 0)
	if err := setClipboardBytes(uint32(cfHTML), htmlBytes); err != nil {
		return fmt.Errorf("setting HTML clipboard data: %w", err)
	}

	// 2) Set CF_UNICODETEXT - plain text (UTF-16LE with null terminator)
	utf16 := syscall.StringToUTF16(plainText) // includes null terminator
	textBytes := make([]byte, len(utf16)*2)
	for i, v := range utf16 {
		binary.LittleEndian.PutUint16(textBytes[i*2:], v)
	}
	if err := setClipboardBytes(cfUnicodeText, textBytes); err != nil {
		return fmt.Errorf("setting text clipboard data: %w", err)
	}

	return nil
}

func setClipboardBytes(format uint32, data []byte) error {
	hMem, _, _ := pGlobalAlloc.Call(gmemMoveable, uintptr(len(data)))
	if hMem == 0 {
		return fmt.Errorf("GlobalAlloc failed")
	}

	pMem, _, _ := pGlobalLock.Call(hMem)
	if pMem == 0 {
		return fmt.Errorf("GlobalLock failed")
	}

	// Copy data to global memory
	// pMem is a valid pointer returned by GlobalLock - conversion from uintptr is safe here
	dstPtr := (*byte)(unsafe.Pointer(pMem)) //nolint:govet
	dst := unsafe.Slice(dstPtr, len(data))
	copy(dst, data)

	pGlobalUnlock.Call(hMem)

	ret, _, _ := pSetClipboardData.Call(uintptr(format), hMem)
	if ret == 0 {
		return fmt.Errorf("SetClipboardData failed")
	}

	return nil
}

// buildHTMLClipboard builds the CF_HTML clipboard format with byte-offset headers
// Matches the format from the original C# BuildHtmlClipboard method
func buildHTMLClipboard(html string) string {
	const htmlPrefix = "<html>\r\n<head>\r\n<meta charset=\"UTF-8\">\r\n</head>\r\n<body>\r\n<!--StartFragment-->"
	const htmlSuffix = "<!--EndFragment-->\r\n</body>\r\n</html>"

	fullHTML := htmlPrefix + html + htmlSuffix

	// Header template — offsets are byte positions in UTF-8
	sampleHeader := "Version:0.9\r\n" +
		"StartHTML:0000000000\r\n" +
		"EndHTML:0000000000\r\n" +
		"StartFragment:0000000000\r\n" +
		"EndFragment:0000000000\r\n"

	headerLen := len(sampleHeader)
	startHTMLPos := headerLen
	startFragPos := headerLen + len(htmlPrefix)
	endFragPos := startFragPos + len(html)
	endHTMLPos := endFragPos + len(htmlSuffix)

	header := fmt.Sprintf("Version:0.9\r\n"+
		"StartHTML:%010d\r\n"+
		"EndHTML:%010d\r\n"+
		"StartFragment:%010d\r\n"+
		"EndFragment:%010d\r\n",
		startHTMLPos, endHTMLPos, startFragPos, endFragPos)

	return header + fullHTML
}
