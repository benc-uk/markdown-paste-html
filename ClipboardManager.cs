using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MarkdownPasteHtml;

public class ClipboardManager
{
  // Win32 clipboard API - bypass .NET DataObject entirely
  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool OpenClipboard(IntPtr hWndNewOwner);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool CloseClipboard();

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool EmptyClipboard();

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint RegisterClipboardFormatW([MarshalAs(UnmanagedType.LPWStr)] string lpszFormat);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr GetClipboardData(uint uFormat);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool IsClipboardFormatAvailable(uint format);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern IntPtr GlobalLock(IntPtr hMem);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool GlobalUnlock(IntPtr hMem);

  [DllImport("kernel32.dll")]
  private static extern UIntPtr GlobalSize(IntPtr hMem);

  private const uint GMEM_MOVEABLE = 0x0002;
  private const uint CF_UNICODETEXT = 13;

  private bool TryOpenClipboard(int maxRetries = 10, int delayMs = 50)
  {
    for (int i = 0; i < maxRetries; i++)
    {
      if (OpenClipboard(IntPtr.Zero))
        return true;
      Logger.Log($"OpenClipboard attempt {i + 1} failed (err={Marshal.GetLastWin32Error()}), retrying...");
      System.Threading.Thread.Sleep(delayMs);
    }
    Logger.LogError($"OpenClipboard failed after {maxRetries} attempts");
    return false;
  }

  public string? GetText()
  {
    // Use Win32 API directly to read clipboard - matches our Win32 writes
    try
    {
      if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
      {
        Logger.Log("GetText: CF_UNICODETEXT not available on clipboard");
        return null;
      }

      if (!TryOpenClipboard())
        return null;

      try
      {
        IntPtr hData = GetClipboardData(CF_UNICODETEXT);
        if (hData == IntPtr.Zero)
        {
          Logger.Log("GetText: GetClipboardData returned null");
          return null;
        }

        IntPtr pData = GlobalLock(hData);
        if (pData == IntPtr.Zero)
        {
          Logger.Log("GetText: GlobalLock returned null");
          return null;
        }

        try
        {
          string text = Marshal.PtrToStringUni(pData) ?? "";
          Logger.Log($"GetText: Read {text.Length} chars from clipboard");
          return string.IsNullOrEmpty(text) ? null : text;
        }
        finally
        {
          GlobalUnlock(hData);
        }
      }
      finally
      {
        CloseClipboard();
      }
    }
    catch (Exception ex)
    {
      Logger.LogError("Failed to get clipboard text", ex);
    }

    return null;
  }

  public bool SetMultiFormat(string plainText, string html, string? rtf)
  {
    try
    {
      Logger.Log($"SetMultiFormat called with HTML length: {html.Length}");

      // Build the CF_HTML clipboard format string
      string htmlClipboard = BuildHtmlClipboard(html);
      Logger.Log($"HTML clipboard (first 300 chars): {htmlClipboard.Substring(0, Math.Min(300, htmlClipboard.Length))}");

      // Register the HTML Format clipboard format
      uint cfHtml = RegisterClipboardFormatW("HTML Format");
      if (cfHtml == 0)
      {
        Logger.LogError($"RegisterClipboardFormat failed: {Marshal.GetLastWin32Error()}");
        return false;
      }
      Logger.Log($"Registered HTML Format as clipboard format ID: {cfHtml}");

      // Open clipboard with retry (another process may briefly hold it)
      if (!TryOpenClipboard())
        return false;

      try
      {
        EmptyClipboard();

        // 1) Set HTML Format - UTF-8 encoded bytes with null terminator
        byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlClipboard);
        IntPtr hHtml = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(htmlBytes.Length + 1));
        if (hHtml != IntPtr.Zero)
        {
          IntPtr pHtml = GlobalLock(hHtml);
          Marshal.Copy(htmlBytes, 0, pHtml, htmlBytes.Length);
          Marshal.WriteByte(pHtml, htmlBytes.Length, 0); // null terminator
          GlobalUnlock(hHtml);

          IntPtr result = SetClipboardData(cfHtml, hHtml);
          Logger.Log($"SetClipboardData HTML Format: {(result != IntPtr.Zero ? "SUCCESS" : $"FAILED err={Marshal.GetLastWin32Error()}")}");
        }

        // 2) Set CF_UNICODETEXT - plain text fallback (UTF-16)
        byte[] textBytes = Encoding.Unicode.GetBytes(plainText);
        IntPtr hText = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(textBytes.Length + 2));
        if (hText != IntPtr.Zero)
        {
          IntPtr pText = GlobalLock(hText);
          Marshal.Copy(textBytes, 0, pText, textBytes.Length);
          Marshal.WriteByte(pText, textBytes.Length, 0);     // null terminator
          Marshal.WriteByte(pText, textBytes.Length + 1, 0); // (2 bytes for UTF-16 null)
          GlobalUnlock(hText);

          IntPtr result = SetClipboardData(CF_UNICODETEXT, hText);
          Logger.Log($"SetClipboardData CF_UNICODETEXT: {(result != IntPtr.Zero ? "SUCCESS" : $"FAILED err={Marshal.GetLastWin32Error()}")}");
        }
      }
      finally
      {
        CloseClipboard();
        Logger.Log("Clipboard closed");
      }

      return true;
    }
    catch (Exception ex)
    {
      Logger.LogError("Failed to set clipboard", ex);
      return false;
    }
  }

  private string BuildHtmlClipboard(string html)
  {
    const string htmlPrefix = "<html>\r\n<head>\r\n<meta charset=\"UTF-8\">\r\n</head>\r\n<body>\r\n<!--StartFragment-->";
    const string htmlSuffix = "<!--EndFragment-->\r\n</body>\r\n</html>";

    string fullHtml = htmlPrefix + html + htmlSuffix;

    // Header template - offsets are byte positions in UTF-8
    string sampleHeader = "Version:0.9\r\n" +
                         "StartHTML:0000000000\r\n" +
                         "EndHTML:0000000000\r\n" +
                         "StartFragment:0000000000\r\n" +
                         "EndFragment:0000000000\r\n";

    int headerByteCount = Encoding.UTF8.GetByteCount(sampleHeader);
    int startHtmlPos = headerByteCount;
    int startFragmentPos = headerByteCount + Encoding.UTF8.GetByteCount(htmlPrefix);
    int endFragmentPos = startFragmentPos + Encoding.UTF8.GetByteCount(html);
    int endHtmlPos = endFragmentPos + Encoding.UTF8.GetByteCount(htmlSuffix);

    string result = "Version:0.9\r\n" +
                   $"StartHTML:{startHtmlPos:D10}\r\n" +
                   $"EndHTML:{endHtmlPos:D10}\r\n" +
                   $"StartFragment:{startFragmentPos:D10}\r\n" +
                   $"EndFragment:{endFragmentPos:D10}\r\n" +
                   fullHtml;

    Logger.Log($"BuildHtmlClipboard: startHTML={startHtmlPos}, endHTML={endHtmlPos}, startFrag={startFragmentPos}, endFrag={endFragmentPos}");

    return result;
  }
}
