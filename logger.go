//go:build windows

package main

import (
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"
)

var logMu sync.Mutex

func logPath() string {
	configDir, err := os.UserConfigDir()
	if err != nil {
		configDir = os.TempDir()
	}
	return filepath.Join(configDir, "MarkdownPasteHtml", "app.log")
}

func Log(message string) {
	logMu.Lock()
	defer logMu.Unlock()

	path := logPath()
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0700); err != nil {
		return
	}

	entry := fmt.Sprintf("[%s] %s\n", time.Now().Format("2006-01-02 15:04:05"), message)

	f, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0600)
	if err != nil {
		return
	}
	defer f.Close()

	_, _ = f.WriteString(entry)
}

func LogError(message string, err error) {
	if err != nil {
		Log(fmt.Sprintf("ERROR: %s - %v", message, err))
	} else {
		Log(fmt.Sprintf("ERROR: %s", message))
	}
}

func ClearLog() {
	path := logPath()
	_ = os.Remove(path)
}
