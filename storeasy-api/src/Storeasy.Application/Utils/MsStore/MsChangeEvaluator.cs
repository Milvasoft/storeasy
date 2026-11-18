using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.Assets;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Repositories <see cref="MsChangeEvaluator"/> reads.
/// </summary>
public sealed class MsChangeRepositories
{
    /// <summary>
    /// Submissions.
    /// </summary>
    public IStoreasyRepositoryBase<MsSubmission> Submissions { get; init; }

    /// <summary>
    /// Flights.
    /// </summary>
    public IStoreasyRepositoryBase<MsFlight> Flights { get; init; }

    /// <summary>
    /// Add-ons.
    /// </summary>
    public IStoreasyRepositoryBase<MsAddon> Addons { get; init; }

    /// <summary>
    /// Assets.
    /// </summary>
    public IStoreasyRepositoryBase<Asset> Assets { get; init; }

    /// <summary>
    /// Listing limits of the rule catalog (<see cref="MsListingLimits.From"/>).
    /// </summary>
    public MsListingLimits Limits { get; init; } = MsListingLimits.Default;
}

/// <summary>
/// Result of <see cref="MsChangeEvaluator.EvaluateAsync"/>: either an error key, or the plan with what it was made from.
/// </summary>
public sealed class MsChangeEvaluation
{
    /// <summary>
    /// Message key when the change cannot even be planned (wrong product kind, unknown flight, invalid file).
    /// </summary>
    public string ErrorKey { get; init; }

    /// <summary>
    /// The plan.
    /// </summary>
    public MsChangePlanDto Plan { get; init; }

    /// <summary>
    /// Store content the plan was made from.
    /// </summary>
    public MsContentDto Content { get; init; }

    /// <summary>
    /// Planning context.
    /// </summary>
    public MsPlanContext Context { get; init; }

    /// <summary>
    /// What was read (encoded into the change plan for the approval's second read).
    /// </summary>
    public MsContentReader.Request Read { get; init; }
}

/// <summary>
/// Plans one Microsoft Store change end to end: checks what only the database knows (product kind, flight and add-on
/// ownership, uploaded files), reads the store content and runs <see cref="MsChangePlanner"/>. The planning request and
/// the apply job both call it, so the job's second plan is made exactly like the first one.
/// </summary>
public static class MsChangeEvaluator
{
    /// <summary>
    /// Plans <paramref name="change"/> for <paramref name="product"/>. Store failures surface as
    /// <see cref="StoreCallException"/>.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="secretJson"></param>
    /// <param name="organizationId"></param>
    /// <param name="product"></param>
    /// <param name="change"></param>
    /// <param name="repositories"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsChangeEvaluation> EvaluateAsync(IMicrosoftStoreClient client,
                                                               string secretJson,
                                                               int organizationId,
                                                               MsProductContext product,
                                                               MsChangeInputDto change,
                                                               MsChangeRepositories repositories,
                                                               CancellationToken cancellationToken)
    {
        if (!MsChangePlanner.Supports(product.Kind, change.Kind))
            return new MsChangeEvaluation { ErrorKey = MessageKey.MsWrongProductKind };

        if (!string.IsNullOrEmpty(change.FlightId) && change.Kind is MsChangeKind.Packages or MsChangeKind.Rollout or MsChangeKind.DiscardSubmission)
        {
            var flight = await repositories.Flights.GetFirstOrDefaultAsync(f => f.StoreAppId == product.StoreAppId && f.FlightId == change.FlightId, projection: f => (int?)f.Id, cancellationToken: cancellationToken);

            if (!flight.HasValue)
                return new MsChangeEvaluation { ErrorKey = MessageKey.MsFlightNotFound };
        }

        if (change.Kind == MsChangeKind.AddonContent || (change.Kind == MsChangeKind.DiscardSubmission && !string.IsNullOrEmpty(change.AddonStoreId)))
        {
            var addon = await repositories.Addons.GetFirstOrDefaultAsync(a => a.StoreAppId == product.StoreAppId && a.StoreId == change.AddonStoreId, projection: a => (int?)a.Id, cancellationToken: cancellationToken);

            if (!addon.HasValue)
                return new MsChangeEvaluation { ErrorKey = MessageKey.MsAddonNotFound };
        }

        IReadOnlyDictionary<long, string> assetNames = new Dictionary<long, string>();

        if (change.Kind is MsChangeKind.Images or MsChangeKind.Packages)
        {
            var packages = change.Kind == MsChangeKind.Packages;
            var ids = packages ? change.Packages.AddAssetIds ?? [] : [.. (change.Images.Add ?? []).Select(i => i.AssetId)];

            assetNames = await repositories.Assets.GetMsZipEntryNamesAsync(organizationId, ids, packages, cancellationToken);

            if (assetNames == null)
                return new MsChangeEvaluation { ErrorKey = MessageKey.MsAssetInvalid };
        }

        var target = MsChangePlanner.TargetOf(product.Kind, change);
        var read = new MsContentReader.Request
                                                      {
                                                          Kind = product.Kind,
                                                          ProductId = product.ProductId,
                                                          Target = target,
                                                          FlightId = change.FlightId,
                                                          AddonStoreId = change.AddonStoreId,
                                                          RolloutSubmissionId = change.Kind == MsChangeKind.Rollout ? change.Rollout.SubmissionId : null,
                                                      };
        var content = await MsContentReader.ReadAsync(client, secretJson, read, cancellationToken);

        var context = await repositories.Submissions.GetMsPlanContextAsync(repositories.Flights,
                                                                           repositories.Addons,
                                                                           product,
                                                                           target,
                                                                           content.SubmissionIsPending ? content.SubmissionId : null,
                                                                           change.AddonStoreId,
                                                                           assetNames,
                                                                           repositories.Limits,
                                                                           cancellationToken);

        return new MsChangeEvaluation
        {
            Plan = MsChangePlanner.Plan(product.StoreAppId, content, change, context),
            Content = content,
            Context = context,
            Read = read,
        };
    }
}
