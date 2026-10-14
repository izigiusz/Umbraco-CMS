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
    private readonly IRedirectUrlService _redirectUrlService;
    private readonly ILogger<CategoryRouteInitializer> _logger;

    public CategoryRouteInitializer(
        IContentService contentService,
        IDocumentUrlService documentUrlService,
        ILanguageService languageService,
        IRedirectUrlService redirectUrlService,
        ILogger<CategoryRouteInitializer> logger)
    {
        _contentService = contentService;
        _documentUrlService = documentUrlService;
        _languageService = languageService;
        _redirectUrlService = redirectUrlService;
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
            await EnsureNodeRouteAsync(CategorySeed.Neo4jKey, culture, "Neo4j");
            await EnsureNodeRouteAsync(AddToolsCatalogMigration.CatalogKey, culture, AddToolsCatalogMigration.CatalogName);
            await EnsurePillarRoutesAsync(culture);
            await EnsureNewsRoutesAsync(culture);
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

    private async Task EnsureNodeRouteAsync(Guid key, string culture, string label)
    {
        IContent? content = _contentService.GetById(key);
        if (content is null || content.Trashed)
        {
            return;
        }

        if (string.IsNullOrEmpty(_documentUrlService.GetUrlSegment(key, culture, isDraft: false)) is false)
        {
            return;
        }

        await _documentUrlService.CreateOrUpdateUrlSegmentsAsync(key);
        _logger.LogInformation("Created a public URL for {Label}.", label);
    }

    private async Task EnsurePillarRoutesAsync(string culture)
    {
        foreach (PillarSeedItem item in PillarSeed.Items)
        {
            IContent? content = _contentService.GetById(item.Key);
            if (content is null || content.Trashed)
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

    private async Task EnsureNewsRoutesAsync(string culture)
    {
        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));
        if (home is null)
        {
            return;
        }

        IContent? blog = _contentService.GetPagedChildren(home.Id, 0, 100, out _)
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Blog, StringComparison.OrdinalIgnoreCase));
        if (blog is null || string.Equals(blog.Name, RestructurePortalMigration.NewsName, StringComparison.Ordinal) is false)
        {
            return;
        }

        var segment = _documentUrlService.GetUrlSegment(blog.Key, culture, isDraft: false);
        if (string.Equals(segment, "aktualnosci", StringComparison.OrdinalIgnoreCase) is false)
        {
            await _documentUrlService.CreateOrUpdateUrlSegmentsWithDescendantsAsync(blog.Key);
            segment = _documentUrlService.GetUrlSegment(blog.Key, culture, isDraft: false);
        }

        if (string.Equals(segment, "aktualnosci", StringComparison.OrdinalIgnoreCase) is false)
        {
            _logger.LogWarning("The news URL segment is {Segment}, so /blog redirects were not registered.", segment);
            return;
        }

        RegisterLegacyRoute("/blog", blog.Key);
        RegisterLegacyRoute("/blog/", blog.Key);

        foreach (IContent child in _contentService.GetPagedChildren(blog.Id, 0, 200, out _))
        {
            if (child.Trashed)
            {
                continue;
            }

            var childSegment = _documentUrlService.GetUrlSegment(child.Key, culture, isDraft: false);
            if (string.IsNullOrWhiteSpace(childSegment))
            {
                continue;
            }

            RegisterLegacyRoute("/blog/" + childSegment, child.Key);
            RegisterLegacyRoute("/blog/" + childSegment + "/", child.Key);
        }

        _logger.LogInformation("Kept /blog routes as redirects to Aktualności.");
    }

    private void RegisterLegacyRoute(string route, Guid contentKey)
        => _redirectUrlService.Register(route, contentKey);
}
