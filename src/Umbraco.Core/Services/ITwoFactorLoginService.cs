using Umbraco.Cms.Core.Models;

namespace Umbraco.Cms.Core.Services;

/// <summary>
///     Service handling 2FA logins.
/// </summary>
public interface ITwoFactorLoginService : IService
{
    /// <summary>
    ///     Deletes all user logins - normally used when a member is deleted.
    /// </summary>
    Task DeleteUserLoginsAsync(Guid userOrMemberKey);

    /// <summary>
    ///     Checks whether 2FA is enabled for the user or member with the specified key.
    /// </summary>
    Task<bool> IsTwoFactorEnabledAsync(Guid userOrMemberKey);

    /// <summary>
    ///     Gets the secret for user or member and a specific provider.
    /// </summary>
    Task<string?> GetSecretForUserAndProviderAsync(Guid userOrMemberKey, string providerName);

    /// <summary>
    ///     Gets all registered providers names.
    /// </summary>
    IEnumerable<string> GetAllProviderNames();

    /// <summary>
    ///     Disables the 2FA provider with the specified provider name for the specified user or member.
    /// </summary>
    Task<bool> DisableAsync(Guid userOrMemberKey, string providerName);

    /// <summary>
    ///     Validates the setup of the provider using the secret and code.
    /// </summary>
    bool ValidateTwoFactorSetup(string providerName, string secret, string code);

    /// <summary>
    ///     Saves the 2FA login information.
    /// </summary>
    Task SaveAsync(TwoFactorLogin twoFactorLogin);

    /// <summary>
    /// Gets all the enabled 2FA providers for the user or member with the specified key.
    /// </summary>
    Task<IEnumerable<string>> GetEnabledTwoFactorProviderNamesAsync(Guid userOrMemberKey);
}
