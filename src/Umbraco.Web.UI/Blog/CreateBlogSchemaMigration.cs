using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Creates the Home, Blog, and Blog post document types and their templates.
/// Existing aliases are left untouched so a repeated run cannot rewrite content or schema.
/// </summary>
public class CreateBlogSchemaMigration : AsyncMigrationBase
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ITemplateService _templateService;
    private readonly IShortStringHelper _shortStringHelper;

    public CreateBlogSchemaMigration(
        IMigrationContext context,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ITemplateService templateService,
        IShortStringHelper shortStringHelper)
        : base(context)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _templateService = templateService;
        _shortStringHelper = shortStringHelper;
    }

    protected override async Task MigrateAsync()
    {
        // Document types are read by the published cache. Rebuild after this step
        // so the seeder and the first request see them. Do not revoke backoffice tokens.
        RebuildCache = true;

        ITemplate homeTemplate = await EnsureTemplateAsync(BlogAliases.HomeTemplate, "Home");
        ITemplate blogTemplate = await EnsureTemplateAsync(BlogAliases.BlogTemplate, "Blog");
        ITemplate postTemplate = await EnsureTemplateAsync(BlogAliases.PostTemplate, "Blog post");

        IDataType textarea = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextareaGuid);
        IDataType date = await RequiredDataTypeAsync(Constants.DataTypes.Guids.DatePickerGuid);
        IDataType richText = await RequiredDataTypeAsync(Constants.DataTypes.Guids.RichtextEditorGuid);
        IDataType image = await RequiredDataTypeAsync(Constants.DataTypes.Guids.MediaPicker3SingleImageGuid);
        IDataType tags = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TagsGuid);

        IContentType postType = await EnsureContentTypeAsync(
            BlogAliases.Post,
            "Blog post",
            "icon-document",
            "A single post: title, date, excerpt, body, optional image and tags.",
            allowedAtRoot: false,
            postTemplate,
            listView: null,
            allowedChildren: null,
            properties =>
            {
                AddProperty(properties, date, BlogAliases.PublishDate, "Publish date", 0, mandatory: true, "Shown on the post and used to sort the list, newest first.");
                AddProperty(properties, textarea, BlogAliases.Summary, "Summary", 1, mandatory: true, "Short excerpt shown in listings.");
                AddProperty(properties, richText, BlogAliases.Body, "Body", 2, mandatory: true);
                AddProperty(properties, image, BlogAliases.CoverImage, "Cover image", 3, mandatory: false, "Optional.");
                AddProperty(properties, tags, BlogAliases.Tags, "Tags", 4, mandatory: false, "Optional.");
            });

        IContentType blogType = await EnsureContentTypeAsync(
            BlogAliases.Blog,
            "Blog",
            "icon-notebook",
            "Lists blog posts, newest first.",
            allowedAtRoot: false,
            blogTemplate,
            Constants.DataTypes.Guids.ListViewContentGuid,
            [postType],
            properties =>
            {
                AddProperty(properties, textarea, BlogAliases.Intro, "Intro", 0, mandatory: false, "Optional text shown above the list.");
            });

        await EnsureContentTypeAsync(
            BlogAliases.Home,
            "Home",
            "icon-home",
            "The site front page.",
            allowedAtRoot: true,
            homeTemplate,
            listView: null,
            [blogType],
            properties =>
            {
                AddProperty(properties, textarea, BlogAliases.Intro, "Intro", 0, mandatory: false, "Optional text shown on the front page.");
            });
    }

    private async Task<ITemplate> EnsureTemplateAsync(string alias, string name)
    {
        ITemplate? existing = await _templateService.GetAsync(alias);
        if (existing is not null)
        {
            Logger.LogInformation("Template {Alias} already exists; leaving it unchanged.", alias);
            return existing;
        }

        // Null content keeps the view file that was deployed with the site.
        // TemplateService reads Views/{alias}.cshtml when that file is already on disk.
        Attempt<ITemplate, TemplateOperationStatus> created = await _templateService.CreateAsync(
            name,
            alias,
            content: null,
            Constants.Security.SuperUserKey);

        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating template '{alias}' failed: {created.Status}.");
        }

        Logger.LogInformation("Created template {Alias}.", alias);
        return created.Result;
    }

    private async Task<IContentType> EnsureContentTypeAsync(
        string alias,
        string name,
        string icon,
        string description,
        bool allowedAtRoot,
        ITemplate template,
        Guid? listView,
        IReadOnlyList<IContentType>? allowedChildren,
        Action<IContentType> addProperties)
    {
        IContentType? existing = _contentTypeService.Get(alias);
        if (existing is not null)
        {
            Logger.LogInformation("Document type {Alias} already exists; leaving it and its content unchanged.", alias);
            return existing;
        }

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Icon = icon,
            Description = description,
            AllowedAsRoot = allowedAtRoot,
            ListView = listView,
            AllowedTemplates = [template],
        };
        contentType.SetDefaultTemplate(template);
        addProperties(contentType);

        if (allowedChildren is { Count: > 0 })
        {
            contentType.AllowedContentTypes = allowedChildren
                .Select((child, index) => new ContentTypeSort(child.Key, index, child.Alias))
                .ToArray();
        }

        Attempt<ContentTypeOperationStatus> result = await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Creating document type '{alias}' failed: {result.Result}.");
        }

        Logger.LogInformation("Created document type {Alias}.", alias);
        return contentType;
    }

    private async Task<IDataType> RequiredDataTypeAsync(Guid key)
        => await _dataTypeService.GetAsync(key)
           ?? throw new InvalidOperationException($"Required Umbraco data type {key} was not found. The site database does not look like a completed install.");

    private void AddProperty(
        IContentType contentType,
        IDataType dataType,
        string alias,
        string name,
        int sortOrder,
        bool mandatory,
        string? description = null)
    {
        var property = new PropertyType(_shortStringHelper, dataType, alias)
        {
            Name = name,
            SortOrder = sortOrder,
            Mandatory = mandatory,
            Description = description,
            DataTypeKey = dataType.Key,
        };

        if (contentType.AddPropertyType(property, BlogAliases.PropertyGroup, "Content") is false)
        {
            throw new InvalidOperationException($"Could not add property '{alias}' to document type '{contentType.Alias}'.");
        }
    }
}
