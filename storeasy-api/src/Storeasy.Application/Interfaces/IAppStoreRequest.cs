namespace Storeasy.Application.Interfaces;

/// <summary>
/// A request about one app in one store.
/// </summary>
public interface IAppStoreRequest
{
    /// <summary>
    /// App id.
    /// </summary>
    int AppId { get; }

    /// <summary>
    /// Store.
    /// </summary>
    StoreKind Store { get; }
}
