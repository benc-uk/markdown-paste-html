//go:build windows

package main

import (
	"fmt"
	"os"

	"golang.org/x/sys/windows/registry"
)

const (
	appName    = "MarkdownPasteHtml"
	runKeyPath = `Software\Microsoft\Windows\CurrentVersion\Run`
)

// IsAutoStartEnabled checks if the app is registered to start with Windows
func IsAutoStartEnabled() bool {
	key, err := registry.OpenKey(registry.CURRENT_USER, runKeyPath, registry.QUERY_VALUE)
	if err != nil {
		return false
	}
	defer key.Close()

	_, _, err = key.GetStringValue(appName)
	return err == nil
}

// SetAutoStart enables or disables auto-start with Windows
func SetAutoStart(enable bool) error {
	key, err := registry.OpenKey(registry.CURRENT_USER, runKeyPath, registry.SET_VALUE)
	if err != nil {
		return fmt.Errorf("opening Run key: %w", err)
	}
	defer key.Close()

	if enable {
		exePath, err := os.Executable()
		if err != nil {
			return fmt.Errorf("getting executable path: %w", err)
		}
		return key.SetStringValue(appName, fmt.Sprintf(`"%s"`, exePath))
	}

	err = key.DeleteValue(appName)
	if err == registry.ErrNotExist {
		return nil
	}
	return err
}
