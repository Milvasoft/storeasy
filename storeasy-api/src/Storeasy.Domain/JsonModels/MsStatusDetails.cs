namespace Storeasy.Domain.JsonModels;

/// <summary>
/// Errors, warnings and certification reports of a Microsoft Store submission (<c>statusDetails</c>).
/// </summary>
public class MsStatusDetails
{
    /// <summary>
    /// Errors.
    /// </summary>
    public List<MsStatusItem> Errors { get; set; } = [];

    /// <summary>
    /// Warnings.
    /// </summary>
    public List<MsStatusItem> Warnings { get; set; } = [];

    /// <summary>
    /// Certification reports.
    /// </summary>
    public List<MsCertificationReport> CertificationReports { get; set; } = [];
}
