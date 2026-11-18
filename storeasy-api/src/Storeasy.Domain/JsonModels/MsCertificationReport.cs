namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A certification report of a Microsoft Store submission. The report opens in the user's Partner Center session.
/// </summary>
public class MsCertificationReport
{
    /// <summary>
    /// UTC instant the report was generated.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Address of the report.
    /// </summary>
    public string ReportUrl { get; set; }
}
