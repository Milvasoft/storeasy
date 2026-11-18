namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Associated data of the secrets sealed in saved answers (the demo account password), so a sealed value copied to
/// another app or organization does not open.
/// </summary>
public static class SavedAnswerSecrets
{
    /// <summary>
    /// Associated data of the secret of app <paramref name="appId"/> in <paramref name="organizationId"/>.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static string Context(int organizationId, int appId) => $"SavedAnswer:{organizationId}:{appId}";
}
