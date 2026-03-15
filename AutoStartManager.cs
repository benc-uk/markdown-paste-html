using System;
using Microsoft.Win32;

namespace MarkdownPasteHtml;

public class AutoStartManager
{
  private const string AppName = "MarkdownPasteHtml";
  private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

  public bool IsAutoStartEnabled()
  {
    try
    {
      using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
      if (key != null)
      {
        var value = key.GetValue(AppName);
        return value != null;
      }
    }
    catch
    {
      // If we can't read registry, assume it's not enabled
    }

    return false;
  }

  public bool SetAutoStart(bool enable)
  {
    try
    {
      using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
      if (key == null)
        return false;

      if (enable)
      {
        // Get the executable path - for single-file apps, use the process path
        string exePath = Environment.ProcessPath ??
                         System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ??
                         AppContext.BaseDirectory;

        // Ensure we have an .exe extension
        if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
          exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
        }

        key.SetValue(AppName, $"\"{exePath}\"");
      }
      else
      {
        key.DeleteValue(AppName, false);
      }

      return true;
    }
    catch
    {
      return false;
    }
  }
}
