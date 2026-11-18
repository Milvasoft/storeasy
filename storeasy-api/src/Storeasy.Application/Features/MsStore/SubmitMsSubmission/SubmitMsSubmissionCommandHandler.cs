using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.SubmitMsSubmission;

/// <summary>
/// Handles <see cref="SubmitMsSubmissionCommand"/>. Everything is decided from storeasy's rows (kept current by the sync
/// and by storeasy's own writes); the job reads the store again before it commits.
/// </summary>
/// <param name="ProductRepository"></param>
/// <param name="SubmissionRepository"></param>
/// <param name="FlightRepository"></param>
/// <param name="AddonRepository"></param>
/// <param name="PackageRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SubmitMsSubmissionCommandHandler(IStoreasyRepositoryBase<MsProduct> ProductRepository,
                                               IStoreasyRepositoryBase<MsSubmission> SubmissionRepository,
                                               IStoreasyRepositoryBase<MsFlight> FlightRepository,
                                               IStoreasyRepositoryBase<MsAddon> AddonRepository,
                                               IStoreasyRepositoryBase<MsInstallerPackage> PackageRepository,
                                               IStoreJobQueue StoreJobQueue,
                                               IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SubmitMsSubmissionCommand, long>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = ProductRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = SubmissionRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = FlightRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = AddonRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = PackageRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SubmitMsSubmissionCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetMsProductAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (product == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (!string.Equals(request.Confirm?.Trim(), product.ProductId, StringComparison.OrdinalIgnoreCase))
            return Response<long>.Error(0, MessageKey.MsConfirmationMismatch);

        var target = product.Kind == MsProductKind.MsiExe ? MsSubmissionTarget.Installer : request.Target;

        if (product.Kind == MsProductKind.Msix && target == MsSubmissionTarget.Installer)
            return Response<long>.Error(0, MessageKey.MsWrongProductKind);

        var (errorKey, submissionId) = target == MsSubmissionTarget.Installer
            ? (await CheckInstallerProductAsync(product, cancellationToken), null)
            : await CheckDraftAsync(product, target, request, cancellationToken);

        if (errorKey != null)
            return Response<long>.Error(0, errorKey);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId,
                                                                            StoreJobKind.MsSubmit,
                                                                            new MsSubmitPayload(product.StoreAppId, target, request.FlightId, request.AddonStoreId, submissionId),
                                                                            StoreKind.Microsoft,
                                                                            product.AppId,
                                                                            MaxAttempts: 1)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MsSubmitted,
                                     product.StoreAppId.ToString(),
                                     $"Microsoft Store {target} submission of {product.ProductId} sent ({submissionId ?? "draft"}).",
                                     StoreKind.Microsoft,
                                     new { target, request.FlightId, request.AddonStoreId, submissionId, JobId = jobIds[0] },
                                     cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }

    private async Task<(string ErrorKey, string SubmissionId)> CheckDraftAsync(MsProductContext product, MsSubmissionTarget target, SubmitMsSubmissionCommand request, CancellationToken cancellationToken)
    {
        string pendingId;

        switch (target)
        {
            case MsSubmissionTarget.Flight:
            {
                var flight = await _flightRepository.GetFirstOrDefaultAsync(f => f.StoreAppId == product.StoreAppId && f.FlightId == request.FlightId,
                                                                            projection: f => new MsFlight { Id = f.Id, PendingSubmissionId = f.PendingSubmissionId },
                                                                            cancellationToken: cancellationToken);

                if (flight == null)
                    return (MessageKey.MsFlightNotFound, null);

                pendingId = flight.PendingSubmissionId;
                break;
            }
            case MsSubmissionTarget.Addon:
            {
                var addon = await _addonRepository.GetFirstOrDefaultAsync(a => a.StoreAppId == product.StoreAppId && a.StoreId == request.AddonStoreId,
                                                                          projection: a => new MsAddon { Id = a.Id, PendingSubmissionId = a.PendingSubmissionId, ApiWritable = a.ApiWritable },
                                                                          cancellationToken: cancellationToken);

                if (addon == null)
                    return (MessageKey.MsAddonNotFound, null);

                if (!addon.ApiWritable)
                    return (MessageKey.MsAddonAssisted, null);

                pendingId = addon.PendingSubmissionId;
                break;
            }
            default:
                if (product.HasMandatoryUpdate)
                    return (MessageKey.MsApiRefused, null);

                pendingId = product.PendingSubmissionId;
                break;
        }

        if (string.IsNullOrEmpty(pendingId))
            return (MessageKey.MsNoPendingSubmission, null);

        var row = await _submissionRepository.GetFirstOrDefaultAsync(s => s.StoreAppId == product.StoreAppId && s.Target == target && s.ExternalId == pendingId,
                                                                     projection: s => new MsSubmission { Id = s.Id, Status = s.Status, CreatedByStoreasy = s.CreatedByStoreasy, ModifiedOutsideApi = s.ModifiedOutsideApi },
                                                                     cancellationToken: cancellationToken);

        if (row?.ModifiedOutsideApi == true)
            return (MessageKey.MsSubmissionModifiedOutsideApi, null);

        if (row == null || !row.CreatedByStoreasy)
            return (MessageKey.MsSubmissionNotOurs, null);

        if (row.Status != MsSubmissionStatus.PendingCommit)
            return (MessageKey.MsSubmissionInProgress, null);

        return (null, pendingId);
    }

    private async Task<string> CheckInstallerProductAsync(MsProductContext product, CancellationToken cancellationToken)
    {
        var inProgress = await _submissionRepository.GetFirstOrDefaultAsync(s => s.StoreAppId == product.StoreAppId && s.Target == MsSubmissionTarget.Installer && s.IsPending && s.Status == MsSubmissionStatus.InProgress,
                                                                            projection: s => (long?)s.Id,
                                                                            cancellationToken: cancellationToken);

        if (inProgress.HasValue)
            return MessageKey.MsSubmissionInProgress;

        var signatures = await _packageRepository.GetAllAsync(p => p.StoreAppId == product.StoreAppId,
                                                              projection: p => new MsInstallerPackage { Id = p.Id, SignatureStatus = p.SignatureStatus },
                                                              cancellationToken: cancellationToken);

        if (signatures.Count == 0)
            return MessageKey.MsInstallerNotReady;

        // The Store rejects installers without an Authenticode signature; storeasy does not send what it knows will fail.
        return signatures.All(p => p.SignatureStatus == MsSignatureStatus.Signed) ? null : MessageKey.MsInstallerNotSigned;
    }
}
