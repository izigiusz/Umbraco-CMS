using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Rejects a category save when its slug is empty or already used by another category.
/// </summary>
public class CategorySlugSavingHandler : INotificationHandler<ContentSavingNotification>
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;

    public CategorySlugSavingHandler(IContentService contentService, IContentTypeService contentTypeService)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
    }

    public void Handle(ContentSavingNotification notification)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IContent content in notification.SavedEntities)
        {
            if (content.ContentType.Alias.Equals(BlogAliases.Category, StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            var slug = content.GetValue<string>(BlogAliases.Slug)?.Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                notification.CancelOperation(new EventMessage(
                    "Category",
                    "Slug is required.",
                    EventMessageType.Error));
                return;
            }

            content.SetValue(BlogAliases.Slug, slug);

            if (seen.Add(slug) is false || SlugUsedByOther(content, slug))
            {
                notification.CancelOperation(new EventMessage(
                    "Category",
                    $"Another category already uses the slug \"{slug}\".",
                    EventMessageType.Error));
                return;
            }
        }
    }

    private bool SlugUsedByOther(IContent content, string slug)
    {
        IContentType? categoryType = _contentTypeService.Get(BlogAliases.Category);
        if (categoryType is null)
        {
            return false;
        }

        long page = 0;
        while (true)
        {
            IEnumerable<IContent> batch = _contentService.GetPagedOfTypes([categoryType.Id], page, 100, out long total, null);
            foreach (IContent other in batch)
            {
                if (other.Key == content.Key)
                {
                    continue;
                }

                var otherSlug = other.GetValue<string>(BlogAliases.Slug)?.Trim();
                if (string.Equals(otherSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            page++;
            if (page * 100 >= total)
            {
                return false;
            }
        }
    }
}
