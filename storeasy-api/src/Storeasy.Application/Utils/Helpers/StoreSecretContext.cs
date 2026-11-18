namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Associated data strings that bind sealed secrets to their owner, so a sealed value copied to another organization's
/// row does not open. One place, used by every protect and unprotect call.
/// </summary>
public static class StoreSecretContext
{
    /// <summary>
    /// Associated data of a store connection secret.
    /// </summary>
    public static string Connection(int organizationId, StoreKind store) => $"StoreConnection:{organizationId}:{store}";
}
