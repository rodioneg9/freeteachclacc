using FreeTeachClass.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;

namespace FreeTeachClass.App.Controls;

/// <summary>
/// Text that keeps the usual look but turns every http(s) link into a
/// clickable hyperlink (opened in the default browser). Used for assignment
/// titles, descriptions and attachment names.
/// </summary>
public sealed partial class LinkTextBlock : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(LinkTextBlock),
        new PropertyMetadata(string.Empty, (dependencyObject, _) => ((LinkTextBlock)dependencyObject).Rebuild()));

    private readonly RichTextBlock _richText;

    public LinkTextBlock()
    {
        _richText = new RichTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        // The inner rich text follows the typography set on the control itself.
        _richText.SetBinding(RichTextBlock.ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        _richText.SetBinding(RichTextBlock.FontSizeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(FontSize)) });
        _richText.SetBinding(RichTextBlock.FontFamilyProperty, new Binding { Source = this, Path = new PropertyPath(nameof(FontFamily)) });
        _richText.SetBinding(RichTextBlock.FontWeightProperty, new Binding { Source = this, Path = new PropertyPath(nameof(FontWeight)) });
        _richText.SetBinding(RichTextBlock.FontStyleProperty, new Binding { Source = this, Path = new PropertyPath(nameof(FontStyle)) });

        Content = _richText;
    }

    /// <summary>The text to show; links inside it become clickable.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Rebuild()
    {
        _richText.Blocks.Clear();

        var text = Text;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Every line of the text becomes its own paragraph, so the original
        // line breaks survive.
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new Paragraph();
            foreach (var segment in LinkTextParser.Parse(line))
            {
                if (segment.Text.Length == 0)
                {
                    continue;
                }

                if (segment.IsLink && TryCreateHyperlink(segment, out var hyperlink))
                {
                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run { Text = segment.Text });
                }
            }

            _richText.Blocks.Add(paragraph);
        }
    }

    private static bool TryCreateHyperlink(TextSegment segment, out Hyperlink hyperlink)
    {
        hyperlink = null!;

        if (string.IsNullOrWhiteSpace(segment.Url))
        {
            return false;
        }

        if (!Uri.TryCreate(segment.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return false;
        }

        var link = new Hyperlink { NavigateUri = uri };
        link.Inlines.Add(new Run { Text = segment.Text });
        hyperlink = link;
        return true;
    }
}
