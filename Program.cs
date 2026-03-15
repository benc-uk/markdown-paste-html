using System;
using System.Threading;
using System.Windows.Forms;

namespace MarkdownPasteHtml;

static class Program
{
  private static Mutex? _mutex;
  private const string MutexName = "MarkdownPasteHtml_SingleInstance_Mutex";

  [STAThread]
  static void Main()
  {
    // Single instance enforcement
    _mutex = new Mutex(true, MutexName, out bool createdNew);

    if (!createdNew)
    {
      MessageBox.Show(
          "Markdown Paste HTML is already running. Check the system tray.",
          "Already Running",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
      return;
    }

    ApplicationConfiguration.Initialize();
    Application.Run(new TrayApplicationContext());

    _mutex?.ReleaseMutex();
    _mutex?.Dispose();
  }
}
