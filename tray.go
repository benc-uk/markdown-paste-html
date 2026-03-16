//go:build windows

package main

import (
	"fmt"
	"syscall"
	"unsafe"

	"github.com/getlantern/systray"
)

var (
	settings    Settings
	hotkey      *GlobalHotkey
	menuEnabled *systray.MenuItem
	menuChange  *systray.MenuItem
	menuAuto    *systray.MenuItem
)

func onReady() {
	settings = LoadSettings()

	// Set tray icon (blue "M" as ICO)
	systray.SetIcon(iconData)
	systray.SetTooltip(fmt.Sprintf("Markdown Paste HTML\n%s to convert and paste", settings.HotkeyDisplayString()))

	// Build menu
	menuEnabled = systray.AddMenuItem("✓ Hotkey Enabled", "Toggle hotkey on/off")
	menuChange = systray.AddMenuItem("Change Shortcut...", "Change the keyboard shortcut")
	systray.AddSeparator()
	menuAuto = systray.AddMenuItemCheckbox("Start with Windows", "Launch on Windows startup", settings.AutoStartEnabled)
	systray.AddSeparator()
	menuAbout := systray.AddMenuItem("About", "About this application")
	menuExit := systray.AddMenuItem("Exit", "Exit the application")

	updateMenuState()

	// Sync auto-start with reality
	if settings.AutoStartEnabled != IsAutoStartEnabled() {
		_ = SetAutoStart(settings.AutoStartEnabled)
	}

	// Initialize hotkey handler
	var err error
	hotkey, err = NewGlobalHotkey()
	if err != nil {
		Log(fmt.Sprintf("Failed to create hotkey handler: %v", err))
		showMessageBox("Hotkey Error", fmt.Sprintf("Failed to create hotkey handler: %v", err))
		return
	}

	if settings.HotkeyEnabled {
		registerHotkey()
	}

	// Handle hotkey presses
	go func() {
		for range hotkey.Pressed() {
			performConversionAndPaste()
		}
	}()

	// Handle menu clicks
	go func() {
		for {
			select {
			case <-menuEnabled.ClickedCh:
				onToggleHotkey()
			case <-menuChange.ClickedCh:
				onChangeShortcut()
			case <-menuAuto.ClickedCh:
				onToggleAutoStart()
			case <-menuAbout.ClickedCh:
				onAbout()
			case <-menuExit.ClickedCh:
				systray.Quit()
			}
		}
	}()
}

func onExit() {
	if hotkey != nil {
		hotkey.Destroy()
	}
}

func registerHotkey() {
	err := hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey)
	if err != nil {
		Log(fmt.Sprintf("Hotkey registration failed: %v", err))
		showMessageBox("Hotkey Registration Failed",
			fmt.Sprintf("Could not register %s hotkey.\n\n%v\n\nIt may be in use by another application.\nTry changing it via the tray menu.",
				settings.HotkeyDisplayString(), err))
	}
}

func updateMenuState() {
	if settings.HotkeyEnabled {
		menuEnabled.SetTitle("✓ Hotkey Enabled")
	} else {
		menuEnabled.SetTitle("Enable Hotkey")
	}

	if settings.AutoStartEnabled {
		menuAuto.Check()
	} else {
		menuAuto.Uncheck()
	}
}

func performConversionAndPaste() {
	// Get clipboard text
	clipText, err := GetClipboardText()
	if err != nil {
		ShowBalloonWarning("Clipboard Empty", "No text found in clipboard.")
		return
	}

	// Check if it looks like markdown
	if !IsLikelyMarkdown(clipText) {
		ShowBalloonWarning("Not Markdown", "Clipboard content doesn't appear to be markdown.")
		return
	}

	// Convert markdown to HTML
	html, err := ConvertToHTML(clipText)
	if err != nil {
		ShowBalloonError("Conversion Failed", fmt.Sprintf("Error: %v", err))
		return
	}

	// Set clipboard with HTML and plain text formats
	if err := SetClipboardMultiFormat(clipText, html); err != nil {
		ShowBalloonWarning("Clipboard Error", "Could not write to clipboard. Try again.")
		return
	}

	// Auto-paste
	if err := Paste(); err != nil {
		ShowBalloonWarning("Paste Failed",
			"Converted clipboard content but could not auto-paste. Try pasting manually (Ctrl+V).")
	}
}

func onToggleHotkey() {
	settings.HotkeyEnabled = !settings.HotkeyEnabled
	settings.Save()

	if settings.HotkeyEnabled {
		registerHotkey()
	} else {
		hotkey.Unregister()
	}

	updateMenuState()
}

func onChangeShortcut() {
	mods, key, ok := ShowHotkeyDialog(settings.HotkeyModifiers, settings.HotkeyKey)
	if !ok {
		return
	}

	hotkey.Unregister()

	settings.HotkeyModifiers = mods
	settings.HotkeyKey = key
	settings.Save()

	if settings.HotkeyEnabled {
		registerHotkey()
	}

	systray.SetTooltip(fmt.Sprintf("Markdown Paste HTML\n%s to convert and paste", settings.HotkeyDisplayString()))
}

func onToggleAutoStart() {
	settings.AutoStartEnabled = !settings.AutoStartEnabled
	settings.Save()

	if err := SetAutoStart(settings.AutoStartEnabled); err != nil {
		ShowBalloonError("Auto-Start Failed", "Could not update Windows startup settings.")
		// Revert
		settings.AutoStartEnabled = !settings.AutoStartEnabled
		settings.Save()
	}

	updateMenuState()
}

func onAbout() {
	showMessageBox("About Markdown Paste HTML",
		fmt.Sprintf("Markdown Paste HTML v1.0\n\n"+
			"Converts markdown in clipboard to HTML and auto-pastes.\n\n"+
			"Hotkey: %s\n\n"+
			"Usage:\n"+
			"1. Copy markdown text\n"+
			"2. Press %s\n"+
			"3. Content is converted and pasted automatically",
			settings.HotkeyDisplayString(), settings.HotkeyDisplayString()))
}

func showMessageBox(title, message string) {
	t, _ := syscall.UTF16PtrFromString(title)
	m, _ := syscall.UTF16PtrFromString(message)
	pMessageBox.Call(0, uintptr(unsafe.Pointer(m)), uintptr(unsafe.Pointer(t)), 0x00000040) // MB_ICONINFORMATION
}
