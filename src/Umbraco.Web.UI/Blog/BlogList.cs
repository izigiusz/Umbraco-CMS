using System.Globalization;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Helpers for the blog list template. Kept out of the Razor file because a local
/// function inside a <c>@{ }</c> block is rejected by the Razor parser.
/// </summary>
public static class BlogList
{
    /// <summary>
    /// Reads a 1-based page index and clamps it to the available pages.
    /// </summary>
    public static int ParsePage(string? raw, int totalPages)
    {
        if (totalPages < 1)
        {
            totalPages = 1;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested) is false || requested < 1)
        {
            return 1;
        }

        return Math.Min(requested, totalPages);
    }

    /// <summary>
    /// Publish date used for newest-first ordering. Falls back to the create date.
    /// </summary>
    public static DateTime PublishDate(IPublishedContent? post)
    {
        if (post is null)
        {
            return DateTime.MinValue;
        }

        try
        {
            return post.Value<DateTime?>(BlogAliases.PublishDate) ?? post.CreateDate;
        }
        catch (Exception)
        {
            return post.CreateDate;
        }
    }

    /// <summary>
    /// Returns whether the post carries the requested tag. A bad property value does not throw.
    /// </summary>
    public static bool HasTag(IPublishedContent? post, string? tagFilter)
    {
        if (post is null || string.IsNullOrWhiteSpace(tagFilter))
        {
            return false;
        }

        try
        {
            IEnumerable<string>? tags = post.Value<IEnumerable<string>>(BlogAliases.Tags);
            if (tags is null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                if (string.Equals(tag, tagFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Builds a list URL, omitting the page query on the first page.
    /// </summary>
    public static string PageUrl(string? blogUrl, string? tag, int page)
    {
        var path = string.IsNullOrWhiteSpace(blogUrl) ? "/blog/" : blogUrl;
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(tag) is false)
        {
            parts.Add("tag=" + Uri.EscapeDataString(tag));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }
}
