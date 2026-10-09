using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Umbraco.Cms.Web.UI.Security;

/// <summary>
/// Writes data-protection keys without DPAPI.
/// Shared IIS with LoadUserProfile=false cannot use the Windows user profile, so DPAPI
/// either throws or produces keys that disappear on the next recycle.
/// </summary>
public sealed class PlaintextXmlEncryptor : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        return new EncryptedXmlInfo(plaintextElement, typeof(PlaintextXmlDecryptor));
    }
}

/// <summary>
/// Returns key XML that was stored without encryption.
/// </summary>
/// <remarks>
/// Key files record this type's assembly-qualified name. Nerdbank.GitVersioning changes
/// <c>Umbraco.Web.UI</c>'s assembly version every build, so <see cref="ResolveCurrentSiteAssembly"/>
/// maps older versions back to the assembly that is actually loaded.
/// </remarks>
public sealed class PlaintextXmlDecryptor : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        return encryptedElement;
    }

    public static Assembly? ResolveCurrentSiteAssembly(ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name);
        Assembly current = typeof(PlaintextXmlDecryptor).Assembly;
        if (!string.Equals(requested.Name, current.GetName().Name, StringComparison.Ordinal))
        {
            return null;
        }

        return current;
    }
}
