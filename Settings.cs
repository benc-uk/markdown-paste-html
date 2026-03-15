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
