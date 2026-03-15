using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MarkdownPasteHtml;

public class TrayApplicationContext : ApplicationContext
{
  private readonly NotifyIcon _trayIcon;
  private readonly GlobalHotkey _globalHotkey;
  private readonly ClipboardManager _clipboardManager;
  private readonly MarkdownConverter _markdownConverter;
  private readonly AutoPaster _autoPaster;
  private readonly NotificationService _notificationService;
  private readonly AutoStartManager _autoStartManager;
  private readonly Settings _settings;

  private ToolStripMenuItem? _enableDisableMenuItem;
  private ToolStripMenuItem? _autoStartMenuItem;
  private ToolStripMenuItem? _changeShortcutMenuItem;

  public TrayApplicationContext()
  {
    // Initialize services
    _clipboardManager = new ClipboardManager();
    _markdownConverter = new MarkdownConverter();
    _autoPaster = new AutoPaster();
    _notificationService = new NotificationService();
    _autoStartManager = new AutoStartManager();
    _settings = Settings.Load();

    // Initialize global hotkey
    _globalHotkey = new GlobalHotkey();
    _globalHotkey.HotkeyPressed += OnHotkeyPressed;

    // Create tray icon
    _trayIcon = new NotifyIcon
    {
      Icon = CreateIcon(),
      Text = $"Markdown Paste HTML\n{_settings.HotkeyDisplayString} to convert and paste",
      Visible = true,
      ContextMenuStrip = CreateContextMenu()
    };

    // Register hotkey if enabled
    if (_settings.HotkeyEnabled)
    {
      RegisterHotkey();
    }

    // Sync auto-start setting
    if (_settings.AutoStartEnabled != _autoStartManager.IsAutoStartEnabled())
    {
      _autoStartManager.SetAutoStart(_settings.AutoStartEnabled);
    }

    UpdateMenuItems();
  }

  private Icon CreateIcon()
  {
    // Create a simple icon (16x16 bitmap with "M" letter)
    // In production, you would load this from a resource file
    var bitmap = new Bitmap(16, 16);
    using (var g = Graphics.FromImage(bitmap))
    {
      g.Clear(Color.White);
      g.FillRectangle(Brushes.DodgerBlue, 0, 0, 16, 16);
      g.DrawString("M", new Font("Arial", 10, FontStyle.Bold), Brushes.White, -2, -1);
    }
    return Icon.FromHandle(bitmap.GetHicon());
  }

  private ContextMenuStrip CreateContextMenu()
  {
    var menu = new ContextMenuStrip();

    _enableDisableMenuItem = new ToolStripMenuItem("Enable Hotkey", null, OnToggleHotkey);
    menu.Items.Add(_enableDisableMenuItem);

    _changeShortcutMenuItem = new ToolStripMenuItem("Change Shortcut...", null, OnChangeShortcut);
    menu.Items.Add(_changeShortcutMenuItem);

    menu.Items.Add(new ToolStripSeparator());

    _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", null, OnToggleAutoStart);
    menu.Items.Add(_autoStartMenuItem);

    menu.Items.Add(new ToolStripSeparator());

    menu.Items.Add(new ToolStripMenuItem("About", null, OnAbout));
    menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

    return menu;
  }

  private void UpdateMenuItems()
  {
    if (_enableDisableMenuItem != null)
    {
      _enableDisableMenuItem.Text = _settings.HotkeyEnabled ? "✓ Hotkey Enabled" : "Enable Hotkey";
    }

    if (_autoStartMenuItem != null)
    {
      _autoStartMenuItem.Checked = _settings.AutoStartEnabled;
    }
  }

  private void RegisterHotkey()
  {
    bool success = _globalHotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);

    if (!success)
    {
      MessageBox.Show(
          $"Could not register {_settings.HotkeyDisplayString} hotkey.\n\n" +
          "It may be in use by another application, or hotkey registration may not work in this environment (WSL).",
          "Hotkey Registration Failed",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
    }
  }

  private async void OnHotkeyPressed(object? sender, EventArgs e)
  {
    await PerformConversionAndPaste();
  }

