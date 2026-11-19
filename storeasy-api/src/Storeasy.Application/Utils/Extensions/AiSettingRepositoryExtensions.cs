using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Organizations;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Reads the organization's AI settings for a call to Anthropic: the key in clear (for the call only) and the chosen models.
/// Shared by every AI feature so the key is opened the same way everywhere.
/// </summary>
public static class AiSettingRepositoryExtensions
{
    /// <summary>
    /// The organization's AI settings with the key opened, or null when no key is saved. A sealed value that no longer
    /// opens (a master key that was removed) counts as no key: it has to be saved again.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="secretProtector"></param>
    /// <param name="organizationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<OpenedAiSetting> OpenAiSettingAsync(this IStoreasyRepositoryBase<OrganizationAiSetting> repository,
                                                                 ISecretProtector secretProtector,
                                                                 int organizationId,
                                                                 CancellationToken cancellationToken)
    {
        var setting = await repository.GetFirstOrDefaultAsync(s => s.OrganizationId == organizationId,
                                                              projection: s => new OrganizationAiSetting
                                                              {
                                                                  EncryptedApiKey = s.EncryptedApiKey,
                                                                  EncryptedDataKey = s.EncryptedDataKey,
                                                                  KeyVersion = s.KeyVersion,
                                                                  ReplyDraftModel = s.ReplyDraftModel,
                                                                  PreReviewModel = s.PreReviewModel,
                                                              },
                                                              cancellationToken: cancellationToken);

        if (setting == null)
            return null;

        try
        {
            var plaintext = secretProtector.Unprotect(new ProtectedSecret(setting.EncryptedApiKey, setting.EncryptedDataKey, setting.KeyVersion), SecretScopes.AiProviderKey(organizationId));

            return new OpenedAiSetting(Encoding.UTF8.GetString(plaintext), setting.ReplyDraftModel, setting.PreReviewModel);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the organization has a key saved (the key is not opened).
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="organizationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool> HasAiKeyAsync(this IStoreasyRepositoryBase<OrganizationAiSetting> repository, int organizationId, CancellationToken cancellationToken)
        => await repository.GetFirstOrDefaultAsync(s => s.OrganizationId == organizationId, projection: s => (int?)s.Id, cancellationToken: cancellationToken) != null;
}

/// <summary>
/// The organization's AI settings with the key in clear. Keep it in memory for one call only.
/// </summary>
/// <param name="ApiKey">The Anthropic API key.</param>
/// <param name="ReplyDraftModel">Chosen model of the reply drafts; null for the default.</param>
/// <param name="PreReviewModel">Chosen model of the pre-reviews; null for the default.</param>
public sealed record OpenedAiSetting(string ApiKey, string ReplyDraftModel, string PreReviewModel)
{
    /// <summary>
    /// Masks the key so the record never prints it.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"OpenedAiSetting {{ ReplyDraftModel = {this.ReplyDraftModel}, PreReviewModel = {this.PreReviewModel} }}";
}
