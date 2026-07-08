using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using Windows.UI.Xaml.Media;

namespace wasteof.phone.Helpers
{
    public static class HtmlHelper
    {
        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.RegisterAttached("Html", typeof(string), typeof(HtmlHelper),
                new PropertyMetadata(null, OnHtmlChanged));

        public static string GetHtml(DependencyObject obj)
        {
            return (string)obj.GetValue(HtmlProperty);
        }

        public static void SetHtml(DependencyObject obj, string value)
        {
            obj.SetValue(HtmlProperty, value);
        }

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var richText = d as RichTextBlock;
            if (richText == null) return;

            // Automatically disable text selection to prevent blocking list item tap events
            richText.IsTextSelectionEnabled = false;

            richText.Blocks.Clear();
            var html = e.NewValue as string;
            if (string.IsNullOrEmpty(html)) return;

            try
            {
                ParseHtml(richText, html);
            }
            catch
            {
                // Fallback to plain text paragraph in case of any parsing exception
                var p = new Paragraph();
                p.Inlines.Add(new Run { Text = StripHtml(html) });
                richText.Blocks.Add(p);
            }
        }

        private static string StripHtml(string html)
        {
            var text = html.Replace("<p>", "").Replace("</p>", "\n").Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
            while (text.Contains("<"))
            {
                int start = text.IndexOf("<");
                int end = text.IndexOf(">");
                if (end > start)
                    text = text.Remove(start, end - start + 1);
                else
                    break;
            }
            return text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ").Trim();
        }

