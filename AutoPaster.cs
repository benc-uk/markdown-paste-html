using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdownPasteHtml;

public class AutoPaster
{
  [DllImport("user32.dll")]
  private static extern IntPtr GetForegroundWindow();

  [DllImport("user32.dll")]
  private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

  [DllImport("user32.dll")]
  private static extern uint MapVirtualKey(uint uCode, uint uMapType);

  [DllImport("user32.dll")]
  private static extern short GetAsyncKeyState(int vKey);

  private const int INPUT_KEYBOARD = 1;
  private const uint KEYEVENTF_KEYDOWN = 0x0000;
  private const uint KEYEVENTF_KEYUP = 0x0002;
  private const uint KEYEVENTF_SCANCODE = 0x0008;

  // Virtual key codes
  private const ushort VK_SHIFT = 0x10;
  private const ushort VK_CONTROL = 0x11;
  private const ushort VK_MENU = 0x12; // Alt
  private const ushort VK_V = 0x56;

  [StructLayout(LayoutKind.Sequential)]
  private struct INPUT
  {
    public int type;
    public InputUnion u;
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct InputUnion
  {
    [FieldOffset(0)]
    public MOUSEINPUT mi;
    [FieldOffset(0)]
    public KEYBDINPUT ki;
    [FieldOffset(0)]
    public HARDWAREINPUT hi;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct KEYBDINPUT
  {
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MOUSEINPUT
  {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct HARDWAREINPUT
  {
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
  }

  public async Task<bool> PasteAsync()
  {
    // Check if there's a foreground window
    IntPtr foregroundWindow = GetForegroundWindow();
    if (foregroundWindow == IntPtr.Zero)
    {
      return false;
    }

    // Wait for user to release all modifier keys from the hotkey (Ctrl+Shift+M)
    // If we send Ctrl+V while user still holds Ctrl+Shift, it becomes Ctrl+Shift+V = paste plain text
    int maxWait = 2000; // 2 second max
    int waited = 0;
    while (waited < maxWait)
    {
      bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
      bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
      bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

      if (!ctrlDown && !shiftDown && !altDown)
        break;

      await Task.Delay(20);
      waited += 20;
    }

    // Small extra delay for key state to fully settle
    await Task.Delay(50);

    // Simulate Ctrl+V keypress
    INPUT[] inputs = new INPUT[4];

    inputs[0] = CreateKeyboardInput(VK_CONTROL, KEYEVENTF_KEYDOWN);
    inputs[1] = CreateKeyboardInput(VK_V, KEYEVENTF_KEYDOWN);
    inputs[2] = CreateKeyboardInput(VK_V, KEYEVENTF_KEYUP);
    inputs[3] = CreateKeyboardInput(VK_CONTROL, KEYEVENTF_KEYUP);

    uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

    return result == inputs.Length;
  }

  private INPUT CreateKeyboardInput(ushort virtualKey, uint flags)
  {
    return new INPUT
    {
      type = INPUT_KEYBOARD,
      u = new InputUnion
      {
        ki = new KEYBDINPUT
        {
          wVk = virtualKey,
          wScan = 0,
          dwFlags = flags,
          time = 0,
          dwExtraInfo = IntPtr.Zero
        }
      }
    };
  }
}
