using Storeasy.Application.Features.MsStore.ApplyMsChange;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// The Microsoft Store side of the shared content engine (<see cref="IStoreContentGateway"/>), used by change plans of kind
/// <see cref="ChangePlanKind.MsChange"/>:
/// <list type="bullet">
/// <item>Read: the plan's version id carries the encoded read (<see cref="MsContentReader.Encode"/>); the snapshot holds
/// the fingerprint of that content under <see cref="ApplyMsChangeCommandHandler.BaseHashKey"/>, so the shared approval
/// compares it with the fingerprint the plan was made on. Without an encoded read the snapshot carries the listing
/// languages only.</item>
/// <item>Apply: the change set's Microsoft part is written by <see cref="MsChangeApplier"/>, which plans again and refuses a
/// changed fingerprint.</item>
/// </list>
/// </summary>
/// <param name="microsoftStoreClient"></param>
/// <param name="applier"></param>
public sealed class MicrosoftContentGateway(IMicrosoftStoreClient microsoftStoreClient, MsChangeApplier applier) : IStoreContentGateway
{
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly MsChangeApplier _applier = applier;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Microsoft;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Microsoft, ExternalId = target.ExternalId, VersionId = target.VersionId };

        if (!MsContentReader.TryDecode(target.VersionId, target.ExternalId, out var request, out var kind))
            return snapshot;

        var content = await MsContentReader.ReadAsync(_microsoftStoreClient, target.SecretJson, request, cancellationToken);

        snapshot.Values[ApplyMsChangeCommandHandler.BaseHashKey] = MsChangePlanner.BaseHash(content, kind);
        snapshot.Locales = [.. content.Listings.Select(l => l.Locale)];

        return snapshot;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreOption>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        if (changes.Microsoft == null)
            throw new StoreJobException("The plan has no Microsoft Store change.", transient: false);

        return _applier.ApplyAsync(context, target.SecretJson, target.ExternalId, changes.Microsoft, cancellationToken);
    }
}
