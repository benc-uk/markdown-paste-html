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

  public TrayApplicationContext()
  {
    Logger.Log("Application starting...");

    // Initialize services
    _clipboardManager = new ClipboardManager();
    _markdownConverter = new MarkdownConverter();
    _autoPaster = new AutoPaster();
    _notificationService = new NotificationService();
    _autoStartManager = new AutoStartManager();
    _settings = Settings.Load();

    Logger.Log($"Settings loaded: HotkeyEnabled={_settings.HotkeyEnabled}, AutoStartEnabled={_settings.AutoStartEnabled}");

    // Initialize global hotkey
    _globalHotkey = new GlobalHotkey();
    _globalHotkey.HotkeyPressed += OnHotkeyPressed;

    // Create tray icon
    _trayIcon = new NotifyIcon
    {
      Icon = CreateIcon(),
      Text = "Markdown Paste HTML\nCtrl+Shift+B to convert and paste",
      Visible = true,
      ContextMenuStrip = CreateContextMenu()
    };

    // Register hotkey if enabled
    if (_settings.HotkeyEnabled)
    {
      Logger.Log("Registering hotkey...");
      RegisterHotkey();
    }
    else
    {
      Logger.Log("Hotkey disabled in settings");
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

    menu.Items.Add(new ToolStripSeparator());

    _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", null, OnToggleAutoStart);
    menu.Items.Add(_autoStartMenuItem);

    menu.Items.Add(new ToolStripSeparator());

    menu.Items.Add(new ToolStripMenuItem("Test Conversion (Manual)", null, OnTestConversion));
    menu.Items.Add(new ToolStripMenuItem("View Log File", null, OnViewLog));
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
    Logger.Log("Attempting to register Ctrl+Shift+B hotkey...");
    bool success = _globalHotkey.Register();

    if (!success)
    {
      Logger.LogError("Hotkey registration failed");
      MessageBox.Show(
          "Could not register Ctrl+Shift+B hotkey.\n\n" +
          "It may be in use by another application, or hotkey registration may not work in this environment (WSL).\n\n" +
          $"Check log file at: {Logger.GetLogPath()}",
          "Hotkey Registration Failed",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
    }
    else
    {
      Logger.Log("Hotkey registered successfully");
    }
  }

  private async void OnTestConversion(object? sender, EventArgs e)
  {
    await PerformConversionAndPaste();
  }

  private async void OnHotkeyPressed(object? sender, EventArgs e)
  {
    Logger.Log("Hotkey pressed!");
    await PerformConversionAndPaste();
  }

  private async Task PerformConversionAndPaste()
  {
    try
    {
      Logger.Log("Starting conversion process...");

      // Get clipboard text
      string? clipboardText = _clipboardManager.GetText();
      Logger.Log($"Clipboard text retrieved: {(clipboardText != null ? clipboardText.Length + " chars" : "null")}");

      if (string.IsNullOrWhiteSpace(clipboardText))
      {
        Logger.Log("Clipboard is empty");
        _notificationService.ShowWarning(
            "Clipboard Empty",
            "No text found in clipboard.");
        return;
      }

      Logger.Log($"First 100 chars: {clipboardText.Substring(0, Math.Min(100, clipboardText.Length))}");

      // Check if it looks like markdown
      bool isMarkdown = _markdownConverter.IsLikelyMarkdown(clipboardText);
      Logger.Log($"Is likely markdown: {isMarkdown}");

      if (!isMarkdown)
      {
        _notificationService.ShowWarning(
            "Not Markdown",
            "Clipboard content doesn't appear to be markdown.");
        return;
      }

      // Convert markdown to HTML
      Logger.Log("Converting markdown to HTML...");
      string html = _markdownConverter.ConvertToHtml(clipboardText);
      Logger.Log($"HTML generated: {html.Length} chars");

      // Set clipboard with HTML and plain text formats
      Logger.Log("Setting clipboard with multiple formats...");
      bool clipboardSet = _clipboardManager.SetMultiFormat(clipboardText, html, null);

      if (!clipboardSet)
      {
        Logger.LogError("Failed to set clipboard - aborting paste");
        _notificationService.ShowWarning("Clipboard Error", "Could not write to clipboard. Try again.");
        return;
      }
      Logger.Log("Clipboard updated successfully");

      // Auto-paste
      Logger.Log("Attempting to paste...");

      bool pasteSuccess = await _autoPaster.PasteAsync();
      Logger.Log($"Paste success: {pasteSuccess}");

      if (!pasteSuccess)
      {
        _notificationService.ShowWarning(
            "Paste Failed",
            "Converted clipboard content but could not auto-paste. Try pasting manually (Ctrl+V).");
      }
    }
    catch (Exception ex)
    {
      Logger.LogError("Exception during conversion", ex);

      string errorMsg = $"Error: {ex.Message}\n\nLog file: {Logger.GetLogPath()}";

      MessageBox.Show(
          errorMsg,
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


  private void OnViewLog(object? sender, EventArgs e)
  {
    try
    {
      string logPath = Logger.GetLogPath();
      if (File.Exists(logPath))
      {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
          FileName = logPath,
          UseShellExecute = true
        });
      }
      else
      {
        MessageBox.Show($"Log file not found at:\n{logPath}", "No Log File", MessageBoxButtons.OK, MessageBoxIcon.Information);
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show($"Error opening log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void OnAbout(object? sender, EventArgs e)
  {
    MessageBox.Show(
        "Markdown Paste HTML v1.0\n\n" +
        "Converts markdown in clipboard to HTML and auto-pastes.\n\n" +
        "Hotkey: Ctrl+Shift+B\n\n" +
        "Usage:\n" +
        "1. Copy markdown text\n" +
        "2. Press Ctrl+Shift+B\n" +
        "3. Content is converted and pasted automatically\n\n" +
        $"Log file: {Logger.GetLogPath()}",
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
