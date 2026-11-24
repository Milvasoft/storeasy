using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Package intake (docs/urun-plani.md 11.8, tools/CONTRACT.md 3): opens, resumes and deduplicates upload sessions,
/// completes them and queues their processing job, reads them, and lets a member deliver a held package or delete a stored
/// one. Shared by the REST endpoints, the pull webhooks and MCP. The caller's identity (member, CI token, upload token) is
/// read from the request; every query is filtered by organization and, for CI and upload tokens, by their apps.
/// </summary>
public interface IArtifactIntakeService
{
    /// <summary>
    /// Opens a session, resumes the open one of the same file, or records a duplicate.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ArtifactUploadDto>> OpenAsync(ArtifactOpenRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the multipart upload and queues processing. Idempotent: a session past uploading is returned as is.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="sessionId"></param>
    /// <param name="parts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ArtifactSessionListDto>> CompleteAsync(int organizationId, long sessionId, IReadOnlyList<UploadedPart> parts, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one session; null when it is not visible to the caller.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="sessionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ArtifactSessionDetailDto> GetAsync(int organizationId, long sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a package a pull source announced (EAS, generic webhook) and queues its download and processing. A file
    /// already received for the app becomes a duplicate session.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ArtifactUploadDto>> AcceptPulledAsync(ArtifactPullRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Records a package storeasy itself was asked to fetch (a finished EAS build a member sent to the intake) and queues
    /// its download and processing. The download is limited to the hosts configured for <paramref name="sourceKind"/>.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="fileName"></param>
    /// <param name="downloadUrl">Address to download from (already checked against the allowed hosts).</param>
    /// <param name="sourceKind"></param>
    /// <param name="sourceRef"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ArtifactUploadDto>> AcceptDownloadAsync(int organizationId, int appId, string fileName, string downloadUrl, ArtifactSource sourceKind, ArtifactSourceRef sourceRef, CancellationToken cancellationToken);

    /// <summary>
    /// Delivers a held session by hand (the member's permission for the target is checked).
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="sessionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ArtifactSessionDetailDto>> DeliverHeldAsync(int organizationId, long sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the stored file of a session that is not being processed.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="sessionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<long>> DeletePackageAsync(int organizationId, long sessionId, CancellationToken cancellationToken);
}

/// <summary>
/// Opening of an upload session.
/// </summary>
/// <param name="OrganizationId">Organization.</param>
/// <param name="AppId">App.</param>
/// <param name="FileName">File name.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Sha256">Lower case hex SHA-256.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Source">Source.</param>
/// <param name="SourceRef">CI details.</param>
/// <param name="Delivery">Target override.</param>
public sealed record ArtifactOpenRequest(int OrganizationId,
                                         int AppId,
                                         string FileName,
                                         long Size,
                                         string Sha256,
                                         string ContentType,
                                         ArtifactSource Source,
                                         ArtifactSourceRef SourceRef,
                                         ArtifactDeliveryOverride Delivery);

/// <summary>
/// A package announced by a pull source.
/// </summary>
/// <param name="Source">The pull source row.</param>
/// <param name="FileName">File name.</param>
/// <param name="DownloadUrl">Address to download from (already checked against the allowed domains).</param>
/// <param name="Size">Size, when announced.</param>
/// <param name="Sha256">SHA-256, when announced.</param>
/// <param name="SourceKind">Source kind of the session.</param>
/// <param name="SourceRef">CI details.</param>
public sealed record ArtifactPullRequest(ArtifactPullSource Source,
                                         string FileName,
                                         string DownloadUrl,
                                         long? Size,
                                         string Sha256,
                                         ArtifactSource SourceKind,
                                         ArtifactSourceRef SourceRef);
