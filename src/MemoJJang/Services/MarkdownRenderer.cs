using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using FlowList = System.Windows.Documents.List;
using FlowTable = System.Windows.Documents.Table;
using FlowTableCell = System.Windows.Documents.TableCell;
using FlowTableRow = System.Windows.Documents.TableRow;

namespace MemoJJang.Services;

/// <summary>
/// Markdown 을 WPF <see cref="FlowDocument"/> 로 변환한다.
///
/// 파싱은 Markdig 에 맡기고 시각화는 직접 하기 때문에
/// 색상을 모두 DynamicResource 로 묶을 수 있고, 테마를 바꾸면 미리 보기도 함께 바뀐다.
/// 외부 이미지는 내려받지 않는다(문서를 여는 것만으로 네트워크 요청이 나가지 않도록).
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private const string BodyFont = "Segoe UI, Malgun Gothic";
    private const string CodeFont = "Consolas, D2Coding, Malgun Gothic";

    /// <summary>Markdown 미리 보기를 지원하는 확장자.</summary>
    private static readonly string[] MarkdownExtensions =
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".mdwn"
    };

    public static bool IsMarkdownFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return MarkdownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static FlowDocument Render(string markdown, string? sourcePath, double fontSize)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily(BodyFont),
            FontSize = fontSize,
            PagePadding = new Thickness(26, 22, 26, 40),
            LineHeight = fontSize * 1.65,
            IsOptimalParagraphEnabled = false,
            TextAlignment = TextAlignment.Left
        };

        document.SetResourceReference(FlowDocument.BackgroundProperty, "App.Editor.Background");
        document.SetResourceReference(FlowDocument.ForegroundProperty, "App.Editor.Foreground");

        var directory = GetDirectory(sourcePath);

        try
        {
            var parsed = Markdown.Parse(markdown ?? string.Empty, Pipeline);

            foreach (var block in parsed)
            {
                foreach (var rendered in ConvertBlock(block, directory, fontSize))
                {
                    document.Blocks.Add(rendered);
                }
            }
        }
        catch (Exception ex)
        {
            // 미리 보기가 실패해도 편집 자체는 계속되어야 한다.
            var error = new Paragraph(new Run($"미리 보기를 만들지 못했습니다: {ex.Message}"));
            error.SetResourceReference(TextElement.ForegroundProperty, "App.Warning");
            document.Blocks.Add(error);
        }

        return document;
    }

    // ==================================================================
    //  블록
    // ==================================================================

    private static IEnumerable<System.Windows.Documents.Block> ConvertBlock(
        MdBlock block, string? directory, double fontSize)
    {
        switch (block)
        {
            case HeadingBlock heading:
                yield return CreateHeading(heading, directory, fontSize);
                break;

            case ParagraphBlock paragraph:
            {
                var result = new Paragraph { Margin = new Thickness(0, 0, 0, fontSize * 0.75) };
                AppendInlines(result.Inlines, paragraph.Inline, directory, fontSize);
                yield return result;
                break;
            }

            case ListBlock list:
                yield return CreateList(list, directory, fontSize);
                break;

            case QuoteBlock quote:
                yield return CreateQuote(quote, directory, fontSize);
                break;

            case FencedCodeBlock fenced:
                yield return CreateCode(GetLinesText(fenced), fenced.Info, fontSize);
                break;

            case CodeBlock code:
                yield return CreateCode(GetLinesText(code), null, fontSize);
                break;

            case ThematicBreakBlock:
                yield return CreateRule(fontSize);
                break;

            case MdTable table:
                yield return CreateTable(table, directory, fontSize);
                break;

            case HtmlBlock html:
                yield return CreateRawHtml(GetLinesText(html), fontSize);
                break;

            case LinkReferenceDefinitionGroup:
                // 참조 정의는 화면에 나타나지 않는다.
                break;

            case ContainerBlock container:
            {
                // 알 수 없는 컨테이너는 자식만 펼쳐서 보여 준다.
                foreach (var child in container)
                {
                    foreach (var rendered in ConvertBlock(child, directory, fontSize))
                    {
                        yield return rendered;
                    }
                }

                break;
            }

            case LeafBlock leaf:
            {
                var text = GetLinesText(leaf);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return new Paragraph(new Run(text));
                }

                break;
            }
        }
    }

    private static System.Windows.Documents.Block CreateHeading(
        HeadingBlock heading, string? directory, double fontSize)
    {
        var scale = heading.Level switch
        {
            1 => 1.90,
            2 => 1.55,
            3 => 1.30,
            4 => 1.12,
            5 => 1.00,
            _ => 0.92
        };

        var paragraph = new Paragraph
        {
            FontSize = fontSize * scale,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, heading.Level <= 2 ? fontSize * 1.5 : fontSize * 1.2, 0, fontSize * 0.5),
            LineHeight = fontSize * scale * 1.35
        };

        if (heading.Level >= 6)
        {
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");
        }

        if (heading.Level <= 2)
        {
            paragraph.BorderThickness = new Thickness(0, 0, 0, 1);
            paragraph.Padding = new Thickness(0, 0, 0, fontSize * 0.35);
            paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "App.Border");
        }

        AppendInlines(paragraph.Inlines, heading.Inline, directory, fontSize);
        return paragraph;
    }

    private static System.Windows.Documents.Block CreateList(
        ListBlock list, string? directory, double fontSize)
    {
        var result = new FlowList
        {
            Margin = new Thickness(fontSize * 0.4, 0, 0, fontSize * 0.75),
            Padding = new Thickness(fontSize * 1.2, 0, 0, 0),
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
        };

        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start) && start > 0)
        {
            result.StartIndex = start;
        }

        foreach (var child in list)
        {
            if (child is not ListItemBlock itemBlock)
            {
                continue;
            }

            var item = new ListItem();

            foreach (var inner in itemBlock)
            {
                foreach (var rendered in ConvertBlock(inner, directory, fontSize))
                {
                    item.Blocks.Add(rendered);
                }
            }

            if (item.Blocks.Count == 0)
            {
                item.Blocks.Add(new Paragraph());
            }

            // 목록 안의 문단은 간격을 줄여 촘촘하게 보이도록 한다.
            foreach (var itemBlockChild in item.Blocks)
            {
                if (itemBlockChild is Paragraph p)
                {
                    p.Margin = new Thickness(0, 0, 0, fontSize * 0.2);
                }
            }

            result.ListItems.Add(item);
        }

        return result;
    }

    private static System.Windows.Documents.Block CreateQuote(
        QuoteBlock quote, string? directory, double fontSize)
    {
        var section = new Section
        {
            Margin = new Thickness(0, 0, 0, fontSize * 0.75),
            Padding = new Thickness(fontSize, fontSize * 0.2, 0, fontSize * 0.2),
            BorderThickness = new Thickness(3, 0, 0, 0)
        };

        section.SetResourceReference(Section.BorderBrushProperty, "App.Accent");
        section.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");

        foreach (var child in quote)
        {
            foreach (var rendered in ConvertBlock(child, directory, fontSize))
            {
                section.Blocks.Add(rendered);
            }
        }

        if (section.Blocks.Count == 0)
        {
            section.Blocks.Add(new Paragraph());
        }

        return section;
    }

    private static System.Windows.Documents.Block CreateCode(string code, string? language, double fontSize)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(CodeFont),
            FontSize = fontSize * 0.92,
            LineHeight = fontSize * 1.4,
            Padding = new Thickness(fontSize * 0.9, fontSize * 0.7, fontSize * 0.9, fontSize * 0.7),
            Margin = new Thickness(0, 0, 0, fontSize * 0.85),
            BorderThickness = new Thickness(1),
            TextAlignment = TextAlignment.Left
        };

        paragraph.SetResourceReference(Paragraph.BackgroundProperty, "App.SurfaceAlt");
        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "App.Border");

        if (!string.IsNullOrWhiteSpace(language))
        {
            var label = new Run(language.Trim() + "\n");
            label.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");
            label.FontSize = fontSize * 0.78;
            paragraph.Inlines.Add(label);
        }

        paragraph.Inlines.Add(new Run(code.TrimEnd('\n', '\r')));
        return paragraph;
    }

    private static System.Windows.Documents.Block CreateRawHtml(string html, double fontSize)
    {
        // HTML 은 실행하지 않고 원문 그대로 보여 준다.
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(CodeFont),
            FontSize = fontSize * 0.86,
            Margin = new Thickness(0, 0, 0, fontSize * 0.75)
        };

        paragraph.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");
        paragraph.Inlines.Add(new Run(html.TrimEnd('\n', '\r')));
        return paragraph;
    }

    private static System.Windows.Documents.Block CreateRule(double fontSize)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, fontSize * 0.6, 0, fontSize * 1.1),
            BorderThickness = new Thickness(0, 0, 0, 1),
            FontSize = 1,
            LineHeight = 1
        };

        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "App.Border");
        return paragraph;
    }

    private static System.Windows.Documents.Block CreateTable(
        MdTable table, string? directory, double fontSize)
    {
        var result = new FlowTable
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, fontSize * 0.9)
        };

        var columnCount = 0;
        foreach (var row in table)
        {
            if (row is MdTableRow tableRow)
            {
                columnCount = Math.Max(columnCount, tableRow.Count);
            }
        }

        for (var i = 0; i < Math.Max(1, columnCount); i++)
        {
            result.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        result.RowGroups.Add(group);

        foreach (var row in table)
        {
            if (row is not MdTableRow tableRow)
            {
                continue;
            }

            var flowRow = new FlowTableRow();

            if (tableRow.IsHeader)
            {
                flowRow.FontWeight = FontWeights.SemiBold;
                flowRow.SetResourceReference(TextElement.BackgroundProperty, "App.SurfaceAlt");
            }

            foreach (var cell in tableRow)
            {
                var flowCell = new FlowTableCell
                {
                    Padding = new Thickness(fontSize * 0.7, fontSize * 0.4, fontSize * 0.7, fontSize * 0.4),
                    BorderThickness = new Thickness(1)
                };

                flowCell.SetResourceReference(FlowTableCell.BorderBrushProperty, "App.Border");

                if (cell is MdTableCell tableCell)
                {
                    foreach (var child in tableCell)
                    {
                        foreach (var rendered in ConvertBlock(child, directory, fontSize))
                        {
                            if (rendered is Paragraph p)
                            {
                                p.Margin = new Thickness(0);
                            }

                            flowCell.Blocks.Add(rendered);
                        }
                    }
                }

                if (flowCell.Blocks.Count == 0)
                {
                    flowCell.Blocks.Add(new Paragraph());
                }

                flowRow.Cells.Add(flowCell);
            }

            group.Rows.Add(flowRow);
        }

        return result;
    }

    // ==================================================================
    //  인라인
    // ==================================================================

    private static void AppendInlines(
        InlineCollection target, ContainerInline? container, string? directory, double fontSize)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            var converted = ConvertInline(inline, directory, fontSize);
            if (converted is not null)
            {
                target.Add(converted);
            }
        }
    }

    private static System.Windows.Documents.Inline? ConvertInline(
        MdInline inline, string? directory, double fontSize)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());

            case EmphasisInline emphasis:
            {
                Span span = emphasis.DelimiterChar switch
                {
                    '~' => new Span { TextDecorations = TextDecorations.Strikethrough },
                    _ => emphasis.DelimiterCount >= 2 ? new Bold() : new Italic()
                };

                AppendInlines(span.Inlines, emphasis, directory, fontSize);
                return span;
            }

            case CodeInline code:
            {
                var run = new Run(code.Content)
                {
                    FontFamily = new FontFamily(CodeFont),
                    FontSize = fontSize * 0.92
                };

                run.SetResourceReference(TextElement.BackgroundProperty, "App.SurfaceAlt");
                run.SetResourceReference(TextElement.ForegroundProperty, "App.Accent");
                return run;
            }

            case TaskList task:
                return new Run(task.Checked ? "☑ " : "☐ ");

            case LinkInline link when link.IsImage:
                return CreateImage(link, directory, fontSize);

            case LinkInline link:
            {
                var hyperlink = new Hyperlink();
                hyperlink.SetResourceReference(TextElement.ForegroundProperty, "App.Accent");

                AppendInlines(hyperlink.Inlines, link, directory, fontSize);

                if (hyperlink.Inlines.Count == 0)
                {
                    hyperlink.Inlines.Add(new Run(link.Url ?? string.Empty));
                }

                if (TryCreateNavigableUri(link.Url, out var uri))
                {
                    hyperlink.NavigateUri = uri;
                    hyperlink.ToolTip = link.Url;
                }

                return hyperlink;
            }

            case AutolinkInline autolink:
            {
                var hyperlink = new Hyperlink(new Run(autolink.Url));
                hyperlink.SetResourceReference(TextElement.ForegroundProperty, "App.Accent");

                if (TryCreateNavigableUri(autolink.Url, out var uri))
                {
                    hyperlink.NavigateUri = uri;
                }

                return hyperlink;
            }

            case LineBreakInline lineBreak:
                return lineBreak.IsHard ? new LineBreak() : new Run(" ");

            case HtmlEntityInline entity:
                return new Run(entity.Transcoded.ToString());

            case HtmlInline html:
            {
                var run = new Run(html.Tag);
                run.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");
                return run;
            }

            case ContainerInline container:
            {
                var span = new Span();
                AppendInlines(span.Inlines, container, directory, fontSize);
                return span;
            }

            default:
            {
                var text = inline.ToString();
                return string.IsNullOrEmpty(text) ? null : new Run(text);
            }
        }
    }

    /// <summary>
    /// 이미지. 문서와 같은 디스크에 있는 파일만 표시하고,
    /// 원격 주소는 내려받지 않고 대체 텍스트로 대신한다.
    /// </summary>
    private static System.Windows.Documents.Inline CreateImage(
        LinkInline link, string? directory, double fontSize)
    {
        var alt = GetPlainText(link);
        var label = string.IsNullOrWhiteSpace(alt) ? link.Url ?? "이미지" : alt;

        var localPath = ResolveLocalPath(link.Url, directory);

        if (localPath is not null)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var image = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    MaxWidth = bitmap.PixelWidth,
                    ToolTip = localPath
                };

                return new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Bottom };
            }
            catch
            {
                // 못 읽으면 아래의 대체 텍스트로 넘어간다.
            }
        }

        var fallback = new Run($"🖼 {label}")
        {
            FontStyle = FontStyles.Italic,
            FontSize = fontSize * 0.9
        };

        fallback.SetResourceReference(TextElement.ForegroundProperty, "App.TextMuted");
        return fallback;
    }

    // ==================================================================
    //  도우미
    // ==================================================================

    private static string GetLinesText(LeafBlock block)
    {
        var builder = new StringBuilder();
        var lines = block.Lines;

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines.Lines[i].Slice.ToString());
        }

        return builder.ToString();
    }

    private static string GetPlainText(ContainerInline container)
    {
        var builder = new StringBuilder();
        Collect(container);
        return builder.ToString();

        void Collect(ContainerInline node)
        {
            foreach (var child in node)
            {
                switch (child)
                {
                    case LiteralInline literal:
                        builder.Append(literal.Content.ToString());
                        break;
                    case CodeInline code:
                        builder.Append(code.Content);
                        break;
                    case ContainerInline inner:
                        Collect(inner);
                        break;
                }
            }
        }
    }

    private static string? GetDirectory(string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(sourcePath));
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveLocalPath(string? url, string? directory)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // 원격 주소는 다루지 않는다.
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var cleaned = Uri.UnescapeDataString(url.Split('#')[0].Split('?')[0]);

            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(url, UriKind.Absolute, out var fileUri))
            {
                cleaned = fileUri.LocalPath;
            }

            var full = Path.IsPathRooted(cleaned)
                ? cleaned
                : directory is null ? null : Path.GetFullPath(Path.Combine(directory, cleaned));

            return full is not null && File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>브라우저로 열어도 되는 주소인지 확인한다.</summary>
    private static bool TryCreateNavigableUri(string? url, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp &&
            parsed.Scheme != Uri.UriSchemeHttps &&
            parsed.Scheme != Uri.UriSchemeMailto)
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
