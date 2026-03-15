using System;
using System.Windows.Forms;

namespace MarkdownPasteHtml;

public class HotkeyPickerDialog : Form
{
  private readonly Label _instructionLabel;
  private readonly Label _hotkeyLabel;
  private readonly Button _okButton;
  private readonly Button _cancelButton;

  public uint ResultModifiers { get; private set; }
  public uint ResultKey { get; private set; }

  // Win32 modifier constants
  private const uint MOD_ALT = 0x0001;
  private const uint MOD_CONTROL = 0x0002;
  private const uint MOD_SHIFT = 0x0004;

  public HotkeyPickerDialog(uint currentModifiers, uint currentKey)
  {
    ResultModifiers = currentModifiers;
    ResultKey = currentKey;

    Text = "Change Shortcut";
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MaximizeBox = false;
    MinimizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    ClientSize = new System.Drawing.Size(330, 145);
    KeyPreview = true;

    _instructionLabel = new Label
    {
      Text = "Press a new shortcut combo (e.g. Ctrl+Shift+K):",
      AutoSize = true,
      Location = new System.Drawing.Point(20, 20)
    };

    _hotkeyLabel = new Label
    {
      Text = FormatHotkey(currentModifiers, currentKey),
      Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold),
      AutoSize = true,
      Location = new System.Drawing.Point(20, 50)
    };

    _okButton = new Button
    {
      Text = "OK",
      DialogResult = DialogResult.OK,
      Location = new System.Drawing.Point(140, 100),
      Size = new System.Drawing.Size(80, 30)
    };

    _cancelButton = new Button
    {
      Text = "Cancel",
      DialogResult = DialogResult.Cancel,
      Location = new System.Drawing.Point(230, 100),
      Size = new System.Drawing.Size(80, 30)
    };

    AcceptButton = _okButton;
    CancelButton = _cancelButton;

    Controls.Add(_instructionLabel);
    Controls.Add(_hotkeyLabel);
    Controls.Add(_okButton);
    Controls.Add(_cancelButton);
  }

  protected override void OnKeyDown(KeyEventArgs e)
  {
    e.SuppressKeyPress = true;

    // Ignore lone modifier presses
    if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
    {
      base.OnKeyDown(e);
      return;
    }

    // Require at least one modifier
    uint mods = 0;
    if (e.Control) mods |= MOD_CONTROL;
    if (e.Alt) mods |= MOD_ALT;
    if (e.Shift) mods |= MOD_SHIFT;

    if (mods == 0)
    {
      base.OnKeyDown(e);
      return;
    }

    uint vk = (uint)e.KeyCode;
    ResultModifiers = mods;
    ResultKey = vk;
    _hotkeyLabel.Text = FormatHotkey(mods, vk);

    base.OnKeyDown(e);
  }

  private static string FormatHotkey(uint modifiers, uint key)
  {
    var parts = new System.Collections.Generic.List<string>();
    if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
    if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
    if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");

    var keyName = ((Keys)key).ToString();
    parts.Add(keyName);
    return string.Join(" + ", parts);
  }
}
