using System;
using Markdig;

namespace MarkdownPasteHtml;

public class MarkdownConverter
{
  private readonly MarkdownPipeline _pipeline;

  public MarkdownConverter()
  {
    _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
  }

  public string ConvertToHtml(string markdown)
  {
    if (string.IsNullOrWhiteSpace(markdown))
    {
      throw new ArgumentException("Markdown content is empty", nameof(markdown));
    }

    try
    {
      string html = Markdown.ToHtml(markdown, _pipeline);
      return html;
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException("Failed to convert markdown to HTML", ex);
    }
  }

  public bool IsLikelyMarkdown(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return false;

    // Simple heuristic to detect if text contains markdown patterns
    // This helps avoid converting plain text unnecessarily
    string[] markdownIndicators = new[]
    {
            "# ",      // Headers
            "## ",
            "### ",
            "* ",      // Lists
            "- ",
            "+ ",
            "1. ",     // Numbered lists
            "**",      // Bold
            "__",
            "*",       // Italic (check for word boundaries)
            "_",
            "[",       // Links
            "```",     // Code blocks
            "`",       // Inline code
            ">",       // Blockquotes
            "---",     // Horizontal rules
            "***",
            "|",       // Tables
        };

    foreach (var indicator in markdownIndicators)
    {
      if (text.Contains(indicator))
        return true;
    }

    return false;
  }
}