  private async Task PerformConversionAndPaste()
  {
    try
    {
      // Get clipboard text
      string? clipboardText = _clipboardManager.GetText();

      if (string.IsNullOrWhiteSpace(clipboardText))
      {
        _notificationService.ShowWarning(
            "Clipboard Empty",
            "No text found in clipboard.");
        return;
      }

      // Check if it looks like markdown
      bool isMarkdown = _markdownConverter.IsLikelyMarkdown(clipboardText);

      if (!isMarkdown)
      {
        _notificationService.ShowWarning(
            "Not Markdown",
            "Clipboard content doesn't appear to be markdown.");
        return;
      }

      // Convert markdown to HTML
      string html = _markdownConverter.ConvertToHtml(clipboardText);

      // Set clipboard with HTML and plain text formats
      bool clipboardSet = _clipboardManager.SetMultiFormat(clipboardText, html, null);

      if (!clipboardSet)
      {
        _notificationService.ShowWarning("Clipboard Error", "Could not write to clipboard. Try again.");
        return;
      }

      // Auto-paste
      bool pasteSuccess = await _autoPaster.PasteAsync();

      if (!pasteSuccess)
      {
        _notificationService.ShowWarning(
            "Paste Failed",
            "Converted clipboard content but could not auto-paste. Try pasting manually (Ctrl+V).");
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show(
          $"Error: {ex.Message}",
          "Conversion Failed",
          MessageBoxButtons.OK,
          MessageBoxIcon.Error);

      _notificationService.ShowError(
          "Conversion Failed",
          $"Error: {ex.Message}");
    }
  }

  private void OnToggleHotkey(object? sender, EventArgs e)
  {
    _settings.HotkeyEnabled = !_settings.HotkeyEnabled;
    _settings.Save();

    if (_settings.HotkeyEnabled)
    {
      RegisterHotkey();
    }
    else
    {
      _globalHotkey.Unregister();
    }

    UpdateMenuItems();
  }

  private void OnToggleAutoStart(object? sender, EventArgs e)
  {
    _settings.AutoStartEnabled = !_settings.AutoStartEnabled;
    _settings.Save();

    if (!_autoStartManager.SetAutoStart(_settings.AutoStartEnabled))
    {
      _notificationService.ShowError(
          "Auto-Start Failed",
          "Could not update Windows startup settings.");

      // Revert setting
      _settings.AutoStartEnabled = !_settings.AutoStartEnabled;
      _settings.Save();
    }

    UpdateMenuItems();
  }

  private void OnChangeShortcut(object? sender, EventArgs e)
  {
    using var dialog = new HotkeyPickerDialog(_settings.HotkeyModifiers, _settings.HotkeyKey);
    if (dialog.ShowDialog() == DialogResult.OK)
    {
      _globalHotkey.Unregister();

      _settings.HotkeyModifiers = dialog.ResultModifiers;
      _settings.HotkeyKey = dialog.ResultKey;
      _settings.Save();

      if (_settings.HotkeyEnabled)
      {
        RegisterHotkey();
      }

      _trayIcon.Text = $"Markdown Paste HTML\n{_settings.HotkeyDisplayString} to convert and paste";
    }
  }


  private void OnAbout(object? sender, EventArgs e)
  {
    MessageBox.Show(
        "Markdown Paste HTML v1.0\n\n" +
        "Converts markdown in clipboard to HTML and auto-pastes.\n\n" +
        $"Hotkey: {_settings.HotkeyDisplayString}\n\n" +
        "Usage:\n" +
        "1. Copy markdown text\n" +
        $"2. Press {_settings.HotkeyDisplayString}\n" +
        "3. Content is converted and pasted automatically",
        "About Markdown Paste HTML",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
  }

  private void OnExit(object? sender, EventArgs e)
  {
    _trayIcon.Visible = false;
    _globalHotkey.Dispose();
    _trayIcon.Dispose();
    Application.Exit();
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _trayIcon?.Dispose();
      _globalHotkey?.Dispose();
    }
    base.Dispose(disposing);
  }
}
