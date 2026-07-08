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

            richText.Blocks.Clear();
            var html = e.NewValue as string;
            if (string.IsNullOrEmpty(html)) return;

            try
            {
                ParseHtml(richText, html);
            }
            catch
            {
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
            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            bool isBlockQuote = false;
            
            var listStack = new Stack<bool>();
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
                    var text = html.Substring(i);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (currentParagraph == null)
                        {
                            currentParagraph = CreateParagraph(isBlockQuote);
                            richText.Blocks.Add(currentParagraph);
                        }
                        AddText(currentParagraph, text, isBold, isItalic, isUnderline, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    }
                    break;
                }

                if (nextTagStart > i)
                {
                    var text = html.Substring(i, nextTagStart - i);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (currentParagraph == null)
                        {
                            currentParagraph = CreateParagraph(isBlockQuote);
                            richText.Blocks.Add(currentParagraph);
                        }
                        AddText(currentParagraph, text, isBold, isItalic, isUnderline, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    }
                }

                int nextTagEnd = html.IndexOf('>', nextTagStart);
                if (nextTagEnd == -1)
                {
                    var text = html.Substring(nextTagStart);
                    if (currentParagraph == null)
                    {
                        currentParagraph = CreateParagraph(isBlockQuote);
                        richText.Blocks.Add(currentParagraph);
                    }
                    AddText(currentParagraph, text, isBold, isItalic, isUnderline, isListItem && !hasListPrefixBeenAdded, ref hasListPrefixBeenAdded, listStack, listIndexStack);
                    break;
                }

                string tagString = html.Substring(nextTagStart + 1, nextTagEnd - nextTagStart - 1).Trim();
                string tagLower = tagString.ToLower();

                if (tagLower.StartsWith("/"))
                {
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
                        currentParagraph = null;
                    }
                    else if (closeTagName == "p")
                    {
                        currentParagraph = null;
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
                }
                else
                {
                    bool isSelfClosing = tagLower.EndsWith("/");
                    string tagName = isSelfClosing ? tagLower.Substring(0, tagLower.Length - 1).Trim() : tagLower;
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
                        currentParagraph = null;
                    }
                    else if (tagName == "blockquote")
                    {
                        isBlockQuote = true;
                        currentParagraph = null;
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

        private static void AddText(Paragraph paragraph, string text, bool isBold, bool isItalic, bool isUnderline, bool addListPrefix, ref bool hasListPrefixBeenAdded, Stack<bool> listStack, Stack<int> listIndexStack)
        {
            var decoded = System.Net.WebUtility.HtmlDecode(text);
            if (string.IsNullOrEmpty(decoded)) return;

            if (decoded == "\n" || decoded == "\r\n")
            {
                paragraph.Inlines.Add(new LineBreak());
                return;
            }

            Inline targetInline = new Run { Text = decoded };

            if (isUnderline)
            {
                var u = new Underline();
                u.Inlines.Add(targetInline);
                targetInline = u;
            }

            if (isItalic)
            {
                var it = new Italic();
                it.Inlines.Add(targetInline);
                targetInline = it;
            }

            if (isBold)
            {
                var b = new Bold();
                b.Inlines.Add(targetInline);
                targetInline = b;
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

            paragraph.Inlines.Add(targetInline);
        }
    }
}
