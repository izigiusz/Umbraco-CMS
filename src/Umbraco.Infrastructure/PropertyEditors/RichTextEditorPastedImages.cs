// Copyright (c) Umbraco.
// See LICENSE for more details.

using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Exceptions;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Models.TemporaryFile;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.PropertyEditors;

public sealed class RichTextEditorPastedImages
{
    private const string TemporaryImageDataAttribute = "data-tmpimg";
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly ITemporaryFileService _temporaryFileService;
    private readonly IScopeProvider _scopeProvider;
    private readonly IMediaImportService _mediaImportService;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IEntityService _entityService;
    private readonly AppCaches _appCaches;
    private readonly IUserService _userService;

    [Obsolete("Please use the non-obsolete constructor. Will be removed in V17.")]
    public RichTextEditorPastedImages(
        IUmbracoContextAccessor umbracoContextAccessor,
        ILogger<RichTextEditorPastedImages> logger,
        IHostingEnvironment hostingEnvironment,
        IMediaService mediaService,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IPublishedUrlProvider publishedUrlProvider,
        ITemporaryFileService temporaryFileService,
        IScopeProvider scopeProvider,
        IMediaImportService mediaImportService,
        IImageUrlGenerator imageUrlGenerator,
        IOptions<ContentSettings> contentSettings,
        IEntityService entityService,
        AppCaches appCaches)
        : this(umbracoContextAccessor, publishedUrlProvider, temporaryFileService, scopeProvider, mediaImportService, imageUrlGenerator, entityService, appCaches)
    {
    }

    [Obsolete("Please use the non-obsolete constructor. Will be removed in V17.")]
    public RichTextEditorPastedImages(
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        ITemporaryFileService temporaryFileService,
        IScopeProvider scopeProvider,
        IMediaImportService mediaImportService,
        IImageUrlGenerator imageUrlGenerator)
        : this(
            umbracoContextAccessor,
            publishedUrlProvider,
            temporaryFileService,
            scopeProvider,
            mediaImportService,
            imageUrlGenerator,
            StaticServiceProvider.Instance.GetRequiredService<IEntityService>(),
            StaticServiceProvider.Instance.GetRequiredService<AppCaches>())
    {
    }

    public RichTextEditorPastedImages(
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        ITemporaryFileService temporaryFileService,
        IScopeProvider scopeProvider,
        IMediaImportService mediaImportService,
        IImageUrlGenerator imageUrlGenerator,
        IEntityService entityService,
        AppCaches appCaches)
    {
        _umbracoContextAccessor =
            umbracoContextAccessor ?? throw new ArgumentNullException(nameof(umbracoContextAccessor));
        _publishedUrlProvider = publishedUrlProvider;
        _temporaryFileService = temporaryFileService;
        _scopeProvider = scopeProvider;
        _mediaImportService = mediaImportService;
        _imageUrlGenerator = imageUrlGenerator;
        _entityService = entityService;
        _appCaches = appCaches;

        // this obviously is not correct. however, we only use IUserService in an obsolete method,
        // so this is better than having even more obsolete constructors for V16
        _userService = StaticServiceProvider.Instance.GetRequiredService<IUserService>();
    }

    /// <summary>
    ///     Used by the RTE (and grid RTE) for drag/drop/persisting images.
    /// </summary>
    public async Task<string> FindAndPersistPastedTempImagesAsync(string html, Guid mediaParentFolder, Guid userKey)
    {
        // Find all img's that has data-tmpimg attribute
        // Use HTML Agility Pack - https://html-agility-pack.net
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        HtmlNodeCollection? tmpImages = htmlDoc.DocumentNode.SelectNodes($"//img[@{TemporaryImageDataAttribute}]");
        if (tmpImages is null || tmpImages.Count is 0)
        {
            return html;
        }

        // An array to contain a list of URLs that
        // we have already processed to avoid dupes
        var uploadedImages = new Dictionary<Guid, GuidUdi>();

        foreach (HtmlNode? img in tmpImages)
        {
            // The data attribute contains the key of the temporary file
            var tmpImgKey = img.GetAttributeValue(TemporaryImageDataAttribute, string.Empty);
            if (Guid.TryParse(tmpImgKey, out Guid temporaryFileKey) is false)
            {
                continue;
            }

            TemporaryFileModel? temporaryFile = _temporaryFileService.GetAsync(temporaryFileKey).GetAwaiter().GetResult();
            if (temporaryFile is null)
            {
                continue;
            }

            GuidUdi udi;

            using (IScope scope = _scopeProvider.CreateScope())
            {
                _temporaryFileService.EnlistDeleteIfScopeCompletes(temporaryFileKey, _scopeProvider);

                if (uploadedImages.ContainsKey(temporaryFileKey) == false)
                {
                    using Stream fileStream = temporaryFile.OpenReadStream();
                    Guid? parentFolderKey = mediaParentFolder == Guid.Empty ? await GetDefaultMediaRoot(userKey) : mediaParentFolder;
                    IMedia mediaFile = await _mediaImportService.ImportAsync(temporaryFile.FileName, fileStream, parentFolderKey, MediaTypeAlias(temporaryFile.FileName), userKey);
                    udi = mediaFile.GetUdi();
                }
                else
                {
                    // Already been uploaded & we have it's UDI
                    udi = uploadedImages[temporaryFileKey];
                }

                scope.Complete();
            }

            // Add the UDI to the img element as new data attribute
            img.SetAttributeValue("data-udi", udi.ToString());

            // Get the new persisted image URL
            _umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext);
            IPublishedContent? mediaTyped = umbracoContext?.Media?.GetById(udi.Guid);
            if (mediaTyped == null)
            {
                throw new PanicException(
                    $"Could not find media by id {udi.Guid} or there was no UmbracoContext available.");
            }

            var location = mediaTyped.Url(_publishedUrlProvider);

            // Find the width & height attributes as we need to set the imageprocessor QueryString
            var width = img.GetAttributeValue("width", int.MinValue);
            var height = img.GetAttributeValue("height", int.MinValue);

            if (width != int.MinValue && height != int.MinValue)
            {
                location = _imageUrlGenerator.GetImageUrl(new ImageUrlGenerationOptions(location)
                {
                    ImageCropMode = ImageCropMode.Max,
                    Width = width,
                    Height = height,
                });
            }

            img.SetAttributeValue("src", location ?? string.Empty);

            // Remove the data attribute (so we do not re-process this)
            img.Attributes.Remove(TemporaryImageDataAttribute);

            // Add to the dictionary to avoid dupes
            uploadedImages.TryAdd(temporaryFileKey, udi);
        }

        return htmlDoc.DocumentNode.OuterHtml;
    }

    private async Task<Guid?> GetDefaultMediaRoot(Guid userKey)
    {
        IUser user = await _userService.GetAsync(userKey) ?? throw new ArgumentException("User could not be found");
        var userStartNodes = user.CalculateMediaStartNodeIds(_entityService, _appCaches);
        var firstNodeId = userStartNodes?.FirstOrDefault();
        if (firstNodeId is null)
        {
            return Constants.System.RootKey;
        }

        Attempt<Guid> firstNodeKeyAttempt = _entityService.GetKey(firstNodeId.Value, UmbracoObjectTypes.Media);
        return firstNodeKeyAttempt.Success ? firstNodeKeyAttempt.Result : Constants.System.RootKey;
    }

    private string MediaTypeAlias(string fileName)
        => fileName.InvariantEndsWith(".svg")
            ? Constants.Conventions.MediaTypes.VectorGraphicsAlias
            : Constants.Conventions.MediaTypes.Image;
}
