# Markdown Paste HTML — Copilot Instructions

## Project Overview

A lightweight Windows-only system tray utility written in Go that converts markdown text from the clipboard into rich HTML format and auto-pastes it. Compiles to a single native `.exe` (~5MB) with zero runtime dependencies.

**Origin**: Rewritten from a .NET/C# WinForms application to Go to achieve a single static binary with no runtime or DLL dependencies.

## Architecture

### Core Flow

```
Hotkey pressed → GetClipboardText() → IsLikelyMarkdown() → ConvertToHTML() → SetClipboardMultiFormat() → Paste()
```

Each step can fail independently; failures show balloon notifications and abort the pipeline.

### File Responsibilities

| File               | Role                                                                                              |
| ------------------ | ------------------------------------------------------------------------------------------------- |
| `main.go`          | Entry point, single-instance enforcement via named mutex, launches systray                        |
| `tray.go`          | System tray UI hub, menu items, event dispatch, orchestrates the conversion pipeline              |
| `clipboard.go`     | Win32 clipboard I/O: reads CF_UNICODETEXT, writes both CF_HTML ("HTML Format") and CF_UNICODETEXT |
| `converter.go`     | Markdown → HTML via Goldmark with extensions; `IsLikelyMarkdown()` heuristic pre-check            |
| `paster.go`        | Simulates Ctrl+V via `SendInput`, waits for modifier key release to avoid Ctrl+Shift+V            |
| `hotkey.go`        | Global hotkey registration with dedicated OS-locked thread and Win32 message pump                 |
| `hotkey_dialog.go` | Modal dialog for hotkey reconfiguration, own OS-locked thread                                     |
| `icon.go`          | Programmatically generates a 16×16 ICO at init time for the tray icon                             |
| `notifications.go` | Balloon notifications via `Shell_NotifyIconW` (shell32.dll)                                       |
| `settings.go`      | JSON config at `%APPDATA%/MarkdownPasteHtml/settings.json`, modifier/key constants                |
| `autostart.go`     | Windows registry manipulation for Start with Windows (`HKCU\...\Run`)                             |
| `logger.go`        | Thread-safe file logging to `%APPDATA%/MarkdownPasteHtml/app.log`                                 |

### Resources

| File                  | Role                                                                      |
| --------------------- | ------------------------------------------------------------------------- |
| `res/icon.ico`        | 16×16 blue "M" icon (checked into repo)                                   |
| `rsrc.syso`           | Generated at build time by `rsrc`, embeds icon into the exe (git-ignored) |
| `cmd/genicon/main.go` | Standalone tool that programmatically generates `res/icon.ico`            |

## Build Constraints

- **All `.go` files** have `//go:build windows` — this is a Windows-only project
- **All files** use `package main` (single-package binary)
- Cross-compilation from Linux/macOS is supported and is the primary dev workflow

## Build Process

```bash
make          # runs: icon → build (default target)
make build    # cross-compiles for Windows with -H windowsgui -s -w
make clean    # removes bin/MarkdownPaste.exe and rsrc.syso
make lint     # runs go vet with GOOS=windows
make run      # build then run
```

The `icon` target runs `rsrc -ico res/icon.ico -o rsrc.syso` to generate a Windows resource file. Go's toolchain automatically links any `.syso` file in the package directory into the final binary.

**Prerequisites**: `rsrc` tool — install with `go install github.com/akavel/rsrc@latest`

**Build flags**:

- `-H windowsgui` — hides console window (essential for tray app). Omit this for debug builds to see panics in a console window.
- `-s -w` — strips debug symbols for smaller binary (~5MB)

## Win32 API Patterns

This project makes extensive direct Win32 syscalls. Follow these conventions:

### DLL Organisation

| DLL            | Variable   | Used for                                                |
| -------------- | ---------- | ------------------------------------------------------- |
| `kernel32.dll` | `kernel32` | Mutexes, global memory, module handles                  |
| `user32.dll`   | `user32`   | Windows, messages, hotkeys, clipboard, input simulation |
| `shell32.dll`  | `shell32`  | `Shell_NotifyIconW` (balloon notifications)             |
| `gdi32.dll`    | (local)    | Stock objects, device caps (dialog only)                |

**IMPORTANT**: Always verify which DLL exports a given function. `Shell_NotifyIconW` is in `shell32.dll`, NOT `user32.dll` — this was a bug that caused crashes.

### Syscall Conventions

- Use `syscall.NewLazyDLL()` and `.NewProc()` for late binding
- Convert Go strings to UTF-16 via `syscall.UTF16PtrFromString()` before passing to Win32
- The `.Call()` return tuple is `(r1, r2, err)` where `err` is always non-nil (it captures `GetLastError`)
- Use `unsafe.Pointer` for struct pointers passed to Win32 APIs
- Use `syscall.NewCallback()` to create C-callable function pointers from Go functions

### Threading Model

Win32 message pumps and hotkey registration are **thread-affine**. Any goroutine that creates a window or registers a hotkey must call `runtime.LockOSThread()` at the start and never unlock until the message loop exits.

The project uses three OS-locked threads:

