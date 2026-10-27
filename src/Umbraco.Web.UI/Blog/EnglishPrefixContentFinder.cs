using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Routes /en and /en/... even when Culture and Hostnames did not match the request.
/// Outbound links still come from the domains assigned by the language migration.
/// </summary>
public class EnglishPrefixContentFinder : IContentFinder
{
    private readonly IDocumentUrlService _documentUrlService;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly ILogger<EnglishPrefixContentFinder> _logger;

    public EnglishPrefixContentFinder(
        IDocumentUrlService documentUrlService,
        IPublishedContentCache publishedContentCache,
        IUmbracoContextAccessor umbracoContextAccessor,
        ILogger<EnglishPrefixContentFinder> logger)
    {
        _documentUrlService = documentUrlService;
        _publishedContentCache = publishedContentCache;
        _umbracoContextAccessor = umbracoContextAccessor;
        _logger = logger;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        if (TryStripPrefix(request.AbsolutePathDecoded, out var remainder) is false)
        {
            return Task.FromResult(false);
        }

        if (_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context) is false)
        {
            return Task.FromResult(false);
        }

        try
        {
            request.SetCulture(SiteText.English);
            Guid? key = _documentUrlService.GetDocumentKeyByRoute(remainder, SiteText.English, null, context.InPreviewMode);
            if (key is null)
            {
                return Task.FromResult(false);
            }

            IPublishedContent? node = _publishedContentCache.GetById(context.InPreviewMode, key.Value);
            if (node is null)
            {
                return Task.FromResult(false);
            }

            request.SetPublishedContent(node);
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The /en prefix did not match a published English page.");
            return Task.FromResult(false);
        }
    }

    private static bool TryStripPrefix(string path, out string remainder)
    {
        remainder = "/";
        if (path.Equals("/en", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        remainder = path["/en".Length..];
        if (remainder.Length == 0)
        {
            remainder = "/";
        }

        return true;
    }
}
