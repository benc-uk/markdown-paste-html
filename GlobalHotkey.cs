using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MarkdownPasteHtml;

public class GlobalHotkey : IDisposable
{
  [DllImport("user32.dll")]
  private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

  [DllImport("user32.dll")]
  private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

  private const int WM_HOTKEY = 0x0312;
  private const int HOTKEY_ID = 1;

  private readonly MessageWindow _messageWindow;
  private bool _isRegistered = false;
  private uint _modifiers;
  private uint _key;

  public event EventHandler? HotkeyPressed;

  public GlobalHotkey()
  {
    _messageWindow = new MessageWindow(this);
  }

  public bool Register(uint modifiers, uint key)
  {
    // Unregister any existing hotkey first
    Unregister();

    _modifiers = modifiers;
    _key = key;

    _isRegistered = RegisterHotKey(
        _messageWindow.Handle,
        HOTKEY_ID,
        _modifiers,
        _key);

    return _isRegistered;
  }

  public void Unregister()
  {
    if (_isRegistered)
    {
      UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID);
      _isRegistered = false;
    }
  }

  public void Dispose()
  {
    Unregister();
    _messageWindow.Dispose();
  }

  private class MessageWindow : NativeWindow, IDisposable
  {
    private readonly GlobalHotkey _parent;

    public MessageWindow(GlobalHotkey parent)
    {
      _parent = parent;
      CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
      if (m.Msg == WM_HOTKEY)
      {
        _parent.HotkeyPressed?.Invoke(_parent, EventArgs.Empty);
      }

      base.WndProc(ref m);
    }

    public void Dispose()
    {
      DestroyHandle();
    }
  }
}