1. **systray thread** — managed by `getlantern/systray` library
2. **hotkey thread** — `hotkey.go` message-only window (`HWND_MESSAGE`)
3. **dialog thread** — `hotkey_dialog.go` (temporary, created on demand)

Cross-thread communication uses `PostMessageW` with custom `WM_USER+N` messages and Go channels for synchronisation. This is critical — see lessons learned #6 and #9.

### Struct Layout (Critical)

When defining Win32 structs in Go, struct field alignment and size must exactly match the C definition. Key pitfalls:

- **Union fields**: C unions occupy a single field's worth of space. In `NOTIFYICONDATAW`, `uTimeout` and `uVersion` are a union — use ONE `uint32` field, not two. Having two shifts all subsequent fields and causes silent failures.
- **Pointer-sized fields**: `uintptr` and `syscall.Handle` are 8 bytes on amd64. Check alignment padding.
- **Validate with `unsafe.Sizeof()`** and `unsafe.Offsetof()` against known Windows struct sizes.

### Notification Icon IDs

The `getlantern/systray` library registers its notification icon with `ID: 100` and window class `"SystrayClass"`. Any code that modifies the notification icon (e.g. balloon notifications) must use the same `uID` and find the same `hWnd`.

## Dependencies

| Package                         | Purpose                                                                                        |
| ------------------------------- | ---------------------------------------------------------------------------------------------- |
| `github.com/getlantern/systray` | System tray integration (menus, icon, hidden window)                                           |
| `github.com/yuin/goldmark`      | Markdown → HTML with extensions (tables, strikethrough, task lists, linkify, definition lists) |
| `golang.org/x/sys`              | Windows registry access for auto-start                                                         |

## Coding Guidelines

- No console output — the app runs with `-H windowsgui`, so use `Log()` for debugging and balloon notifications for user feedback
- Clipboard access is contentious on Windows — always use retry loops (`tryOpenClipboard`)
- Always `defer pCloseClipboard.Call()` after opening the clipboard
- Global memory allocated for clipboard (`GlobalAlloc`) must NOT be freed after `SetClipboardData` — Windows owns it
- The `IsLikelyMarkdown()` heuristic is intentionally broad; false positives are acceptable since the user explicitly triggers conversion
- Settings use `encoding/json` with camelCase field tags
- Errors in the conversion pipeline show balloon notifications, not message boxes (non-blocking)
- Message boxes are reserved for startup errors and the About dialog

## Known Issues & Lessons Learned

1. **`Shell_NotifyIconW` is in `shell32.dll`**, not `user32.dll` — using the wrong DLL causes a crash when any balloon notification fires
2. **`GetStockObject` is in `gdi32.dll`**, not `user32.dll` — binding it to user32 panics when the hotkey dialog opens. Always verify which DLL exports a given Win32 function before adding a `NewProc()` binding.
3. **`NOTIFYICONDATAW.uTimeout`/`uVersion` is a C union** — must be a single `uint32` in Go, not two separate fields, or all fields after it shift by 4 bytes and balloons silently fail
4. **Systray icon ID is `1`** — the `getlantern/systray` library uses this; balloon notification code must use the same `uID`
5. **Modifier key timing**: After hotkey press, must wait for user to release all modifier keys before simulating Ctrl+V, otherwise the target app receives Ctrl+Shift+V (paste as plain text)
6. **`RegisterHotKey` is thread-affine** — must be called from the same OS thread that owns the HWND. Calling it from a different goroutine causes `ERROR_INVALID_WINDOW_HANDLE`. The hotkey module uses `PostMessageW` with custom `WM_USER+N` messages to marshal register/unregister calls onto the window's owning thread, with results passed back via Go channels.
7. **`CreateMutexW` error checking** — the third return value from `.Call()` captures `GetLastError()` immediately. Do NOT call `pGetLastError.Call()` separately — that's a new syscall and the original error is lost by then. Use `callErr == syscall.Errno(errorAlreadyExists)` directly.
8. **`PostQuitMessage` kills the entire app** — never call it from a dialog's `WM_DESTROY` handler. The `WM_QUIT` message is per-thread but can propagate to systray's message loop. Instead, use a `done` flag and a `PeekMessage` loop on a dedicated OS-locked thread.
9. **Dialog must run on its own OS-locked thread** — creating a Win32 window on systray's goroutine or the menu handler goroutine causes cross-thread HWND ownership issues. Spawn a new goroutine with `runtime.LockOSThread()`, create the window there, run a `PeekMessage` loop, and communicate results back via a Go channel.
10. **`PeekMessage` with hwnd=0 on a shared thread steals messages** — if the dialog's message loop runs on the same thread as systray, using `PeekMessage(hwnd=0)` consumes systray's messages too, causing undefined behavior. The dedicated-thread approach (lesson 9) avoids this entirely.
11. **Default hotkey `Ctrl+Shift+M` conflicts** — VS Code and other apps commonly register this combo. The default was changed to `Ctrl+Alt+M` to avoid conflicts. Users can change it via the tray menu.
12. **`HWND_MESSAGE` constant** — the value -3 cannot be represented as a Go `uintptr` literal (overflow). Use `^uintptr(2)` (bitwise NOT) to get the two's complement representation.
