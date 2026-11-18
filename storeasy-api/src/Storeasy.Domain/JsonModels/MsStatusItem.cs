namespace Storeasy.Domain.JsonModels;

/// <summary>
/// An error or warning of a Microsoft Store submission (<c>statusDetails.errors[]</c>, <c>statusDetails.warnings[]</c>,
/// or an MSI/EXE <c>errors[]</c> entry).
/// </summary>
public class MsStatusItem
{
    /// <summary>
    /// Store's code of the issue.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Store's description of the issue (redacted).
    /// </summary>
    public string Details { get; set; }
}
