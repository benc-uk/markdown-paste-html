using System;
using System.IO;
using System.Text.Json;

namespace MarkdownPasteHtml;

public class Settings
{
  private static readonly string SettingsPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "MarkdownPasteHtml",
      "settings.json");

  public bool HotkeyEnabled { get; set; } = true;
  public bool AutoStartEnabled { get; set; } = false;

  // Hotkey configuration (stored as raw Win32 values)
  public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
  public uint HotkeyKey { get; set; } = 0x4D; // VK_M

  public string HotkeyDisplayString
  {
    get
    {
      var parts = new System.Collections.Generic.List<string>();
      if ((HotkeyModifiers & 0x0002) != 0) parts.Add("Ctrl");
      if ((HotkeyModifiers & 0x0001) != 0) parts.Add("Alt");
      if ((HotkeyModifiers & 0x0004) != 0) parts.Add("Shift");
      if ((HotkeyModifiers & 0x0008) != 0) parts.Add("Win");

      var keyName = ((System.Windows.Forms.Keys)HotkeyKey).ToString();
      parts.Add(keyName);
      return string.Join("+", parts);
    }
  }

  public static Settings Load()
  {
    try
    {
      if (File.Exists(SettingsPath))
      {
        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
      }
    }
    catch
    {
      // If loading fails, return default settings
    }

    return new Settings();
  }

  public void Save()
  {
    try
    {
      var directory = Path.GetDirectoryName(SettingsPath);
      if (directory != null && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
      {
        WriteIndented = true
      });
      File.WriteAllText(SettingsPath, json);
    }
    catch
    {
      // Fail silently - settings persistence is not critical
    }
  }
}
