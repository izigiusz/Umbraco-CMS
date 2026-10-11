using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Package migrations suppress publish notifications, so seeded pages have no public URL.
/// This creates those URLs once the site is running and does not change the content.
/// </summary>
public class CategoryRouteInitializer : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IContentService _contentService;
    private readonly IDocumentUrlService _documentUrlService;
    private readonly ILanguageService _languageService;
    private readonly ILogger<CategoryRouteInitializer> _logger;

    public CategoryRouteInitializer(
        IContentService contentService,
        IDocumentUrlService documentUrlService,
        ILanguageService languageService,
        ILogger<CategoryRouteInitializer> logger)
    {
        _contentService = contentService;
        _documentUrlService = documentUrlService;
        _languageService = languageService;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            ILanguage? language = await _languageService.GetDefaultLanguageAsync();
            var culture = language?.IsoCode ?? string.Empty;
            await EnsureContainerRouteAsync(culture);
            await EnsurePillarRoutesAsync(culture);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Published portal pages were missing public URLs.");
        }
    }

    private async Task EnsureContainerRouteAsync(string culture)
    {
        if (_contentService.GetById(CategorySeed.ContainerKey) is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_documentUrlService.GetUrlSegment(CategorySeed.ContainerKey, culture, isDraft: false)) is false)
        {
            return;
        }

        await _documentUrlService.CreateOrUpdateUrlSegmentsWithDescendantsAsync(CategorySeed.ContainerKey);
        _logger.LogInformation("Created public URLs for the category container and its children.");
    }

    private async Task EnsurePillarRoutesAsync(string culture)
    {
        foreach (PillarSeedItem item in PillarSeed.Items)
        {
            if (_contentService.GetById(item.Key) is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(_documentUrlService.GetUrlSegment(item.Key, culture, isDraft: false)) is false)
            {
                continue;
            }

            await _documentUrlService.CreateOrUpdateUrlSegmentsAsync(item.Key);
            _logger.LogInformation("Created a public URL for pillar {Title}.", item.Title);
        }
    }
}
