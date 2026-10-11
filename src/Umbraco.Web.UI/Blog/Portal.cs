using System.Globalization;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Read-only helpers for the Codation portal templates.
/// A missing or broken property returns a fallback instead of throwing.
/// </summary>
public static class Portal
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private static readonly Dictionary<string, string> AccentBySlug = new(StringComparer.OrdinalIgnoreCase)
    {
        ["agenci-kodowania"] = "#22d3ee",
        ["modele"] = "#38bdf8",
        ["orkiestracja"] = "#2dd4bf",
        ["rag"] = "#67e8f9",
        ["evaluation-i-reliability"] = "#818cf8",
        ["agentic-sdlc"] = "#34d399",
        ["ci-cd-i-pipeliney"] = "#fbbf24",
        ["observability-i-monitoring"] = "#fb7185",
        ["playbooks"] = "#a78bfa",
    };

    /// <summary>
    /// Published pillar pages under Home, in editorial order.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> Pillars(IPublishedContent? home)
    {
        try
        {
            IEnumerable<IPublishedContent>? pages = home?.ChildrenOfType(BlogAliases.Pillar);
            if (pages is null)
            {
                return [];
            }

            return pages.OrderBy(PillarOrder).ThenBy(page => page.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Published categories under the Kategorie container.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> Categories(IPublishedContent? anyPage)
    {
        try
        {
            IPublishedContent? container = Container(anyPage);
            IEnumerable<IPublishedContent>? items = container?.ChildrenOfType(BlogAliases.Category);
            return items?.ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// The categories container, when it is published.
    /// </summary>
    public static IPublishedContent? Container(IPublishedContent? anyPage)
    {
        try
        {
            IEnumerable<IPublishedContent>? containers = anyPage?.Root()?.ChildrenOfType(BlogAliases.Categories);
            return containers?.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Categories picked on a pillar. Empty when the picker is blank.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> RelatedCategories(IPublishedContent? pillar)
    {
        var result = new List<IPublishedContent>();
        if (pillar is null || pillar.HasProperty(BlogAliases.RelatedCategories) is false)
        {
            return result;
        }

        try
        {
            IEnumerable<IPublishedContent>? many = pillar.Value<IEnumerable<IPublishedContent>>(BlogAliases.RelatedCategories);
            if (many is not null)
            {
                foreach (IPublishedContent item in many)
                {
                    if (IsCategory(item))
                    {
                        result.Add(item);
                    }
                }

                return result;
            }

            IPublishedContent? single = pillar.Value<IPublishedContent>(BlogAliases.RelatedCategories);
            if (IsCategory(single) && single is not null)
            {
                result.Add(single);
            }
        }
        catch (Exception)
        {
            return result;
        }

        return result;
    }

    /// <summary>
    /// Newest posts whose picked category is this document.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> PostsInCategory(IPublishedContent? category, int take = 24)
    {
        if (category is null)
        {
            return [];
        }

        return PostsMatching(category, take, post =>
        {
            IPublishedContent? picked = BlogList.PickedCategory(post);
            return picked is not null && picked.Key == category.Key;
        });
    }

    /// <summary>
    /// Newest posts in any category picked on the pillar.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> PostsForPillar(IPublishedContent? pillar, int take = 6)
    {
        HashSet<Guid> keys = RelatedCategories(pillar).Select(item => item.Key).ToHashSet();
        if (keys.Count == 0 || pillar is null)
        {
            return [];
        }

        return PostsMatching(pillar, take, post =>
        {
            IPublishedContent? picked = BlogList.PickedCategory(post);
            return picked is not null && keys.Contains(picked.Key);
        });
    }

    /// <summary>
    /// Display name of a category document.
    /// </summary>
    public static string CategoryName(IPublishedContent? category)
        => Text(category, BlogAliases.CategoryName, category?.Name);

    /// <summary>
    /// A text property, or an empty string when it is missing.
    /// </summary>
    public static string TextOrEmpty(IPublishedContent? content, string alias)
        => Text(content, alias, null);

    /// <summary>
    /// Title of a pillar, falling back to the node name.
    /// </summary>
    public static string PillarTitle(IPublishedContent? pillar)
        => Text(pillar, BlogAliases.PillarTitle, pillar?.Name);

    /// <summary>
    /// Optional lead text.
    /// </summary>
    public static string? Lead(IPublishedContent? content)
    {
        var lead = Text(content, BlogAliases.Lead, null);
        return string.IsNullOrWhiteSpace(lead) ? null : lead;
    }

    /// <summary>
    /// CSS color for a category chip. Custom colors are accepted only as hex.
    /// </summary>
    public static string AccentCss(IPublishedContent? category)
    {
        if (TryHex(Text(category, BlogAliases.AccentColor, null), out var custom))
        {
            return custom;
        }

        var slug = Text(category, BlogAliases.Slug, null);
        if (string.IsNullOrWhiteSpace(slug) is false && AccentBySlug.TryGetValue(slug, out var mapped))
        {
            return mapped;
        }

        return "#22d3ee";
    }

    /// <summary>
    /// A Polish calendar date.
    /// </summary>
    public static string FormatDate(DateTime value)
        => value.ToString("d MMMM yyyy", Polish);

    private static int PillarOrder(IPublishedContent page)
    {
        try
        {
            int? number = page.Value<int?>(BlogAliases.PillarOrder);
            if (number.HasValue)
            {
                return number.Value;
            }

            var raw = page.Value<string>(BlogAliases.PillarOrder);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception)
        {
            return page.SortOrder;
        }

        return page.SortOrder;
    }

    private static IReadOnlyList<IPublishedContent> PostsMatching(IPublishedContent anyPage, int take, Func<IPublishedContent, bool> match)
    {
        var result = new List<IPublishedContent>();
        try
        {
            IEnumerable<IPublishedContent>? blogs = anyPage.Root()?.ChildrenOfType(BlogAliases.Blog);
            IPublishedContent? blog = blogs?.FirstOrDefault();
            IEnumerable<IPublishedContent>? posts = blog?.ChildrenOfType(BlogAliases.Post);
            if (posts is null)
            {
                return result;
            }

            foreach (IPublishedContent post in posts)
            {
                if (match(post))
                {
                    result.Add(post);
                }
            }
        }
        catch (Exception)
        {
            return result;
        }

        if (take < 1)
        {
            take = 1;
        }

        return result.OrderByDescending(BlogList.PublishDate).Take(take).ToList();
    }

    private static string Text(IPublishedContent? content, string alias, string? fallback)
    {
        if (content is null)
        {
            return fallback ?? string.Empty;
        }

        try
        {
            if (content.HasProperty(alias))
            {
                var value = content.Value<string>(alias);
                if (string.IsNullOrWhiteSpace(value) is false)
                {
                    return value.Trim();
                }
            }
        }
        catch (Exception)
        {
            return fallback ?? string.Empty;
        }

        return fallback ?? string.Empty;
    }

    private static bool IsCategory(IPublishedContent? item)
        => item is not null && item.ContentType.Alias.Equals(BlogAliases.Category, StringComparison.OrdinalIgnoreCase);

    private static bool TryHex(string? raw, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith('#') is false)
        {
            value = "#" + value;
        }

        if (value.Length is not 4 and not 7)
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (Uri.IsHexDigit(value[index]) is false)
            {
                return false;
            }
        }

        hex = value.ToLowerInvariant();
        return true;
    }
}
