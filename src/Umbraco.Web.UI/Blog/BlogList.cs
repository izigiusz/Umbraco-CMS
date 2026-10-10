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
    public static string PageUrl(string? blogUrl, string? tag, int page, string? category = null)
    {
        var path = string.IsNullOrWhiteSpace(blogUrl) ? "/blog/" : blogUrl;
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(tag) is false)
        {
            parts.Add("tag=" + Uri.EscapeDataString(tag));
        }

        if (string.IsNullOrWhiteSpace(category) is false)
        {
            parts.Add("category=" + Uri.EscapeDataString(category));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }

    /// <summary>
    /// Published blog posts picked on Home. Unpublished and trashed items are omitted.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> FeaturedPosts(IPublishedContent? home)
    {
        var result = new List<IPublishedContent>();
        if (home is null || home.HasProperty(BlogAliases.FeaturedTopics) is false)
        {
            return result;
        }

        try
        {
            IEnumerable<IPublishedContent>? many = home.Value<IEnumerable<IPublishedContent>>(BlogAliases.FeaturedTopics);
            if (many is not null)
            {
                foreach (IPublishedContent? item in many)
                {
                    AddPost(result, item);
                }

                return result;
            }

            AddPost(result, home.Value<IPublishedContent>(BlogAliases.FeaturedTopics));
        }
        catch (Exception)
        {
            return result;
        }

        return result;
    }

    /// <summary>
    /// Name of the picked category, or the first tag when nothing is picked.
    /// </summary>
    public static string? CategoryLabel(IPublishedContent? post)
    {
        CategoryReference? picked = PickedCategoryReference(post);
        if (picked is not null)
        {
            return picked.Name;
        }

        return FirstTag(post);
    }

    /// <summary>
    /// Published category document picked on the post. Unpublished and trashed items are omitted.
    /// </summary>
    public static IPublishedContent? PickedCategory(IPublishedContent? post)
    {
        if (post is null || post.HasProperty(BlogAliases.Category) is false)
        {
            return null;
        }

        try
        {
            IPublishedContent? single = post.Value<IPublishedContent>(BlogAliases.Category);
            if (IsCategory(single))
            {
                return single;
            }

            IEnumerable<IPublishedContent>? many = post.Value<IEnumerable<IPublishedContent>>(BlogAliases.Category);
            if (many is null)
            {
                return null;
            }

            foreach (IPublishedContent item in many)
            {
                if (IsCategory(item))
                {
                    return item;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Name and slug of the picked category, when both are present.
    /// </summary>
    public static CategoryReference? PickedCategoryReference(IPublishedContent? post)
    {
        try
        {
            IPublishedContent? category = PickedCategory(post);
            if (category is null)
            {
                return null;
            }

            var name = category.Value<string>(BlogAliases.CategoryName);
            var slug = category.Value<string>(BlogAliases.Slug);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return new CategoryReference(name.Trim(), slug.Trim());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns whether the post's picked category uses this slug.
    /// </summary>
    public static bool MatchesCategory(IPublishedContent? post, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        CategoryReference? picked = PickedCategoryReference(post);
        return picked is not null && string.Equals(picked.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Display name of a category with this slug, looked up under the categories container.
    /// </summary>
    public static string? CategoryTitle(IPublishedContent? anyPage, string? slug)
    {
        if (anyPage is null || string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        try
        {
            IEnumerable<IPublishedContent>? containers = anyPage.Root()?.ChildrenOfType(BlogAliases.Categories);
            IPublishedContent? container = containers?.FirstOrDefault();
            if (container is null)
            {
                return null;
            }

            foreach (IPublishedContent item in container.ChildrenOfType(BlogAliases.Category) ?? [])
            {
                var itemSlug = item.Value<string>(BlogAliases.Slug);
                if (string.Equals(itemSlug?.Trim(), slug.Trim(), StringComparison.OrdinalIgnoreCase) is false)
                {
                    continue;
                }

                var name = item.Value<string>(BlogAliases.CategoryName);
                return string.IsNullOrWhiteSpace(name) ? item.Name : name.Trim();
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Short lead from the summary property.
    /// </summary>
    public static string? Summary(IPublishedContent? post)
    {
        if (post is null || post.HasProperty(BlogAliases.Summary) is false)
        {
            return null;
        }

        try
        {
            var summary = post.Value<string>(BlogAliases.Summary);
            return string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? FirstTag(IPublishedContent? post)
    {
        if (post is null || post.HasProperty(BlogAliases.Tags) is false)
        {
            return null;
        }

        try
        {
            IEnumerable<string>? tags = post.Value<IEnumerable<string>>(BlogAliases.Tags);
            if (tags is null)
            {
                return null;
            }

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag) is false)
                {
                    return tag.Trim();
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static bool IsCategory(IPublishedContent? item)
        => item is not null && item.ContentType.Alias.Equals(BlogAliases.Category, StringComparison.OrdinalIgnoreCase);

    private static void AddPost(List<IPublishedContent> result, IPublishedContent? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.ContentType.Alias.Equals(BlogAliases.Post, StringComparison.OrdinalIgnoreCase) is false)
        {
            return;
        }

        result.Add(item);
    }

    /// <summary>
    /// Name and slug of a picked category.
    /// </summary>
    public sealed record CategoryReference(string Name, string Slug);
}
