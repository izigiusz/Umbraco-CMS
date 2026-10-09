using Microsoft.AspNetCore.DataProtection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Web.UI.Security;
using Umbraco.Extensions;

// Key files name PlaintextXmlDecryptor with the assembly version from the build that wrote them.
// Later deploys ship a new version, so resolve any version of this site assembly to the one loaded now.
AppDomain.CurrentDomain.AssemblyResolve += (_, args) => PlaintextXmlDecryptor.ResolveCurrentSiteAssembly(args);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
#if UseDeliveryApi
    .AddDeliveryApi()
#endif
    .AddComposers()
    .Build();

// Persist the key ring under umbraco/Data, which the FTP deploy never deletes.
// OpenIddict uses ASP.NET Core Data Protection for access and refresh tokens
// (its per-start signing certificates are unused while UseDataProtection is on),
// and the backoffice auth cookie is protected with the same key ring.
var dataProtectionKeys = Path.Combine(
    builder.Environment.MapPathContentRoot(Constants.SystemDirectories.Data),
    "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeys);
builder.Services
    .AddDataProtection()
    .SetApplicationName("Umbraco.Web.UI")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeys))
    .AddKeyManagementOptions(options => options.XmlEncryptor = new PlaintextXmlEncryptor());

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

// Detailed exceptions stay on the Development environment only.
// Production keeps the host's generic empty 500 and does not leak compiler output.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

#if UseHttpsRedirect
app.UseHttpsRedirection();
#endif

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        /*#if (UmbracoRelease = 'LTS')
        u.UseInstallerEndpoints();
        #endif */
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
