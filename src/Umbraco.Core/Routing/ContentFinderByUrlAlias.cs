using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.Routing;

/// <summary>
///     Provides an implementation of <see cref="IContentFinder" /> that handles page aliases.
/// </summary>
/// <remarks>
///     <para>
///         Handles <c>/just/about/anything</c> where <c>/just/about/anything</c> is contained in the
///         <c>umbracoUrlAlias</c> property of a document.
///     </para>
///     <para>The alias is the full path to the document. There can be more than one alias, separated by commas.</para>
/// </remarks>
public class ContentFinderByUrlAlias : IContentFinder
{
    private readonly ILogger<ContentFinderByUrlAlias> _logger;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IDocumentNavigationQueryService _documentNavigationQueryService;
    private readonly IPublishedContentStatusFilteringService _publishedContentStatusFilteringService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentFinderByUrlAlias" /> class.
    /// </summary>
    public ContentFinderByUrlAlias(
        ILogger<ContentFinderByUrlAlias> logger,
        IPublishedValueFallback publishedValueFallback,
        IUmbracoContextAccessor umbracoContextAccessor,
        IDocumentNavigationQueryService documentNavigationQueryService,
        IPublishedContentStatusFilteringService publishedContentStatusFilteringService)
    {
        _publishedValueFallback = publishedValueFallback;
        _umbracoContextAccessor = umbracoContextAccessor;
        _documentNavigationQueryService = documentNavigationQueryService;
        _publishedContentStatusFilteringService = publishedContentStatusFilteringService;
        _logger = logger;
    }

    [Obsolete("Please use tne non-obsolete constructor instead. Scheduled removal in v17")]
    public ContentFinderByUrlAlias(
        ILogger<ContentFinderByUrlAlias> logger,
        IPublishedValueFallback publishedValueFallback,
        IVariationContextAccessor variationContextAccessor,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService documentNavigationQueryService,
        IPublishStatusQueryService publishStatusQueryService,
        IPublishedContentStatusFilteringService publishedContentStatusFilteringService)
        : this(
            logger,
            publishedValueFallback,
            umbracoContextAccessor,
            documentNavigationQueryService,
            publishedContentStatusFilteringService)
    {
    }

    [Obsolete("Please use tne non-obsolete constructor instead. Scheduled removal in v17")]
    public ContentFinderByUrlAlias(
        ILogger<ContentFinderByUrlAlias> logger,
        IPublishedValueFallback publishedValueFallback,
        IVariationContextAccessor variationContextAccessor,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService documentNavigationQueryService,
        IPublishStatusQueryService publishStatusQueryService)
        : this(
            logger,
            publishedValueFallback,
            umbracoContextAccessor,
            documentNavigationQueryService,
            StaticServiceProvider.Instance.GetRequiredService<IPublishedContentStatusFilteringService>())
    {
    }


    [Obsolete("Please use tne non-obsolete constructor instead. Scheduled removal in v17")]
    public ContentFinderByUrlAlias(
        ILogger<ContentFinderByUrlAlias> logger,
        IPublishedValueFallback publishedValueFallback,
        IVariationContextAccessor variationContextAccessor,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService documentNavigationQueryService)
        : this(
            logger,
            publishedValueFallback,
            umbracoContextAccessor,
            documentNavigationQueryService,
            StaticServiceProvider.Instance.GetRequiredService<IPublishedContentStatusFilteringService>())
    {
    }

    /// <summary>
    ///     Tries to find and assign an Umbraco document to a <c>PublishedRequest</c>.
    /// </summary>
    /// <param name="frequest">The <c>PublishedRequest</c>.</param>
    /// <returns>A value indicating whether an Umbraco document was found and assigned.</returns>
    public Task<bool> TryFindContent(IPublishedRequestBuilder frequest)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext))
        {
            return Task.FromResult(false);
        }

        IPublishedContent? node = null;

        // no alias if "/"
        if (frequest.Uri.AbsolutePath != "/")
        {
            node = FindContentByAlias(
                umbracoContext.Content,
                frequest.Domain != null ? frequest.Domain.ContentId : 0,
                frequest.Culture,
                frequest.AbsolutePathDecoded);

            if (node != null)
            {
                frequest.SetPublishedContent(node);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Path '{UriAbsolutePath}' is an alias for id={PublishedContentId}", frequest.Uri.AbsolutePath, node.Id);
                }
            }
        }

        return Task.FromResult(node != null);
    }

    private IPublishedContent? FindContentByAlias(IPublishedContentCache? cache, int rootNodeId, string? culture, string alias)
    {
        if (alias == null)
        {
            throw new ArgumentNullException(nameof(alias));
        }

        // the alias may be "foo/bar" or "/foo/bar"
        // there may be spaces as in "/foo/bar,  /foo/nil"
        // these should probably be taken care of earlier on

        // TODO: can we normalize the values so that they contain no whitespaces, and no leading slashes?
        // and then the comparisons in IsMatch can be way faster - and allocate way less strings
        const string propertyAlias = Constants.Conventions.Content.UrlAlias;

        var test1 = alias.TrimStart(Constants.CharArrays.ForwardSlash) + ",";
        var test2 = ",/" + test1; // test2 is ",/alias,"
        test1 = "," + test1; // test1 is ",alias,"

        bool IsMatch(IPublishedContent c, string a1, string a2)
        {
            // this basically implements the original XPath query ;-(
            //
            // "//* [@isDoc and (" +
            // "contains(concat(',',translate(umbracoUrlAlias, ' ', ''),','),',{0},')" +
            // " or contains(concat(',',translate(umbracoUrlAlias, ' ', ''),','),',/{0},')" +
            // ")]"
            if (!c.HasProperty(propertyAlias))
            {
                return false;
            }

            IPublishedProperty? p = c.GetProperty(propertyAlias);
            var varies = p?.PropertyType?.VariesByCulture();
            string? v;
            if (varies ?? false)
            {
                if (!c.HasCulture(culture))
                {
                    return false;
                }

                v = c.Value<string>(_publishedValueFallback, propertyAlias, culture);
            }
            else
            {
                v = c.Value<string>(_publishedValueFallback, propertyAlias);
            }

            if (string.IsNullOrWhiteSpace(v))
            {
                return false;
            }

            v = "," + v.Replace(" ", string.Empty) + ",";
            return v.InvariantContains(a1) || v.InvariantContains(a2);
        }

        // TODO: even with Linq, what happens below has to be horribly slow
        // but the only solution is to entirely refactor URL providers to stop being dynamic
        if (rootNodeId > 0)
        {
            IPublishedContent? rootNode = cache?.GetById(rootNodeId);
            return rootNode?.Descendants(_documentNavigationQueryService, _publishedContentStatusFilteringService).FirstOrDefault(x => IsMatch(x, test1, test2));
        }

        if (cache is not null)
        {
            foreach (IPublishedContent rootContent in cache.GetAtRoot())
            {
                IPublishedContent? c = rootContent.DescendantsOrSelf(_documentNavigationQueryService, _publishedContentStatusFilteringService)
                    .FirstOrDefault(x => IsMatch(x, test1, test2));
                if (c != null)
                {
                    return c;
                }
            }
        }

        return null;
    }
}