        private static void ParseHtml(RichTextBlock richText, string html)
        {
            // Formatting stack states
            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            bool isBlockQuote = false;
            string hyperlinkUrl = null;
            
            // List states
            var listStack = new Stack<bool>(); // true = ordered, false = unordered
            var listIndexStack = new Stack<int>();
            bool isListItem = false;
            bool hasListPrefixBeenAdded = false;

            Paragraph currentParagraph = null;

            int i = 0;
            int len = html.Length;

            while (i < len)
            {
                int nextTagStart = html.IndexOf('<', i);
                if (nextTagStart == -1)
                {
                    // Add remaining text
                    var text = html.Substring(i);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (currentParagraph == null)
                        {
                            currentParagraph = CreateParagraph(isBlockQuote);
                            richText.Blocks.Add(currentParagraph);
                        }
                        AddText(currentParagraph, text, isBold, isItalic, isUnderline, hyperlinkUrl, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    }
                    break;
                }

                if (nextTagStart > i)
                {
                    // Add text before the tag
                    var text = html.Substring(i, nextTagStart - i);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (currentParagraph == null)
                        {
                            currentParagraph = CreateParagraph(isBlockQuote);
                            richText.Blocks.Add(currentParagraph);
                        }
                        AddText(currentParagraph, text, isBold, isItalic, isUnderline, hyperlinkUrl, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    }
                }

                int nextTagEnd = html.IndexOf('>', nextTagStart);
                if (nextTagEnd == -1)
                {
                    // Malformed tag, treat rest as text
                    var text = html.Substring(nextTagStart);
                    if (currentParagraph == null)
                    {
                        currentParagraph = CreateParagraph(isBlockQuote);
                        richText.Blocks.Add(currentParagraph);
                    }
                    AddText(currentParagraph, text, isBold, isItalic, isUnderline, hyperlinkUrl, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    break;
                }

                // Parse the tag
                string tagString = html.Substring(nextTagStart + 1, nextTagEnd - nextTagStart - 1).Trim();
                string tagLower = tagString.ToLower();

                if (tagLower.StartsWith("/"))
                {
                    // Close tag
                    string closeTagName = tagLower.Substring(1).Trim();
                    if (closeTagName == "strong" || closeTagName == "b")
                    {
                        isBold = false;
                    }
                    else if (closeTagName == "em" || closeTagName == "i")
                    {
                        isItalic = false;
                    }
                    else if (closeTagName == "u")
                    {
                        isUnderline = false;
                    }
                    else if (closeTagName == "blockquote")
                    {
                        isBlockQuote = false;
                        currentParagraph = null; // force new paragraph outside blockquote
                    }
                    else if (closeTagName == "p")
                    {
                        currentParagraph = null; // force new paragraph
                    }
                    else if (closeTagName == "ul" || closeTagName == "ol")
                    {
                        if (listStack.Count > 0) listStack.Pop();
                        if (listIndexStack.Count > 0) listIndexStack.Pop();
                        currentParagraph = null;
                    }
                    else if (closeTagName == "li")
                    {
                        isListItem = false;
                        hasListPrefixBeenAdded = false;
                        currentParagraph = null;
                    }
                    else if (closeTagName == "a")
                    {
                        hyperlinkUrl = null;
                    }
                }
                else
                {
                    // Open tag or self-closing
                    bool isSelfClosing = tagLower.EndsWith("/");
                    string tagName = isSelfClosing ? tagLower.Substring(0, tagLower.Length - 1).Trim() : tagLower;
                    // Extract tag name if it contains attributes
                    int spaceIndex = tagName.IndexOf(' ');
                    if (spaceIndex != -1)
                    {
                        tagName = tagName.Substring(0, spaceIndex).Trim();
                    }

                    if (tagName == "br")
                    {
                        if (currentParagraph == null)
                        {
                            currentParagraph = CreateParagraph(isBlockQuote);
                            richText.Blocks.Add(currentParagraph);
                        }
                        currentParagraph.Inlines.Add(new LineBreak());
                    }
                    else if (tagName == "strong" || tagName == "b")
                    {
                        isBold = true;
                    }
                    else if (tagName == "em" || tagName == "i")
                    {
                        isItalic = true;
                    }
                    else if (tagName == "u")
                    {
                        isUnderline = true;
                    }
                    else if (tagName == "p")
                    {
                        currentParagraph = null; // force new paragraph
                    }
                    else if (tagName == "blockquote")
                    {
                        isBlockQuote = true;
                        currentParagraph = null; // force new paragraph inside blockquote
                    }
                    else if (tagName == "ul")
                    {
                        listStack.Push(false);
                        listIndexStack.Push(1);
                        currentParagraph = null;
                    }
                    else if (tagName == "ol")
                    {
                        listStack.Push(true);
                        listIndexStack.Push(1);
                        currentParagraph = null;
                    }
                    else if (tagName == "li")
                    {
                        isListItem = true;
                        hasListPrefixBeenAdded = false;
                        currentParagraph = null;
                    }
                    else if (tagName == "a")
                    {
                        // Extract href
                        int hrefIndex = tagLower.IndexOf("href=");
                        if (hrefIndex != -1)
                        {
                            int valStart = hrefIndex + 5;
                            if (valStart < tagLower.Length)
                            {
                                char quoteChar = tagLower[valStart];
                                if (quoteChar == '"' || quoteChar == '\'')
                                {
                                    int valEnd = tagLower.IndexOf(quoteChar, valStart + 1);
                                    if (valEnd != -1)
                                    {
                                        // Use original tagString case for url
                                        hyperlinkUrl = tagString.Substring(valStart + 1, valEnd - valStart - 1);
                                    }
                                }
                                else
                                {
                                    int spaceIdx = tagLower.IndexOf(' ', valStart);
                                    if (spaceIdx != -1)
                                        hyperlinkUrl = tagString.Substring(valStart, spaceIdx - valStart);
                                    else
                                        hyperlinkUrl = tagString.Substring(valStart);
                                }
                            }
                        }
                    }
                }

                i = nextTagEnd + 1;
            }
        }

        private static Paragraph CreateParagraph(bool isBlockQuote)
        {
            var p = new Paragraph();
            p.Margin = new Thickness(isBlockQuote ? 24 : 0, 0, 0, 12);
            return p;
        }

        private static void AddText(Paragraph paragraph, string text, bool isBold, bool isItalic, bool isUnderline, string hyperlinkUrl, bool addListPrefix, ref bool hasListPrefixBeenAdded, Stack<bool> listStack, Stack<int> listIndexStack)
        {
            var decoded = System.Net.WebUtility.HtmlDecode(text);
            if (string.IsNullOrEmpty(decoded)) return;

            // Trim leading newlines if they are empty
            if (decoded == "\n" || decoded == "\r\n")
            {
                paragraph.Inlines.Add(new LineBreak());
                return;
            }

            if (addListPrefix && !hasListPrefixBeenAdded)
            {
                hasListPrefixBeenAdded = true;
                string prefix = "• ";
                if (listStack.Count > 0 && listStack.Peek())
                {
                    int index = listIndexStack.Count > 0 ? listIndexStack.Pop() : 1;
                    prefix = $"{index}. ";
                    listIndexStack.Push(index + 1);
                }

                var prefixRun = new Run { Text = prefix, FontWeight = FontWeights.Bold };
                paragraph.Inlines.Add(prefixRun);
            }

            // If we are already inside an HTML <a> tag, we don't need to auto-link
            if (!string.IsNullOrEmpty(hyperlinkUrl))
            {
                Inline targetInline = new Run { Text = decoded };
                targetInline = ApplyStyles(targetInline, isBold, isItalic, isUnderline);
                
                var hl = new Hyperlink();
                try
                {
                    string url = hyperlinkUrl.Trim();
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + url;
                    }
                    hl.NavigateUri = new Uri(url);
                }
                catch { }
                hl.Inlines.Add(targetInline);
                paragraph.Inlines.Add(hl);
            }
            else
            {
                // Auto-link plain text URLs!
                var tokens = SplitTokens(decoded);
                foreach (var token in tokens)
                {
                    if (IsUrl(token))
                    {
                        var run = new Run { Text = token };
                        Inline styledRun = ApplyStyles(run, isBold, isItalic, isUnderline);

                        var hl = new Hyperlink();
                        try
                        {
                            string url = token.Trim();
                            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                url = "https://" + url;
                            }
                            hl.NavigateUri = new Uri(url);
                        }
                        catch { }
                        hl.Inlines.Add(styledRun);
                        paragraph.Inlines.Add(hl);
                    }
                    else
                    {
                        var run = new Run { Text = token };
                        Inline styledRun = ApplyStyles(run, isBold, isItalic, isUnderline);
                        paragraph.Inlines.Add(styledRun);
                    }
                }
            }
        }

        private static Inline ApplyStyles(Inline inline, bool isBold, bool isItalic, bool isUnderline)
        {
            Inline target = inline;
            if (isUnderline)
            {
                var u = new Underline();
                u.Inlines.Add(target);
                target = u;
            }
            if (isItalic)
            {
                var it = new Italic();
                it.Inlines.Add(target);
                target = it;
            }
            if (isBold)
            {
                var b = new Bold();
                b.Inlines.Add(target);
                target = b;
            }
            return target;
        }

        private static bool IsUrl(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            string t = token.Trim();
            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (t.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && t.Length > 6 && t.Contains("."))
            {
                return true;
            }
            return false;
        }

        private static List<string> SplitTokens(string text)
        {
            var list = new List<string>();
            int lastIndex = 0;
            int len = text.Length;
            
            for (int i = 0; i < len; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' || c == '<' || c == '>')
                {
                    if (i > lastIndex)
                    {
                        list.Add(text.Substring(lastIndex, i - lastIndex));
                    }
                    list.Add(c.ToString());
                    lastIndex = i + 1;
                }
            }
            
            if (len > lastIndex)
            {
                list.Add(text.Substring(lastIndex, len - lastIndex));
            }
            return list;
        }
    }
}
