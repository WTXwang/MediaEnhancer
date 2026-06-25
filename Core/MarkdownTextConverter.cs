using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace MediaEnhancer.Core;

/// <summary>
/// Markdown 文本 → FlowDocument 转换器（支持文本选择/复制）。
/// 用于 AI 对话消息渲染
/// </summary>
public class MarkdownTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        var text = value as string ?? "";
        var doc = new FlowDocument
        {
            FontSize = 14,
            FontFamily = new FontFamily("Microsoft YaHei"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
            PagePadding = new Thickness(0)
        };
        RenderMarkdown(doc, text);
        return new FlowDocumentScrollViewer
        {
            Document = doc,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();

    private static void RenderMarkdown(FlowDocument doc, string text)
    {
        var lines = text.Split('\n');
        bool inCodeBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                AddParagraph(doc, line, Colors.Green, isCode: true);
                continue;
            }

            var h = Regex.Match(line, @"^#{1,4}\s+(.+)$");
            if (h.Success)
            {
                var level = h.Value.Split(' ')[0].Length;
                var size = level switch { 1 => 20.0, 2 => 16.0, _ => 14.5 };
                AddParagraph(doc, h.Groups[1].Value, Colors.Black, size, true);
                continue;
            }

            var li = Regex.Match(line, @"^[\-\*]\s+(.+)$");
            if (li.Success)
            {
                var p = AddParagraph(doc, "  •  ", Colors.Gray);
                ParseInline(p, li.Groups[1].Value);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                AddParagraph(doc, "", Colors.Black, 6);
                continue;
            }

            var bp = AddParagraph(doc, null, Colors.Black);
            ParseInline(bp, line);
        }
    }

    private static void ParseInline(Paragraph p, string text)
    {
        var pattern = new Regex(@"\*\*(.+?)\*\*");
        int pos = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > pos) p.Inlines.Add(NewRun(text[pos..m.Index]));
            p.Inlines.Add(NewRun(m.Groups[1].Value, isBold: true));
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) p.Inlines.Add(NewRun(text[pos..]));
    }

    private static Paragraph AddParagraph(FlowDocument doc, string? t,
        Color? color = null, double? size = null, bool isBold = false,
        bool isCode = false, double? marginBottom = null)
    {
        var p = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, marginBottom ?? 4),
            FontSize = size ?? 14,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
        };
        if (isCode) p.FontFamily = new FontFamily("Consolas");
        if (t != null)
            p.Inlines.Add(NewRun(t, color, size, isBold, isCode));
        doc.Blocks.Add(p);
        return p;
    }

    private static Run NewRun(string text, Color? color = null,
        double? size = null, bool isBold = false, bool isCode = false)
    {
        var run = new Run(text)
        {
            Foreground = new SolidColorBrush(color ?? Colors.Black),
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontSize = size ?? 14
        };
        if (isCode) run.FontFamily = new FontFamily("Consolas");
        return run;
    }
}
