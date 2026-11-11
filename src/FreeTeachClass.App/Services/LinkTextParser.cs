using System.Text.RegularExpressions;

namespace FreeTeachClass.App.Services;

/// <summary>A piece of text: plain text (<see cref="Url"/> is null) or a link.</summary>
public readonly record struct TextSegment(string Text, string? Url)
{
    public bool IsLink => Url is not null;
}

/// <summary>
/// Splits plain text into plain and clickable parts. Google Classroom keeps
/// descriptions as plain text, so links are just words there — this helper
/// finds them without changing a single character of the original text.
/// </summary>
public static class LinkTextParser
{
    private static readonly Regex LinkRegex = new(
        @"(?:https?://|www\.)[^\s<>""'\]}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string TrailingPunctuation = ".,;:!?\"'»”";

    /// <summary>
    /// Returns the text split into segments; every segment with a non-null
    /// <see cref="TextSegment.Url"/> is meant to be rendered as a hyperlink.
    /// </summary>
    public static IReadOnlyList<TextSegment> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TextSegment>();
        }

        var segments = new List<TextSegment>();
        var position = 0;

        foreach (Match match in LinkRegex.Matches(text))
        {
            var url = TrimTrailingPunctuation(match.Value);
            if (url.Length == 0)
            {
                continue;
            }

            if (match.Index > position)
            {
                segments.Add(new TextSegment(text[position..match.Index], null));
            }

            segments.Add(new TextSegment(url, ToAbsoluteUrl(url)));
            position = match.Index + url.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new TextSegment(text[position..], null));
        }

        return segments;
    }

    /// <summary>
    /// Removes sentence punctuation that ended up inside the matched link
    /// ("see https://example.com/a." → "https://example.com/a").
    /// </summary>
    private static string TrimTrailingPunctuation(string value)
    {
        var end = value.Length;

        while (end > 0)
        {
            var last = value[end - 1];

            if (TrailingPunctuation.Contains(last))
            {
                end--;
                continue;
            }

            if (last == ')')
            {
                var head = value.AsSpan(0, end);
                if (head.Count(')') > head.Count('('))
                {
                    end--;
                    continue;
                }
            }

            break;
        }

        return value[..end];
    }

    private static string ToAbsoluteUrl(string value) =>
        value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? "https://" + value
            : value;
}
