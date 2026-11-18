namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.MsSync"/> job.
/// </summary>
/// <param name="ConnectionId">Microsoft connection to read with.</param>
public sealed record MsSyncPayload(int ConnectionId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.MsApplyChange"/> job: the change and the fingerprint of the content it was planned on.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
/// <param name="BaseHash">Fingerprint of the planned content; the job refuses to write when the store content changed.</param>
/// <param name="Change">The change.</param>
public sealed record MsApplyChangePayload(int StoreAppId, string BaseHash, MsChangeInputDto Change);

/// <summary>
/// Payload of a <see cref="StoreJobKind.MsSubmit"/> job.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
/// <param name="Target">What is submitted.</param>
/// <param name="FlightId">Flight of a flight submission.</param>
/// <param name="AddonStoreId">Add-on of an add-on submission.</param>
/// <param name="SubmissionId">Pending submission confirmed by the user (MSIX).</param>
public sealed record MsSubmitPayload(int StoreAppId, MsSubmissionTarget Target, string FlightId, string AddonStoreId, string SubmissionId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.MsCheckInstaller"/> job.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
/// <param name="PackageUrls">Installer addresses to check; empty for every package of the product.</param>
public sealed record MsCheckInstallerPayload(int StoreAppId, List<string> PackageUrls);
