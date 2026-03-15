# Markdown Paste HTML

A lightweight Windows utility that runs in the background and converts markdown text from the clipboard into rich HTML/RTF format for pasting into any application.

## Features

- 🚀 **Background Service**: Runs silently in the system tray
- ⌨️ **Global Hotkey**: Press `Ctrl+Shift+M` (configurable) to convert and paste
- 📋 **Smart Conversion**: Converts plain text markdown to rich HTML
- 🎯 **Auto-Paste**: Automatically pastes the converted content
- 🔔 **Toast Notifications**: Non-intrusive feedback for errors
- 🔄 **Multi-Format Clipboard**: Outputs HTML and plain text for maximum compatibility
- 🌟 **System Tray Integration**: Enable/disable hotkey, change shortcut, toggle auto-start
- 🏁 **Auto-Start Support**: Optionally start with Windows

## How It Works

1. Copy markdown text to clipboard (e.g., `# Hello **world**`)
2. Position cursor where you want to paste
3. Press `Ctrl+Shift+M` (or your configured shortcut)
4. The utility converts markdown → HTML and automatically pastes the rich formatted text

## Usage Example

**Input (clipboard):**

```markdown
# My Document

This is **bold** and this is _italic_.

- List item 1
- List item 2

[Link text](https://example.com)
```

**Output (pasted):**

- In **Word/Outlook**: Formatted heading, bold, italic, lists, hyperlinks
- In **Gmail/Browser**: Rich HTML formatting
- In **Notepad**: Falls back to plain text

## Building

### Prerequisites

- .NET 8 SDK or later
- Windows 10 (version 1809) or later

### Build Debug Version

```bash
dotnet build
```

### Build Release (Single-File Executable)

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

The executable will be in: `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/MarkdownPasteHtml.exe`

### Run from WSL

```bash
./bin/Debug/net8.0-windows10.0.19041.0/win-x64/MarkdownPasteHtml.exe
```

## System Tray Menu

Right-click the tray icon to access:

- **✓ Hotkey Enabled** - Toggle hotkey on/off
- **Change Shortcut...** - Set a custom hotkey combination
- **Start with Windows** - Enable/disable auto-start
- **About** - View application info
- **Exit** - Close the application

## Settings

Settings are automatically saved to:

```
%APPDATA%\MarkdownPasteHtml\settings.json
```

Contains:

- `HotkeyEnabled`: Whether the hotkey is active
- `HotkeyModifiers`: Modifier keys bitmask (Ctrl=2, Alt=1, Shift=4)
- `HotkeyKey`: Virtual key code for the hotkey
- `AutoStartEnabled`: Whether to start with Windows

## Supported Markdown

The utility uses Markdig with advanced extensions and supports:

- Headers (`#`, `##`, etc.)
- Bold (`**text**` or `__text__`)
- Italic (`*text*` or `_text_`)
- Strikethrough (`~~text~~`)
- Lists (ordered and unordered)
- Task lists (`- [x]`, `- [ ]`)
- Tables (pipe tables)
- Links (`[text](url)`)
- Code blocks (` ``` `)
- Inline code (`` `code` ``)
- Blockquotes (`>`)
- Horizontal rules (`---`)

## Troubleshooting

### Hotkey Not Working

- Check if another application is using the same shortcut
- Try changing the shortcut via the tray menu
- Try toggling the hotkey off and on in the tray menu
- Restart the application

### Paste Not Working

- Ensure the target application has focus
- The clipboard is updated, so you can manually paste with `Ctrl+V`

### Toast Notifications Not Appearing

- Windows toast notifications require Windows 10+
- Check Windows notification settings

### Not Starting with Windows

- Requires permission to write to registry
- Check Windows startup settings manually

## Architecture

- **Program.cs** - Application entry point with single-instance enforcement
- **TrayApplicationContext.cs** - Main application logic and tray management
- **GlobalHotkey.cs** - Win32 hotkey registration and message handling
- **ClipboardManager.cs** - Multi-format clipboard operations (HTML, plain text)
- **MarkdownConverter.cs** - Markdown to HTML conversion (Markdig)
- **AutoPaster.cs** - Simulates Ctrl+V using SendInput API
- **NotificationService.cs** - Windows toast notifications
- **AutoStartManager.cs** - Windows startup registry management
- **Settings.cs** - Persistent configuration storage

## License

MIT License - feel free to modify and distribute.

## Contributing

Issues and pull requests welcome!
