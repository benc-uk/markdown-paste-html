//go:build windows

package main

import (
	"bytes"
	"strings"

	"github.com/yuin/goldmark"
	"github.com/yuin/goldmark/extension"
)

var md goldmark.Markdown

func init() {
	md = goldmark.New(
		goldmark.WithExtensions(
			extension.Table,
			extension.Strikethrough,
			extension.TaskList,
			extension.Linkify,
			extension.DefinitionList,
		),
	)
}

// ConvertToHTML converts markdown text to HTML
func ConvertToHTML(markdown string) (string, error) {
	var buf bytes.Buffer
	if err := md.Convert([]byte(markdown), &buf); err != nil {
		return "", err
	}
	return buf.String(), nil
}

// IsLikelyMarkdown checks if text contains markdown patterns
// Uses the same heuristic indicators as the original C# version
func IsLikelyMarkdown(text string) bool {
	if strings.TrimSpace(text) == "" {
		return false
	}

	indicators := []string{
		"# ", // Headers
		"## ",
		"### ",
		"* ", // Lists
		"- ",
		"+ ",
		"1. ", // Numbered lists
		"**",  // Bold
		"__",
		"*", // Italic
		"_",
		"[",   // Links
		"```", // Code blocks
		"`",   // Inline code
		">",   // Blockquotes
		"---", // Horizontal rules
		"***",
		"|", // Tables
	}

	for _, indicator := range indicators {
		if strings.Contains(text, indicator) {
			return true
		}
	}

	return false
}
