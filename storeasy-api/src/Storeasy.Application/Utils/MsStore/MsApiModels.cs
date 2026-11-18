using Storeasy.Domain.JsonModels;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// An MSIX app as the submission API reports it (<c>GET applications</c>).
/// </summary>
/// <param name="Id">Store id (9NBLGGH4R315).</param>
/// <param name="PrimaryName">Reserved name.</param>
/// <param name="PackageFamilyName">Package family name.</param>
/// <param name="LastPublishedSubmissionId">Last published app submission, if any.</param>
/// <param name="PendingSubmissionId">Pending app submission, if any.</param>
public sealed record MsApplicationRecord(string Id, string PrimaryName, string PackageFamilyName, string LastPublishedSubmissionId, string PendingSubmissionId);

/// <summary>
/// A package flight as the submission API reports it.
/// </summary>
/// <param name="FlightId">Flight id.</param>
/// <param name="FriendlyName">Name.</param>
/// <param name="GroupIds">Flight group ids.</param>
/// <param name="RankHigherThan">Flight this one ranks above, if any.</param>
/// <param name="LastPublishedSubmissionId">Last published flight submission, if any.</param>
/// <param name="PendingSubmissionId">Pending flight submission, if any.</param>
public sealed record MsFlightRecord(string FlightId, string FriendlyName, IReadOnlyList<string> GroupIds, string RankHigherThan, string LastPublishedSubmissionId, string PendingSubmissionId);

/// <summary>
/// An add-on as the submission API reports it.
/// </summary>
/// <param name="StoreId">Store id of the add-on.</param>
/// <param name="ProductId">Developer's product id.</param>
/// <param name="ProductType">Durable or Consumable.</param>
/// <param name="LastPublishedSubmissionId">Last published add-on submission, if any.</param>
/// <param name="PendingSubmissionId">Pending add-on submission, if any.</param>
public sealed record MsAddonRecord(string StoreId, string ProductId, string ProductType, string LastPublishedSubmissionId, string PendingSubmissionId);

/// <summary>
/// Status of a submission (<c>.../status</c> or the commit answer).
/// </summary>
/// <param name="Status">Status.</param>
/// <param name="Details">Errors, warnings and certification reports.</param>
public sealed record MsStatusRecord(MsSubmissionStatus Status, MsStatusDetails Details);

/// <summary>
/// Gradual package rollout of a submission.
/// </summary>
/// <param name="IsPackageRollout">Whether the submission rolls out gradually.</param>
/// <param name="Percentage">Percentage of customers who get the packages.</param>
/// <param name="Status">Rollout state.</param>
/// <param name="FallbackSubmissionId">Submission the other customers get.</param>
public sealed record MsRolloutRecord(bool IsPackageRollout, double Percentage, MsRolloutStatus Status, string FallbackSubmissionId);

/// <summary>
/// Draft readiness of an MSI/EXE product (<c>GET /status</c>).
/// </summary>
/// <param name="IsReady">Whether every module, package upload included, is ready.</param>
/// <param name="OngoingSubmissionId">Submission in progress, if any.</param>
/// <param name="Errors">Errors the API reported (a <c>packageuploaderror</c> means the package commit failed).</param>
public sealed record MsProductStatusRecord(bool IsReady, string OngoingSubmissionId, IReadOnlyList<MsStatusItem> Errors);

/// <summary>
/// Publishing status of an MSI/EXE submission.
/// </summary>
/// <param name="Status">INPROGRESS, PUBLISHED, FAILED mapped to <see cref="MsSubmissionStatus"/>.</param>
/// <param name="HasFailed">Whether publishing failed and will not be retried.</param>
public sealed record MsPublishingStatusRecord(MsSubmissionStatus Status, bool HasFailed);

/// <summary>
/// An MSI/EXE installer package as the API reads and writes it.
/// </summary>
public sealed class MsInstallerPackageRecord
{
    /// <summary>
    /// Store's package id; null for a new package.
    /// </summary>
    public string PackageId { get; set; }

    /// <summary>
    /// HTTPS address of the installer.
    /// </summary>
    public string PackageUrl { get; set; }

    /// <summary>
    /// Languages of the installer.
    /// </summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>
    /// Architectures (one value: Neutral, X86, X64, Arm, Arm64).
    /// </summary>
    public List<string> Architectures { get; set; } = [];

    /// <summary>
    /// Whether the installer runs silently without parameters.
    /// </summary>
    public bool IsSilentInstall { get; set; }

    /// <summary>
    /// Silent install switches.
    /// </summary>
    public string InstallerParameters { get; set; }

    /// <summary>
    /// Return code documentation (EXE).
    /// </summary>
    public string GenericDocUrl { get; set; }

    /// <summary>
    /// Installer type.
    /// </summary>
    public MsInstallerType PackageType { get; set; }
}

/// <summary>
/// Addresses one submission (or, without <see cref="SubmissionId"/>, the owner a new submission is created for) of the
/// MSIX submission API.
/// </summary>
public sealed class MsSubmissionRef
{
    /// <summary>
    /// What the submission belongs to (app, flight or add-on).
    /// </summary>
    public MsSubmissionTarget Target { get; init; }

    /// <summary>
    /// Store id of the app (apps and flights).
    /// </summary>
    public string ApplicationId { get; init; }

    /// <summary>
    /// Flight id (flight submissions).
    /// </summary>
    public string FlightId { get; init; }

    /// <summary>
    /// Store id of the add-on (add-on submissions).
    /// </summary>
    public string AddonStoreId { get; init; }

    /// <summary>
    /// Submission id; null to address the owner.
    /// </summary>
    public string SubmissionId { get; init; }

    /// <summary>
    /// Relative path of the submission collection of the owner (<c>applications/{id}/submissions</c>,
    /// <c>applications/{id}/flights/{flightId}/submissions</c> or <c>inappproducts/{id}/submissions</c>), followed by the
    /// submission id when set.
    /// </summary>
    /// <returns></returns>
    public string Path()
    {
        var owner = Target switch
        {
            MsSubmissionTarget.Flight => $"applications/{Uri.EscapeDataString(ApplicationId)}/flights/{Uri.EscapeDataString(FlightId)}/submissions",
            MsSubmissionTarget.Addon => $"inappproducts/{Uri.EscapeDataString(AddonStoreId)}/submissions",
            _ => $"applications/{Uri.EscapeDataString(ApplicationId)}/submissions",
        };

        return string.IsNullOrEmpty(SubmissionId) ? owner : $"{owner}/{Uri.EscapeDataString(SubmissionId)}";
    }

    /// <summary>
    /// The same owner with another submission id.
    /// </summary>
    /// <param name="submissionId"></param>
    /// <returns></returns>
    public MsSubmissionRef WithSubmission(string submissionId) => new()
    {
        Target = Target,
        ApplicationId = ApplicationId,
        FlightId = FlightId,
        AddonStoreId = AddonStoreId,
        SubmissionId = submissionId,
    };
}
