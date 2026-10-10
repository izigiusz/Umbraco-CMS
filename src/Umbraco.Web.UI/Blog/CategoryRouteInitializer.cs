using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Package migrations suppress publish notifications, so seeded categories have no public URL.
/// This creates those URLs once the site is running and does not change category content.
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

        if (_contentService.GetById(CategorySeed.ContainerKey) is null)
        {
            return;
        }

        try
        {
            ILanguage? language = await _languageService.GetDefaultLanguageAsync();
            var culture = language?.IsoCode ?? string.Empty;
            if (string.IsNullOrEmpty(_documentUrlService.GetUrlSegment(CategorySeed.ContainerKey, culture, isDraft: false)) is false)
            {
                return;
            }

            await _documentUrlService.CreateOrUpdateUrlSegmentsWithDescendantsAsync(CategorySeed.ContainerKey);
            _logger.LogInformation("Created public URLs for the category container and its children.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Category pages were published, but their public URLs could not be created.");
        }
    }
}
