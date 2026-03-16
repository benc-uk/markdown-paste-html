//go:build windows

package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
)

// Modifier constants matching Win32 MOD_* values
const (
	ModAlt     uint32 = 0x0001
	ModControl uint32 = 0x0002
	ModShift   uint32 = 0x0004
	ModWin     uint32 = 0x0008
)

type Settings struct {
	HotkeyEnabled    bool   `json:"hotkeyEnabled"`
	AutoStartEnabled bool   `json:"autoStartEnabled"`
	HotkeyModifiers  uint32 `json:"hotkeyModifiers"`
	HotkeyKey        uint32 `json:"hotkeyKey"`
}

func DefaultSettings() Settings {
	return Settings{
		HotkeyEnabled:    true,
		AutoStartEnabled: false,
		HotkeyModifiers:  ModControl | ModAlt, // Ctrl+Alt
		HotkeyKey:        0x4D,                // VK_M
	}
}

func settingsPath() string {
	configDir, err := os.UserConfigDir()
	if err != nil {
		configDir = os.TempDir()
	}
	return filepath.Join(configDir, "MarkdownPasteHtml", "settings.json")
}

func LoadSettings() Settings {
	path := settingsPath()

	data, err := os.ReadFile(path)
	if err != nil {
		return DefaultSettings()
	}

	var s Settings
	if err := json.Unmarshal(data, &s); err != nil {
		return DefaultSettings()
	}

	return s
}

func (s *Settings) Save() {
	path := settingsPath()

	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0700); err != nil {
		return
	}

	data, err := json.MarshalIndent(s, "", "  ")
	if err != nil {
		return
	}

	_ = os.WriteFile(path, data, 0600)
}

func (s *Settings) HotkeyDisplayString() string {
	parts := []string{}

	if s.HotkeyModifiers&ModControl != 0 {
		parts = append(parts, "Ctrl")
	}
	if s.HotkeyModifiers&ModAlt != 0 {
		parts = append(parts, "Alt")
	}
	if s.HotkeyModifiers&ModShift != 0 {
		parts = append(parts, "Shift")
	}
	if s.HotkeyModifiers&ModWin != 0 {
		parts = append(parts, "Win")
	}

	keyName := vkKeyName(s.HotkeyKey)
	parts = append(parts, keyName)

	result := ""
	for i, p := range parts {
		if i > 0 {
			result += "+"
		}
		result += p
	}
	return result
}

// vkKeyName returns a human-readable name for a virtual key code
func vkKeyName(vk uint32) string {
	// Letters A-Z
	if vk >= 0x41 && vk <= 0x5A {
		return string(rune(vk))
	}
	// Digits 0-9
	if vk >= 0x30 && vk <= 0x39 {
		return string(rune(vk))
	}
	// Function keys F1-F24
	if vk >= 0x70 && vk <= 0x87 {
		return fmt.Sprintf("F%d", vk-0x70+1)
	}

	switch vk {
	case 0x20:
		return "Space"
	case 0x0D:
		return "Enter"
	case 0x1B:
		return "Escape"
	case 0x09:
		return "Tab"
	case 0x08:
		return "Backspace"
	case 0x2D:
		return "Insert"
	case 0x2E:
		return "Delete"
	case 0x24:
		return "Home"
	case 0x23:
		return "End"
	case 0x21:
		return "PageUp"
	case 0x22:
		return "PageDown"
	default:
		return fmt.Sprintf("0x%02X", vk)
	}
}
