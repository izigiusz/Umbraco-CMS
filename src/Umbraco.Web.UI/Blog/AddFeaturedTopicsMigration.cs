using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Adds category documents, the featured-topics picker, and the Codeation categories.
/// Existing properties and content values are not removed or overwritten.
/// The sample post is not assigned a category: its text does not match these topics.
/// </summary>
public class AddFeaturedTopicsMigration : AsyncMigrationBase
{
    private const string PickerEditorUiAlias = "Umb.PropertyEditorUi.ContentPicker";

    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;

    public AddFeaturedTopicsMigration(
        IMigrationContext context,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ITemplateService templateService,
        IContentService contentService,
        IContentPublishingService publishingService,
        IShortStringHelper shortStringHelper,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer)
        : base(context)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _templateService = templateService;
        _contentService = contentService;
        _publishingService = publishingService;
        _shortStringHelper = shortStringHelper;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
    }

    protected override async Task MigrateAsync()
    {
        RebuildCache = true;

        IContentType postType = RequiredContentType(BlogAliases.Post);
        IContentType homeType = RequiredContentType(BlogAliases.Home);

        ITemplate categoryTemplate = await EnsureTemplateAsync(BlogAliases.CategoryTemplate, "Category");
        ITemplate categoriesTemplate = await EnsureTemplateAsync(BlogAliases.CategoriesTemplate, "Categories");

        IContentType categoryType = await EnsureCategoryTypeAsync(categoryTemplate);
        IContentType containerType = await EnsureContainerTypeAsync(categoriesTemplate, categoryType);

        IDataType categoryPicker = await EnsurePickerAsync(BlogAliases.CategoryPickerDataTypeName, categoryType.Key, single: true);
        IDataType featuredPicker = await EnsurePickerAsync(BlogAliases.FeaturedTopicsDataTypeName, postType.Key, single: false);

        if (postType.PropertyTypeExists(BlogAliases.Category) is false)
        {
            AddProperty(
                postType,
                categoryPicker,
                BlogAliases.Category,
                "Category",
                5,
                "Optional. Pick a category. Featured cards use its name, or the first tag when empty.");
            await SaveContentTypeAsync(postType);
            Logger.LogInformation("Added property {Alias} to {ContentType}.", BlogAliases.Category, BlogAliases.Post);
        }
        else
        {
            Logger.LogInformation("Property {Alias} already exists on {ContentType}; leaving it unchanged.", BlogAliases.Category, BlogAliases.Post);
        }

        var homeChanged = false;
        if (homeType.PropertyTypeExists(BlogAliases.FeaturedTopics) is false)
        {
            AddProperty(
                homeType,
                featuredPicker,
                BlogAliases.FeaturedTopics,
                "Polecane tematy",
                0,
                "Optional. Pick blog posts to feature. Leave empty to hide the section.",
                BlogAliases.FeaturedGroup,
                "Polecane tematy",
                PropertyGroupType.Tab);
            homeChanged = true;
            Logger.LogInformation("Added property {Alias} to {ContentType}.", BlogAliases.FeaturedTopics, BlogAliases.Home);
        }
        else
        {
            Logger.LogInformation("Property {Alias} already exists on {ContentType}; leaving it unchanged.", BlogAliases.FeaturedTopics, BlogAliases.Home);
        }

        if (AllowChild(homeType, containerType))
        {
            homeChanged = true;
            Logger.LogInformation("Allowed {Child} under {Parent}.", BlogAliases.Categories, BlogAliases.Home);
        }

        if (homeChanged)
        {
            await SaveContentTypeAsync(homeType);
        }

        await SeedCategoriesAsync(categoryType, containerType);
    }

    private async Task<IContentType> EnsureCategoryTypeAsync(ITemplate template)
    {
        IContentType? existing = _contentTypeService.Get(BlogAliases.Category);
        if (existing is not null)
        {
            await AddMissingCategoryPropertiesAsync(existing, template);
            return _contentTypeService.Get(BlogAliases.Category) ?? existing;
        }

        IDataType text = await RequiredTextAsync();
        IDataType area = await RequiredTextAreaAsync();
        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = BlogAliases.Category,
            Name = "Category",
            Icon = "icon-tag",
            Description = "A Codeation topic that can be picked on a blog post.",
            AllowedAsRoot = false,
            IsElement = false,
            AllowedTemplates = [template],
        };
        contentType.SetDefaultTemplate(template);
        AddProperty(contentType, text, BlogAliases.CategoryName, "Nazwa", 0, "Display name.", mandatory: true);
        AddProperty(contentType, text, BlogAliases.Slug, "Slug", 1, "Unique among categories.", mandatory: true);
        AddProperty(contentType, area, BlogAliases.Description, "Opis", 2, "Optional.");

        await SaveNewContentTypeAsync(contentType);
        Logger.LogInformation("Created document type {Alias}.", BlogAliases.Category);
        return contentType;
    }

    private async Task AddMissingCategoryPropertiesAsync(IContentType contentType, ITemplate template)
    {
        var changed = false;
        IDataType text = await RequiredTextAsync();
        IDataType area = await RequiredTextAreaAsync();
        if (contentType.PropertyTypeExists(BlogAliases.CategoryName) is false)
        {
            AddProperty(contentType, text, BlogAliases.CategoryName, "Nazwa", 0, "Display name.", mandatory: true);
            changed = true;
        }

        if (contentType.PropertyTypeExists(BlogAliases.Slug) is false)
        {
            AddProperty(contentType, text, BlogAliases.Slug, "Slug", 1, "Unique among categories.", mandatory: true);
            changed = true;
        }

        if (contentType.PropertyTypeExists(BlogAliases.Description) is false)
        {
            AddProperty(contentType, area, BlogAliases.Description, "Opis", 2, "Optional.");
            changed = true;
        }

        if (contentType.DefaultTemplateId == 0)
        {
            contentType.AllowedTemplates = [template];
            contentType.SetDefaultTemplate(template);
            changed = true;
        }

        if (changed)
        {
            await SaveContentTypeAsync(contentType);
        }
        else
        {
            Logger.LogInformation("Document type {Alias} already exists; leaving its properties unchanged.", BlogAliases.Category);
        }
    }

    private async Task<IContentType> EnsureContainerTypeAsync(ITemplate template, IContentType categoryType)
    {
        IContentType? existing = _contentTypeService.Get(BlogAliases.Categories);
        if (existing is not null)
        {
            if (AllowChild(existing, categoryType) || existing.DefaultTemplateId == 0)
            {
                if (existing.DefaultTemplateId == 0)
                {
                    existing.AllowedTemplates = [template];
                    existing.SetDefaultTemplate(template);
                }

                await SaveContentTypeAsync(existing);
            }

            return _contentTypeService.Get(BlogAliases.Categories) ?? existing;
        }

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = BlogAliases.Categories,
            Name = "Categories",
            Icon = "icon-folders",
            Description = "Holds category items. Editors manage them under Home.",
            AllowedAsRoot = false,
            IsElement = false,
            AllowedTemplates = [template],
        };
        contentType.SetDefaultTemplate(template);
        AllowChild(contentType, categoryType);
        await SaveNewContentTypeAsync(contentType);
        Logger.LogInformation("Created document type {Alias}.", BlogAliases.Categories);
        return contentType;
    }

    private async Task SeedCategoriesAsync(IContentType categoryType, IContentType containerType)
    {
        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));
        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so categories were not seeded.");
            return;
        }

        IContent container = await EnsureContainerAsync(home, containerType);

        foreach (CategorySeedItem item in CategorySeed.Items)
        {
            if (_contentService.GetById(item.Key) is not null || SlugExists(item.Slug))
            {
                Logger.LogInformation("Category {Slug} already exists; it was not modified.", item.Slug);
                continue;
            }

            var node = _contentService.Create(item.Name, container, categoryType.Alias);
            node.Key = item.Key;
            node.SetValue(BlogAliases.CategoryName, item.Name);
            node.SetValue(BlogAliases.Slug, item.Slug);
            if (string.IsNullOrWhiteSpace(item.Description) is false)
            {
                node.SetValue(BlogAliases.Description, item.Description);
            }

            if (categoryType.DefaultTemplateId > 0)
            {
                node.TemplateId = categoryType.DefaultTemplateId;
            }

            await SaveAndPublishAsync(node);
            Logger.LogInformation("Published category {Name} ({Slug}).", item.Name, item.Slug);
        }
    }

    private async Task<IContent> EnsureContainerAsync(IContent home, IContentType containerType)
    {
        IContent? existing = _contentService.GetById(CategorySeed.ContainerKey);
        if (existing is null)
        {
            IEnumerable<IContent> children = _contentService.GetPagedChildren(home.Id, 0, 100, out _);
            existing = children.FirstOrDefault(item =>
                item.ContentType.Alias.Equals(BlogAliases.Categories, StringComparison.OrdinalIgnoreCase));
        }

        if (existing is not null)
        {
            Logger.LogInformation("Category container already exists; it was not modified.");
            return existing;
        }

        var container = _contentService.Create("Kategorie", home, containerType.Alias);
        container.Key = CategorySeed.ContainerKey;
        if (containerType.DefaultTemplateId > 0)
        {
            container.TemplateId = containerType.DefaultTemplateId;
        }

        await SaveAndPublishAsync(container);
        Logger.LogInformation("Published category container Kategorie.");
        return container;
    }

    private bool SlugExists(string slug)
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
            foreach (IContent item in batch)
            {
                var existing = item.GetValue<string>(BlogAliases.Slug)?.Trim();
                if (string.Equals(existing, slug, StringComparison.OrdinalIgnoreCase))
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

    private async Task SaveAndPublishAsync(IContent content)
    {
        OperationResult saved = _contentService.Save(content);
        if (saved.Success is false)
        {
            throw new InvalidOperationException($"Saving '{content.Name}' failed: {saved.Result}.");
        }

        Attempt<ContentPublishingResult, ContentPublishingOperationStatus> published = await _publishingService.PublishAsync(
            content.Key,
            [new CulturePublishScheduleModel()],
            Constants.Security.SuperUserKey);
        if (published.Success is false)
        {
            throw new InvalidOperationException($"Publishing '{content.Name}' failed: {published.Status}.");
        }
    }

    private async Task<IDataType> EnsurePickerAsync(string name, Guid allowedContentTypeKey, bool single)
    {
        IDataType? existing = await _dataTypeService.GetAsync(name);
        if (existing is not null)
        {
            Logger.LogInformation("Data type {Name} already exists; leaving its configuration unchanged.", name);
            return existing;
        }

        if (_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.MultiNodeTreePicker, out IDataEditor? editor) is false || editor is null)
        {
            throw new InvalidOperationException("The Multi Node Tree Picker editor is not registered.");
        }

        var dataType = new DataType(editor, _serializer)
        {
            Name = name,
            EditorUiAlias = PickerEditorUiAlias,
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = new Dictionary<string, object>
            {
                ["startNode"] = new Dictionary<string, object> { ["type"] = "content" },
                ["minNumber"] = 0,
                ["maxNumber"] = single ? 1 : 0,
                ["filter"] = allowedContentTypeKey.ToString(),
                ["ignoreUserStartNodes"] = false,
            },
        };

        Attempt<IDataType, DataTypeOperationStatus> created = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating data type '{name}' failed: {created.Status}.");
        }

        return created.Result;
    }

    private async Task<ITemplate> EnsureTemplateAsync(string alias, string name)
    {
        ITemplate? existing = await _templateService.GetAsync(alias);
        if (existing is not null)
        {
            return existing;
        }

        Attempt<ITemplate, TemplateOperationStatus> created = await _templateService.CreateAsync(
            name,
            alias,
            content: null,
            Constants.Security.SuperUserKey);
        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating template '{alias}' failed: {created.Status}.");
        }

        return created.Result;
    }

    private async Task SaveNewContentTypeAsync(IContentType contentType)
    {
        Attempt<ContentTypeOperationStatus> result = await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Creating document type '{contentType.Alias}' failed: {result.Result}.");
        }
    }

    private async Task SaveContentTypeAsync(IContentType contentType)
    {
        Attempt<ContentTypeOperationStatus> result = await _contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Updating document type '{contentType.Alias}' failed: {result.Result}.");
        }
    }

    private static bool AllowChild(IContentType parent, IContentType child)
    {
        List<ContentTypeSort> allowed = parent.AllowedContentTypes?.ToList() ?? [];
        if (allowed.Any(item => item.Alias.Equals(child.Alias, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        allowed.Add(new ContentTypeSort(child.Key, allowed.Count, child.Alias));
        parent.AllowedContentTypes = allowed;
        return true;
    }

    private async Task<IDataType> RequiredTextAsync()
        => await _dataTypeService.GetAsync(Constants.DataTypes.Guids.TextstringGuid)
           ?? throw new InvalidOperationException("The Textstring data type was not found.");

    private async Task<IDataType> RequiredTextAreaAsync()
        => await _dataTypeService.GetAsync(Constants.DataTypes.Guids.TextareaGuid)
           ?? throw new InvalidOperationException("The Textarea data type was not found.");

    private IContentType RequiredContentType(string alias)
        => _contentTypeService.Get(alias)
           ?? throw new InvalidOperationException($"Document type '{alias}' is missing, so featured topics could not be added.");

    private void AddProperty(
        IContentType contentType,
        IDataType dataType,
        string alias,
        string name,
        int sortOrder,
        string description,
        string groupAlias = BlogAliases.PropertyGroup,
        string groupName = "Content",
        PropertyGroupType groupType = PropertyGroupType.Group,
        bool mandatory = false)
    {
        var property = new PropertyType(_shortStringHelper, dataType, alias)
        {
            Name = name,
            SortOrder = sortOrder,
            Mandatory = mandatory,
            Description = description,
            DataTypeKey = dataType.Key,
        };

        if (contentType.AddPropertyType(property, groupAlias, groupName) is false)
        {
            throw new InvalidOperationException($"Could not add property '{alias}' to document type '{contentType.Alias}'.");
        }

        PropertyGroup? group = contentType.PropertyGroups.FirstOrDefault(item => item.Alias == groupAlias);
        if (group is not null)
        {
            group.Type = groupType;
        }
    }
}
