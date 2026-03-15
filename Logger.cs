using System;
using System.IO;

namespace MarkdownPasteHtml;

public class Logger
{
  private static readonly string LogPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "MarkdownPasteHtml",
      "app.log");

  public static void Log(string message)
  {
    try
    {
      var directory = Path.GetDirectoryName(LogPath);
      if (directory != null && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
      File.AppendAllText(LogPath, logEntry + Environment.NewLine);
    }
    catch
    {
      // Can't log if logging fails
    }
  }

  public static void LogError(string message, Exception? ex = null)
  {
    string fullMessage = ex != null
        ? $"ERROR: {message} - {ex.Message}\n{ex.StackTrace}"
        : $"ERROR: {message}";

    Log(fullMessage);
  }

  public static void Clear()
  {
    try
    {
      if (File.Exists(LogPath))
      {
        File.Delete(LogPath);
      }
    }
    catch
    {
      // Ignore
    }
  }

  public static string GetLogPath() => LogPath;
}
