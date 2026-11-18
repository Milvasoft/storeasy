using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaLocalization;

/// <summary>
/// Handles <see cref="UpdateBetaLocalizationCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateBetaLocalizationCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateBetaLocalizationCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(UpdateBetaLocalizationCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.Localization,
            Locale = request.Locale.Trim(),
            Text = request.Description,
            FeedbackEmail = request.FeedbackEmail?.Trim(),
            MarketingUrl = request.MarketingUrl?.Trim(),
            PrivacyPolicyUrl = request.PrivacyPolicyUrl?.Trim(),
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, storeApp.AppId, new UpdateTestFlightPayload(storeApp.Id, change, null), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaTextChanged, storeApp.Id.ToString(), $"Beta app texts in {change.Locale} written.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
