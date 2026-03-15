using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownPasteHtml;

public class RtfConverter
{
  public string ConvertHtmlToRtf(string html)
  {
    if (string.IsNullOrWhiteSpace(html))
    {
      throw new ArgumentException("HTML content is empty", nameof(html));
    }

    try
    {
      // Simplified HTML to RTF conversion
      // This is a basic implementation - for production consider using RtfPipe library

      StringBuilder rtf = new StringBuilder();

      // RTF header
      rtf.Append(@"{\rtf1\ansi\deff0");
      rtf.Append(@"{\fonttbl{\f0\fswiss Arial;}{\f1\fmodern Courier;}}");
      rtf.Append(@"{\colortbl;\red0\green0\blue0;}");
      rtf.Append(@"\f0\fs20 ");

      string text = html;

      // Decode HTML entities first
      text = System.Net.WebUtility.HtmlDecode(text);

      // Process inline formatting (bold, italic, code) - these can be nested in blocks
      text = Regex.Replace(text, @"<strong>(.*?)</strong>", @"{\b $1}", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<b>(.*?)</b>", @"{\b $1}", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<em>(.*?)</em>", @"{\i $1}", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<i>(.*?)</i>", @"{\i $1}", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<code>(.*?)</code>", @"{\f1 $1}", RegexOptions.Singleline);

      // Process block elements
      text = Regex.Replace(text, @"<h1>(.*?)</h1>", @"{\fs32\b $1}\par\par", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<h2>(.*?)</h2>", @"{\fs28\b $1}\par\par", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<h3>(.*?)</h3>", @"{\fs24\b $1}\par\par", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<h[456]>(.*?)</h[456]>", @"{\b $1}\par\par", RegexOptions.Singleline);

      text = Regex.Replace(text, @"<p>(.*?)</p>", @"$1\par\par", RegexOptions.Singleline);
      text = Regex.Replace(text, @"<br\s*/?>", @"\line ", RegexOptions.IgnoreCase);

      // Lists
      text = Regex.Replace(text, @"<ul>", "", RegexOptions.IgnoreCase);
      text = Regex.Replace(text, @"</ul>", @"\par", RegexOptions.IgnoreCase);
      text = Regex.Replace(text, @"<ol>", "", RegexOptions.IgnoreCase);
      text = Regex.Replace(text, @"</ol>", @"\par", RegexOptions.IgnoreCase);
      text = Regex.Replace(text, @"<li>(.*?)</li>", @"\bullet  $1\par", RegexOptions.Singleline);

      // Code blocks
      text = Regex.Replace(text, @"<pre><code>(.*?)</code></pre>", @"{\f1 $1}\par\par", RegexOptions.Singleline);

      // Blockquotes
      text = Regex.Replace(text, @"<blockquote>(.*?)</blockquote>", @"{\li720 $1\li0}\par", RegexOptions.Singleline);

      // Remove any remaining HTML tags
      text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.Singleline);

      // Now escape RTF special characters in the remaining text
      text = text.Replace(@"\", @"\\");
      text = text.Replace("{", @"\{");
      text = text.Replace("}", @"\}");

      // Clean up whitespace
      text = Regex.Replace(text, @"(\par\s*){3,}", @"\par\par");
      text = text.Trim();

      rtf.Append(text);
      rtf.Append(@"}");

      return rtf.ToString();
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException($"Failed to convert HTML to RTF: {ex.Message}", ex);
    }
  }

  private string EscapeRtf(string text)
  {
    if (string.IsNullOrEmpty(text))
      return text;

    // Escape RTF special characters
    text = text.Replace(@"\", @"\\");
    text = text.Replace("{", @"\{");
    text = text.Replace("}", @"\}");
    return text;
  }
}
