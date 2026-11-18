using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.MsStore.ApplyMsChange;

/// <summary>
/// Handles <see cref="ApplyMsChangeCommand"/>. Reads the store inside the request (the secret is opened only for these
/// reads) and plans with <see cref="MsChangeEvaluator"/>. A plan that is not blocked is saved as a change plan of kind
/// <see cref="ChangePlanKind.MsChange"/> through <see cref="IChangePlanService"/>: a dry run leaves it pending (approve it
/// with the shared plan endpoints); applying approves it right away with the same rules (the plan's permission, a second
/// store read through the Microsoft content gateway compared by fingerprint) and queues the
/// <see cref="StoreJobKind.ApplyChangePlan"/> job. A blocked plan is never saved; when it is a Partner Center task its
/// assisted step is recorded.
/// </summary>
/// <param name="ProductRepository"></param>
/// <param name="ConnectionRepository"></param>
/// <param name="SubmissionRepository"></param>
/// <param name="FlightRepository"></param>
/// <param name="AddonRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="StepRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="MicrosoftStoreClient"></param>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="RulesCatalog"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="Localizer"></param>
[Log]
public record ApplyMsChangeCommandHandler(IStoreasyRepositoryBase<MsProduct> ProductRepository,
                                          IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                          IStoreasyRepositoryBase<MsSubmission> SubmissionRepository,
                                          IStoreasyRepositoryBase<MsFlight> FlightRepository,
                                          IStoreasyRepositoryBase<MsAddon> AddonRepository,
                                          IStoreasyRepositoryBase<Asset> AssetRepository,
                                          IStoreasyRepositoryBase<AssistedStep> StepRepository,
                                          ISecretProtector SecretProtector,
                                          IMicrosoftStoreClient MicrosoftStoreClient,
                                          IStoreContentService ContentService,
                                          IChangePlanService ChangePlanService,
                                          IRulesCatalog RulesCatalog,
                                          IOrganizationAccess OrganizationAccess,
                                          IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<ApplyMsChangeCommand, MsChangePlanDto>
{
    /// <summary>
    /// Content key of the Microsoft fingerprint in the change plan's base keys.
    /// </summary>
    public const string BaseHashKey = "ms.basehash";

    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = ProductRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = SubmissionRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = FlightRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = AddonRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = StepRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = MicrosoftStoreClient;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
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
                                                               new MsChangeRepositories
                                                               {
                                                                   Submissions = _submissionRepository,
                                                                   Flights = _flightRepository,
                                                                   Addons = _addonRepository,
                                                                   Assets = _assetRepository,
                                                                   Limits = MsListingLimits.From(_rulesCatalog.Current),
                                                               },
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

        plan.DryRun = request.DryRun;

        if (!request.DryRun && !OrgRoleRules.Allows(_organizationAccess.CurrentRole.GetValueOrDefault(OrgRole.Viewer), plan.RequiredPermission))
            return Response<MsChangePlanDto>.Error(plan, MessageKey.OrganizationForbidden);

        if (plan.Blocked)
        {
            // A Partner Center task becomes (or stays) an assisted step of the app.
            await _stepRepository.EnsureMsAssistedStepsAsync(request.OrganizationId, [(product.AppId, MsAssistedSteps.KeyOf(plan))], cancellationToken);

            return request.DryRun ? Response<MsChangePlanDto>.Success(plan) : Response<MsChangePlanDto>.Error(plan, plan.BlockKey);
        }

        if (!request.DryRun && !string.Equals(plan.BaseHash, request.BaseHash, StringComparison.Ordinal))
            return Response<MsChangePlanDto>.Error(plan, MessageKey.MsPlanStale);

        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, product.StoreAppId, cancellationToken);

        if (app == null)
            return Response<MsChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var draft = Draft(plan, request.Change, MsContentReader.Encode(evaluation.Read, request.Change.Kind), product.ProductId);
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Microsoft, ExternalId = product.ProductId, VersionId = draft.VersionId };

        snapshot.Values[BaseHashKey] = plan.BaseHash;

        var saved = await _changePlanService.SaveAsync(app, snapshot, draft, request.DryRun, null, cancellationToken);

        if (!saved.IsSuccess)
        {
            var message = saved.Messages?.FirstOrDefault();

            return Response<MsChangePlanDto>.Error(plan, string.IsNullOrEmpty(message?.Key) ? message?.Message : message.Key);
        }

        plan.PlanId = saved.Data.PlanId;
        plan.JobId = saved.Data.JobId;

        return Response<MsChangePlanDto>.Success(plan);
    }

    /// <summary>
    /// The change plan draft of a Microsoft plan: its diff, remarks and calls, the change itself with its fingerprint, the
    /// encoded read and the plan's own permission.
    /// </summary>
    /// <param name="plan"></param>
    /// <param name="change"></param>
    /// <param name="versionId"></param>
    /// <param name="productId"></param>
    /// <returns></returns>
    public static PlanDraft Draft(MsChangePlanDto plan, MsChangeInputDto change, string versionId, string productId) => new()
    {
        Kind = ChangePlanKind.MsChange,
        Title = $"Microsoft Store {change.Kind} of {productId}",
        Changes = new ContentChangeSet { Microsoft = new MsPlannedChange { Change = change, BaseHash = plan.BaseHash } },
        Items = [.. plan.Items.Select(i => new PlanItem
        {
            Group = i.Group,
            Locale = i.Locale,
            Field = i.Field,
            Before = i.Before,
            After = i.After,
            Action = i.Action switch { "add" => ChangeAction.Create, "remove" => ChangeAction.Delete, _ => ChangeAction.Update },
        })],
        Warnings = [.. plan.Warnings.Select(w => new PlanWarning { Key = w.Key, Message = w.Message, Field = w.Field, Locale = w.Locale })],
        Calls = [.. plan.Calls.Select(c => new PlanCall { Method = c.Method, Path = c.Path, Target = c.Target })],
        BaseKeys = [BaseHashKey],
        VersionId = versionId,
        VersionString = productId,
        RequiredPermission = plan.RequiredPermission,
    };
}
