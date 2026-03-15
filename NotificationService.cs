using System;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace MarkdownPasteHtml;

public class NotificationService
{
  private const string AppId = "MarkdownPasteHtml";

  public void ShowSuccess(string message)
  {
    try
    {
      var content = new ToastContentBuilder()
          .AddText("Markdown Paste HTML")
          .AddText(message)
          .GetToastContent();

      var toast = new ToastNotification(content.GetXml());
      ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
    }
    catch
    {
      // Toast notifications may fail if system doesn't support them
      // Fail silently
    }
  }

  public void ShowError(string title, string message)
  {
    try
    {
      var content = new ToastContentBuilder()
          .AddText(title)
          .AddText(message)
          .GetToastContent();

      var toast = new ToastNotification(content.GetXml());
      ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
    }
    catch
    {
      // Toast notifications may fail if system doesn't support them
      // Fail silently
    }
  }

  public void ShowWarning(string title, string message)
  {
    try
    {
      var content = new ToastContentBuilder()
          .AddText(title)
          .AddText(message)
          .GetToastContent();

      var toast = new ToastNotification(content.GetXml());
      ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
    }
    catch
    {
      // Toast notifications may fail if system doesn't support them
      // Fail silently
    }
  }
}
