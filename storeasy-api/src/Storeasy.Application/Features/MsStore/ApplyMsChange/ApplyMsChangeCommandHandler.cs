using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Assets;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.MsStore.ApplyMsChange;

/// <summary>
/// Handles <see cref="ApplyMsChangeCommand"/>. Reads the store inside the request (the secret is opened only for these
/// reads), plans with <see cref="MsChangeEvaluator"/> and, when applying, checks the plan's own permission against the
/// caller's role, refuses blocked plans and stale hashes, queues the <see cref="StoreJobKind.MsApplyChange"/> job and
/// writes the audit entry. The job plans again and refuses when the store content changed in between.
/// </summary>
/// <param name="ProductRepository"></param>
/// <param name="ConnectionRepository"></param>
/// <param name="SubmissionRepository"></param>
/// <param name="FlightRepository"></param>
/// <param name="AddonRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="MicrosoftStoreClient"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="Localizer"></param>
[Log]
public record ApplyMsChangeCommandHandler(IStoreasyRepositoryBase<MsProduct> ProductRepository,
                                          IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                          IStoreasyRepositoryBase<MsSubmission> SubmissionRepository,
                                          IStoreasyRepositoryBase<MsFlight> FlightRepository,
                                          IStoreasyRepositoryBase<MsAddon> AddonRepository,
                                          IStoreasyRepositoryBase<Asset> AssetRepository,
                                          ISecretProtector SecretProtector,
                                          IMicrosoftStoreClient MicrosoftStoreClient,
                                          IOrganizationAccess OrganizationAccess,
                                          IStoreJobQueue StoreJobQueue,
                                          IAuditTrail AuditTrail,
                                          IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<ApplyMsChangeCommand, MsChangePlanDto>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = ProductRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = SubmissionRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = FlightRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = AddonRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = MicrosoftStoreClient;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<MsChangePlanDto>> Handle(ApplyMsChangeCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetMsProductAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (product == null)
            return Response<MsChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var secret = await _connectionRepository.OpenMsSecretAsync(_secretProtector, request.OrganizationId, product.ConnectionId, cancellationToken);

        if (secret == null)
            return Response<MsChangePlanDto>.Error(null, MessageKey.StoreConnectionNotFound);

        MsChangeEvaluation evaluation;

        try
        {
            evaluation = await MsChangeEvaluator.EvaluateAsync(_microsoftStoreClient,
                                                               secret,
                                                               request.OrganizationId,
                                                               product,
                                                               request.Change,
                                                               new MsChangeRepositories { Submissions = _submissionRepository, Flights = _flightRepository, Addons = _addonRepository, Assets = _assetRepository },
                                                               cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return MsResponses.StoreFailure<MsChangePlanDto>(ex);
        }

        if (evaluation.ErrorKey != null)
            return Response<MsChangePlanDto>.Error(null, evaluation.ErrorKey);

        var plan = evaluation.Plan;

        foreach (var warning in plan.Warnings)
            warning.Message = _localizer[warning.Key];

        if (request.DryRun)
            return Response<MsChangePlanDto>.Success(plan);

        if (!OrgRoleRules.Allows(_organizationAccess.CurrentRole.GetValueOrDefault(OrgRole.Viewer), plan.RequiredPermission))
            return Response<MsChangePlanDto>.Error(plan, MessageKey.OrganizationForbidden);

        if (plan.Blocked)
            return Response<MsChangePlanDto>.Error(plan, plan.BlockKey);

        if (!string.Equals(plan.BaseHash, request.BaseHash, StringComparison.Ordinal))
            return Response<MsChangePlanDto>.Error(plan, MessageKey.MsPlanStale);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId,
                                                                            StoreJobKind.MsApplyChange,
                                                                            new MsApplyChangePayload(product.StoreAppId, plan.BaseHash, request.Change),
                                                                            StoreKind.Microsoft,
                                                                            product.AppId)], cancellationToken);

        plan.DryRun = false;
        plan.JobId = jobIds[0];

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MsChangeQueued,
                                     product.StoreAppId.ToString(),
                                     $"Microsoft Store change {request.Change.Kind} of {product.ProductId} queued ({plan.Items.Count} items).",
                                     StoreKind.Microsoft,
                                     new { plan.Kind, plan.Target, plan.SubmissionId, plan.Items, plan.JobId },
                                     cancellationToken);

        return Response<MsChangePlanDto>.Success(plan);
    }
}
