using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// The <see cref="StoreJobKind.MsSync"/> job: reads one Microsoft connection and saves what it sees.
/// <list type="bullet">
/// <item>MSIX apps: every app the Entra application sees (<c>GET applications</c>), each with its app, flight and add-on
/// submissions (pending and last published), flights and add-ons. New apps get their own storeasy app. Every sync
/// writes the package family name to the record's <see cref="StoreApp.BundleId"/> (package intake matches MSIX packages
/// with it); a product added by its Store id that the MSIX API lists becomes an MSIX app.</item>
/// <item>MSI/EXE products: no list API, so the products added by id are read one by one (readiness, ongoing submission,
/// installers). A product the store refuses that was never confirmed is removed with its empty app.</item>
/// </list>
/// Submission rows keep a status history; a submission storeasy created whose content changed since storeasy wrote it is
/// flagged <see cref="MsSubmission.ModifiedOutsideApi"/>. Packages and installers become builds. Writes are bulk.
/// <para>
/// Then the logo of every product is read from the public display catalog (<see cref="IStoreGateway.GetIconUrlAsync"/>)
/// and written to its storeasy app, like the <c>icons.read</c> step of <see cref="SyncAppsJobHandler"/>: a failed read is
/// logged and leaves the app's icon as it was, and a product with no logo (not published yet) changes nothing. Icon
/// precedence (<see cref="MicrosoftIconMayReplace"/>): App Store and Google Play icons win over Microsoft Store ones, so a
/// Microsoft logo only fills an app that has no icon, has no App Store or Google Play record, or whose icon already came
/// from the Microsoft Store.
/// </para>
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="appRepository"></param>
/// <param name="productRepository"></param>
/// <param name="submissionRepository"></param>
/// <param name="flightRepository"></param>
/// <param name="addonRepository"></param>
/// <param name="packageRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="microsoftStoreClient"></param>
/// <param name="timeProvider"></param>
/// <param name="stepRepository"></param>
/// <param name="storeGateway"></param>
/// <param name="logger"></param>
public sealed class MsSyncJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                     IStoreasyRepositoryBase<App> appRepository,
                                     IStoreasyRepositoryBase<MsProduct> productRepository,
                                     IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                     IStoreasyRepositoryBase<MsFlight> flightRepository,
                                     IStoreasyRepositoryBase<MsAddon> addonRepository,
                                     IStoreasyRepositoryBase<MsInstallerPackage> packageRepository,
                                     IStoreasyRepositoryBase<Build> buildRepository,
                                     IMicrosoftStoreClient microsoftStoreClient,
                                     TimeProvider timeProvider,
                                     IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                     IStoreGateway storeGateway,
                                     IMilvaLogger logger) : IStoreJobHandler
{
    /// <summary>
    /// Host of the Microsoft Store image service, where every display catalog logo lives.
    /// </summary>
    public const string MicrosoftImageHost = "store-images.s-microsoft.com";

    private readonly IStoreGateway _storeGateway = storeGateway;
    private readonly IMilvaLogger _logger = logger;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = submissionRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = flightRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = addonRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly Dictionary<int, int> _appIds = [];
    private readonly HashSet<int> _modifiedOutsideApi = [];

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.MsSync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<MsSyncPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store != StoreKind.Microsoft)
            throw new StoreJobException("The connection is not a Microsoft Store connection.", transient: false);

        var known = await _productRepository.GetAllAsync(p => p.OrganizationId == context.OrganizationId && p.StoreApp.Store == StoreKind.Microsoft,
                                                         projection: p => new MsProduct
                                                         {
                                                             Id = p.Id,
                                                             StoreAppId = p.StoreAppId,
                                                             Kind = p.Kind,
                                                             SyncedDate = p.SyncedDate,
                                                             StoreApp = new StoreApp { Id = p.StoreApp.Id, AppId = p.StoreApp.AppId, ExternalId = p.StoreApp.ExternalId, Name = p.StoreApp.Name, BundleId = p.StoreApp.BundleId, StoreConnectionId = p.StoreApp.StoreConnectionId },
                                                         },
                                                         cancellationToken: cancellationToken);

        // 1. MSIX apps the Entra application sees.
        var listStep = await context.BeginStepAsync("apps.list", StepDirection.Outbound, $"Microsoft connection '{connection.Name}'", cancellationToken: cancellationToken);
        var applications = await _microsoftStoreClient.ListApplicationsAsync(connection.SecretJson, cancellationToken);

        await context.EndStepAsync(listStep, StoreJobStepStatus.Succeeded, $"{applications.Count} MSIX apps.", cancellationToken);

        // 2. Details of every MSIX app and MSI/EXE product of this connection. A product added by its Store id starts as
        // MSI/EXE; when the MSIX API lists it, it is an MSIX app: it is read as one (and becomes one when saved), never
        // through the MSI/EXE API, so its record gets the package family name.
        var listed = applications.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var installers = known.Where(p => p.Kind == MsProductKind.MsiExe && p.StoreApp.StoreConnectionId == connection.Id && !listed.Contains(p.StoreApp.ExternalId)).ToList();
        var readStep = await context.BeginStepAsync("products.read", StepDirection.Outbound, total: applications.Count + installers.Count, cancellationToken: cancellationToken);
        var snapshots = new List<MsProductSnapshot>();
        var refused = new List<MsProduct>();
        var progress = 0;

        foreach (var application in applications)
        {
            snapshots.Add(await MsSyncReader.ReadAppAsync(_microsoftStoreClient, connection.SecretJson, application, cancellationToken));
            await context.ReportProgressAsync(readStep, ++progress, cancellationToken);
        }

        foreach (var product in installers)
        {
            try
            {
                snapshots.Add(await MsSyncReader.ReadProductAsync(_microsoftStoreClient, connection.SecretJson, product.StoreApp.ExternalId, product.StoreApp.Name, cancellationToken));
            }
            catch (StoreCallException ex) when (!ex.Transient)
            {
                refused.Add(product);
            }

            await context.ReportProgressAsync(readStep, ++progress, cancellationToken);
        }

        var problems = snapshots.SelectMany(s => s.Problems).Distinct().ToList();

        await context.EndStepAsync(readStep,
                                   StoreJobStepStatus.Succeeded,
                                   $"{snapshots.Count} products; refused: {refused.Count}; problems: {(problems.Count == 0 ? "none" : string.Join(" | ", problems.Take(3)))}",
                                   cancellationToken);

        // 3. Save.
        var saveStep = await context.BeginStepAsync("products.save", StepDirection.Internal, cancellationToken: cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var products = await SaveProductsAsync(context.OrganizationId, connection.Id, snapshots, known, refused, now, cancellationToken);

        await SaveSubmissionsAsync(context.OrganizationId, products, now, cancellationToken);
        await SaveFlightsAsync(context.OrganizationId, products, now, cancellationToken);
        await SaveAddonsAsync(context.OrganizationId, products, now, cancellationToken);
        await SaveInstallersAsync(context.OrganizationId, products, now, cancellationToken);

        var newBuilds = await SaveBuildsAsync(context.OrganizationId, products, now, cancellationToken);

        await _stepRepository.EnsureMsAssistedStepsAsync(context.OrganizationId, [.. products.SelectMany(p => AssistedStepsOf(p.StoreAppId, p.Snapshot))], cancellationToken);

        await context.EndStepAsync(saveStep, StoreJobStepStatus.Succeeded, $"{products.Count} products saved, {newBuilds} new builds.", cancellationToken);

        // 4. Read the logos. A failed read only costs that icon: it is logged and the app keeps the icon it has.
        var iconStep = await context.BeginStepAsync("icons.read", StepDirection.Outbound, total: products.Count, cancellationToken: cancellationToken);
        var icons = new Dictionary<int, string>();
        var iconFailures = 0;

        for (var i = 0; i < products.Count; i++)
        {
            var url = await ReadIconAsync(connection, products[i].Snapshot.ProductId, cancellationToken);

            if (url == null)
                iconFailures++;
            else if (url.Length > 0 && _appIds.TryGetValue(products[i].StoreAppId, out var appId))
                icons.TryAdd(appId, url);

            await context.ReportProgressAsync(iconStep, i + 1, cancellationToken);
        }

        var iconsChanged = await SaveIconsAsync(icons, cancellationToken);

        await context.EndStepAsync(iconStep, StoreJobStepStatus.Succeeded, iconFailures == 0 ? $"{iconsChanged} icons updated." : $"{iconsChanged} icons updated; {iconFailures} could not be read.", cancellationToken);

        return new StoreJobOutcome($"{products.Count} Microsoft Store products synced ({newBuilds} new builds).",
                                   new { Products = products.Count, NewBuilds = newBuilds, Icons = iconsChanged, Refused = refused.Select(r => r.StoreApp.ExternalId).ToList(), Problems = problems });
    }

    /// <summary>
    /// Reads the logo of one product. Returns the address, an empty string when the catalog has no logo for it, or null when
    /// the read failed (logged as a warning; the job goes on).
    /// </summary>
    private async Task<string> ReadIconAsync(OpenedStoreConnection connection, string productId, CancellationToken cancellationToken)
    {
        try
        {
            var url = await _storeGateway.GetIconUrlAsync(StoreKind.Microsoft, connection.SecretJson, productId, cancellationToken);

            return string.IsNullOrWhiteSpace(url) || url.Length > App.IconUrlMaxLength ? string.Empty : url;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Warning("The icon of {Store} app {ExternalId} could not be read: {Error}", StoreKind.Microsoft, productId, SecretRedactor.Redact(ex.Message));

            return null;
        }
    }

    /// <summary>
    /// Writes the logos that changed and that <see cref="MicrosoftIconMayReplace"/> allows. An app whose logo could not be
    /// read is not in <paramref name="icons"/>, so a failure never clears an icon. Only the icon column is written.
    /// </summary>
    private async Task<int> SaveIconsAsync(Dictionary<int, string> icons, CancellationToken cancellationToken)
    {
        if (icons.Count == 0)
            return 0;

        var appIds = icons.Keys.ToList();

        var otherStores = (await _storeAppRepository.GetAllAsync(s => appIds.Contains(s.AppId) && s.Store != StoreKind.Microsoft,
                                                                 projection: s => new StoreApp { AppId = s.AppId },
                                                                 cancellationToken: cancellationToken)).Select(s => s.AppId).ToHashSet();

        var changed = (await _appRepository.GetAllAsync(a => appIds.Contains(a.Id),
                                                        projection: a => new App { Id = a.Id, IconUrl = a.IconUrl },
                                                        cancellationToken: cancellationToken)).Where(a => a.IconUrl != icons[a.Id] && MicrosoftIconMayReplace(a.IconUrl, otherStores.Contains(a.Id)))
                                                                                              .ToList();

        if (changed.Count == 0)
            return 0;

        changed.ForEach(a => a.IconUrl = icons[a.Id]);

        await _appRepository.UpdateAsync(changed, cancellationToken, a => a.IconUrl);

        return changed.Count;
    }

    /// <summary>
    /// Whether a Microsoft Store logo may replace an app's <paramref name="currentIconUrl"/>. App Store and Google Play icons
    /// win (their sync always writes its icon), so the logo is written only when the app has no icon, has no App Store or
    /// Google Play record (<paramref name="hasOtherStoreRecord"/>), or its icon is on the Microsoft Store image host.
    /// </summary>
    /// <param name="currentIconUrl"></param>
    /// <param name="hasOtherStoreRecord"></param>
    /// <returns></returns>
    public static bool MicrosoftIconMayReplace(string currentIconUrl, bool hasOtherStoreRecord)
        => string.IsNullOrWhiteSpace(currentIconUrl)
           || !hasOtherStoreRecord
           || (Uri.TryCreate(currentIconUrl, UriKind.Absolute, out var uri) && string.Equals(uri.Host, MicrosoftImageHost, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Updates the known records and product rows, adds the new MSIX apps (app, record and product row in one insert) and
    /// removes never confirmed MSI/EXE products the store refused. Returns the store record id of every snapshot.
    /// </summary>
    private async Task<List<(int StoreAppId, MsProductSnapshot Snapshot)>> SaveProductsAsync(int organizationId,
                                                                                            int connectionId,
                                                                                            List<MsProductSnapshot> snapshots,
                                                                                            List<MsProduct> known,
                                                                                            List<MsProduct> refused,
                                                                                            DateTime now,
                                                                                            CancellationToken cancellationToken)
    {
        var knownById = known.GroupBy(p => p.StoreApp.ExternalId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<(int, MsProductSnapshot)>();
        var records = new List<StoreApp>();
        var rows = new List<MsProduct>();
        var created = new List<(MsProduct Row, MsProductSnapshot Snapshot)>();

        foreach (var snapshot in snapshots)
        {
            if (knownById.TryGetValue(snapshot.ProductId, out var product))
            {
                product.StoreApp.Name = Trim(snapshot.Name, App.NameMaxLength) ?? product.StoreApp.Name;
                product.StoreApp.BundleId = snapshot.PackageFamilyName ?? product.StoreApp.BundleId;
                product.StoreApp.StoreConnectionId = connectionId;
                product.StoreApp.SyncedDate = now;
                records.Add(product.StoreApp);

                Apply(product, snapshot, now);
                rows.Add(product);
                result.Add((product.StoreAppId, snapshot));
                _appIds[product.StoreAppId] = product.StoreApp.AppId;
                continue;
            }

            var name = Trim(snapshot.Name, App.NameMaxLength) ?? snapshot.ProductId;
            var row = new MsProduct
            {
                OrganizationId = organizationId,
                Kind = snapshot.Kind,
                StoreApp = new StoreApp
                {
                    OrganizationId = organizationId,
                    StoreConnectionId = connectionId,
                    Store = StoreKind.Microsoft,
                    ExternalId = snapshot.ProductId,
                    BundleId = snapshot.PackageFamilyName,
                    Name = name,
                    SyncedDate = now,
                    App = new App { OrganizationId = organizationId, Name = name },
                },
            };

            Apply(row, snapshot, now);
            created.Add((row, snapshot));
        }

        if (records.Count > 0)
            await _storeAppRepository.UpdateAsync(records, cancellationToken, s => s.Name, s => s.BundleId, s => s.StoreConnectionId, s => s.SyncedDate);

        if (rows.Count > 0)
        {
            await _productRepository.UpdateAsync(rows,
                                                 cancellationToken,
                                                 p => p.Kind,
                                                 p => p.FirstSubmissionDone,
                                                 p => p.HasMandatoryUpdate,
                                                 p => p.PricingV2,
                                                 p => p.IsReady,
                                                 p => p.LastPublishedSubmissionId,
                                                 p => p.PendingSubmissionId,
                                                 p => p.SyncedDate);
        }

        if (created.Count > 0)
        {
            await _productRepository.AddRangeAsync([.. created.Select(c => c.Row)], cancellationToken);

            result.AddRange(created.Select(c => (c.Row.StoreAppId, c.Snapshot)));

            foreach (var (row, _) in created)
                _appIds[row.StoreAppId] = row.StoreApp.AppId;
        }

        var neverConfirmed = refused.Where(r => r.SyncedDate == null).ToList();

        if (neverConfirmed.Count > 0)
        {
            var productIds = neverConfirmed.Select(r => r.Id).ToList();
            var storeAppIds = neverConfirmed.Select(r => r.StoreAppId).ToList();
            var appIds = neverConfirmed.Select(r => r.StoreApp.AppId).ToList();

            await _productRepository.ExecuteDeleteAsync(p => productIds.Contains(p.Id), cancellationToken: cancellationToken);
            await _storeAppRepository.ExecuteDeleteAsync(s => storeAppIds.Contains(s.Id), cancellationToken: cancellationToken);
            await _appRepository.ExecuteDeleteAsync(a => appIds.Contains(a.Id) && !a.StoreApps.Any(), cancellationToken: cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// The Partner Center tasks a product read shows: first submission, Pricing Version 2, mandatory updates, refused
    /// add-ons, drafts changed in Partner Center and failed certification.
    /// </summary>
    private IEnumerable<(int AppId, string Key)> AssistedStepsOf(int storeAppId, MsProductSnapshot snapshot)
    {
        if (!_appIds.TryGetValue(storeAppId, out var appId))
            yield break;

        if (snapshot.Kind == MsProductKind.Msix && !snapshot.FirstSubmissionDone)
            yield return (appId, MsAssistedSteps.FirstSubmission);

        if (snapshot.PricingV2)
            yield return (appId, MsAssistedSteps.Pricing);

        if (snapshot.HasMandatoryUpdate)
            yield return (appId, MsAssistedSteps.MandatoryUpdate);

        if (snapshot.Addons.Any(a => a.Refused))
            yield return (appId, MsAssistedSteps.AddonUpdate);

        if (_modifiedOutsideApi.Contains(storeAppId))
            yield return (appId, MsAssistedSteps.ManualEdit);

        if (snapshot.Submissions.Any(s => s.Status == MsSubmissionStatus.CertificationFailed))
            yield return (appId, MsAssistedSteps.CertificationReport);
    }

    private static void Apply(MsProduct row, MsProductSnapshot snapshot, DateTime now)
    {
        row.Kind = snapshot.Kind;
        row.FirstSubmissionDone = snapshot.FirstSubmissionDone;
        row.HasMandatoryUpdate = snapshot.HasMandatoryUpdate;
        row.PricingV2 = snapshot.PricingV2;
        row.IsReady = snapshot.IsReady;
        row.LastPublishedSubmissionId = snapshot.LastPublishedSubmissionId;
        row.PendingSubmissionId = snapshot.PendingSubmissionId;
        row.SyncedDate = now;
    }

    private async Task SaveSubmissionsAsync(int organizationId, List<(int StoreAppId, MsProductSnapshot Snapshot)> products, DateTime now, CancellationToken cancellationToken)
    {
        var storeAppIds = products.Select(p => p.StoreAppId).ToList();
        var existing = (await _submissionRepository.GetAllAsync(s => storeAppIds.Contains(s.StoreAppId), cancellationToken: cancellationToken))
                       .ToDictionary(s => (s.StoreAppId, s.Target, s.ExternalId));

        var seen = products.SelectMany(p => p.Snapshot.Submissions.Select(s => (p.StoreAppId, Submission: s))).DistinctBy(s => (s.StoreAppId, s.Submission.Target, s.Submission.ExternalId)).ToList();
        var updated = new List<MsSubmission>();
        var added = new List<MsSubmission>();

        foreach (var (storeAppId, snapshot) in seen)
        {
            var row = existing.GetValueOrDefault((storeAppId, snapshot.Target, snapshot.ExternalId))
                      ?? new MsSubmission { OrganizationId = organizationId, StoreAppId = storeAppId, Target = snapshot.Target, ExternalId = snapshot.ExternalId };

            MsSubmissionRows.Observe(row, snapshot.Status, snapshot.Details, now);

            MsSubmissionRows.ObserveContent(row, snapshot.Status, snapshot.IsPending, snapshot.ContentHash);

            if (row.ModifiedOutsideApi && snapshot.IsPending)
                _modifiedOutsideApi.Add(storeAppId);

            row.FlightId = snapshot.FlightId;
            row.AddonStoreId = snapshot.AddonStoreId;
            row.FriendlyName = Trim(snapshot.FriendlyName, MsSubmission.FriendlyNameMaxLength);
            row.IsPending = snapshot.IsPending;
            row.PackageVersion = Trim(snapshot.PackageVersion, MsSubmission.PackageVersionMaxLength) ?? row.PackageVersion;
            row.RolloutPercentage = snapshot.RolloutPercentage;
            row.RolloutStatus = snapshot.RolloutStatus;
            row.PublishMode = snapshot.PublishMode;
            row.PublishDate = snapshot.PublishDate;
            row.SyncedDate = now;

            (row.Id == 0 ? added : updated).Add(row);
        }

        // Submissions the store no longer reports as pending are not pending anymore (published, deleted or replaced).
        var stale = existing.Values.Where(s => s.IsPending && !seen.Any(x => x.StoreAppId == s.StoreAppId && x.Submission.Target == s.Target && x.Submission.ExternalId == s.ExternalId)).ToList();

        foreach (var row in stale)
            row.IsPending = false;

        updated.AddRange(stale);

        if (updated.Count > 0)
        {
            await _submissionRepository.UpdateAsync(updated,
                                                    cancellationToken,
                                                    s => s.Status,
                                                    s => s.StatusHistory,
                                                    s => s.StatusDetails,
                                                    s => s.ModifiedOutsideApi,
                                                    s => s.FlightId,
                                                    s => s.AddonStoreId,
                                                    s => s.FriendlyName,
                                                    s => s.IsPending,
                                                    s => s.PackageVersion,
                                                    s => s.RolloutPercentage,
                                                    s => s.RolloutStatus,
                                                    s => s.PublishMode,
                                                    s => s.PublishDate,
                                                    s => s.SyncedDate);
        }

        if (added.Count > 0)
            await _submissionRepository.AddRangeAsync(added, cancellationToken);
    }

    private async Task SaveFlightsAsync(int organizationId, List<(int StoreAppId, MsProductSnapshot Snapshot)> products, DateTime now, CancellationToken cancellationToken)
    {
        var msix = products.Where(p => p.Snapshot.Kind == MsProductKind.Msix).ToList();
        var storeAppIds = msix.Select(p => p.StoreAppId).ToList();

        if (storeAppIds.Count == 0)
            return;

        var existing = (await _flightRepository.GetAllAsync(f => storeAppIds.Contains(f.StoreAppId), cancellationToken: cancellationToken)).ToDictionary(f => (f.StoreAppId, f.FlightId));
        var updated = new List<MsFlight>();
        var added = new List<MsFlight>();
        var seen = new HashSet<(int, string)>();

        foreach (var (storeAppId, snapshot) in msix)
        {
            var ranks = RankFlights(snapshot.Flights);

            foreach (var flight in snapshot.Flights)
            {
                seen.Add((storeAppId, flight.FlightId));

                var row = existing.GetValueOrDefault((storeAppId, flight.FlightId)) ?? new MsFlight { OrganizationId = organizationId, StoreAppId = storeAppId, FlightId = flight.FlightId };

                row.FriendlyName = Trim(flight.FriendlyName, MsFlight.FriendlyNameMaxLength);
                row.GroupIds = [.. flight.GroupIds];
                row.Rank = ranks.GetValueOrDefault(flight.FlightId);
                row.LastPublishedSubmissionId = flight.LastPublishedSubmissionId;
                row.PendingSubmissionId = flight.PendingSubmissionId;
                row.SyncedDate = now;

                (row.Id == 0 ? added : updated).Add(row);
            }
        }

        if (updated.Count > 0)
            await _flightRepository.UpdateAsync(updated, cancellationToken, f => f.FriendlyName, f => f.GroupIds, f => f.Rank, f => f.LastPublishedSubmissionId, f => f.PendingSubmissionId, f => f.SyncedDate);

        if (added.Count > 0)
            await _flightRepository.AddRangeAsync(added, cancellationToken);

        var removed = existing.Values.Where(f => !seen.Contains((f.StoreAppId, f.FlightId))).Select(f => f.Id).ToList();

        if (removed.Count > 0)
            await _flightRepository.ExecuteDeleteAsync(f => removed.Contains(f.Id), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ranks of the flights (1 is the highest), from the <c>rankHigherThan</c> chain the API reports.
    /// </summary>
    /// <param name="flights"></param>
    /// <returns></returns>
    public static Dictionary<string, int> RankFlights(IReadOnlyList<MsFlightRecord> flights)
    {
        var below = flights.Where(f => !string.IsNullOrEmpty(f.RankHigherThan)).ToDictionary(f => f.FlightId, f => f.RankHigherThan);
        var ranks = new Dictionary<string, int>();

        foreach (var flight in flights)
        {
            // Depth of the chain down to the lowest flight; the highest flight has the longest chain.
            var depth = 0;
            var current = flight.FlightId;
            var guard = new HashSet<string>();

            while (below.TryGetValue(current, out var next) && guard.Add(current))
            {
                depth++;
                current = next;
            }

            ranks[flight.FlightId] = depth;
        }

        var max = ranks.Count == 0 ? 0 : ranks.Values.Max();

        return ranks.ToDictionary(r => r.Key, r => max - r.Value + 1);
    }

    private async Task SaveAddonsAsync(int organizationId, List<(int StoreAppId, MsProductSnapshot Snapshot)> products, DateTime now, CancellationToken cancellationToken)
    {
        var msix = products.Where(p => p.Snapshot.Kind == MsProductKind.Msix).ToList();
        var storeAppIds = msix.Select(p => p.StoreAppId).ToList();

        if (storeAppIds.Count == 0)
            return;

        var existing = (await _addonRepository.GetAllAsync(a => storeAppIds.Contains(a.StoreAppId), cancellationToken: cancellationToken)).ToDictionary(a => (a.StoreAppId, a.StoreId));
        var updated = new List<MsAddon>();
        var added = new List<MsAddon>();
        var seen = new HashSet<(int, string)>();

        foreach (var (storeAppId, snapshot) in msix)
        {
            foreach (var addon in snapshot.Addons)
            {
                seen.Add((storeAppId, addon.Record.StoreId));

                var row = existing.GetValueOrDefault((storeAppId, addon.Record.StoreId)) ?? new MsAddon { OrganizationId = organizationId, StoreAppId = storeAppId, StoreId = addon.Record.StoreId };

                row.ProductId = Trim(addon.Record.ProductId, MsAddon.ProductIdMaxLength) ?? row.ProductId;
                row.Type = ParseAddonType(addon.Record.ProductType, row.Type);
                row.Title = Trim(addon.Title, MsAddon.TitleMaxLength) ?? row.Title;
                row.PriceId = Trim(addon.PriceId, MsProduct.IdMaxLength) ?? row.PriceId;
                row.Lifetime = Trim(addon.Lifetime, MsProduct.IdMaxLength) ?? row.Lifetime;
                row.LastPublishedSubmissionId = addon.Record.LastPublishedSubmissionId;
                row.PendingSubmissionId = addon.Record.PendingSubmissionId;
                row.SyncedDate = now;

                if (addon.Refused)
                {
                    row.ApiWritable = false;
                    row.AssistedReason ??= MsAssistedReason.StoreManagedConsumable;
                }

                (row.Id == 0 ? added : updated).Add(row);
            }
        }

        if (updated.Count > 0)
        {
            await _addonRepository.UpdateAsync(updated,
                                               cancellationToken,
                                               a => a.ProductId,
                                               a => a.Type,
                                               a => a.Title,
                                               a => a.PriceId,
                                               a => a.Lifetime,
                                               a => a.LastPublishedSubmissionId,
                                               a => a.PendingSubmissionId,
                                               a => a.ApiWritable,
                                               a => a.AssistedReason,
                                               a => a.SyncedDate);
        }

        if (added.Count > 0)
            await _addonRepository.AddRangeAsync(added, cancellationToken);

        var removed = existing.Values.Where(a => !seen.Contains((a.StoreAppId, a.StoreId))).Select(a => a.Id).ToList();

        if (removed.Count > 0)
            await _addonRepository.ExecuteDeleteAsync(a => removed.Contains(a.Id), cancellationToken: cancellationToken);
    }

    private async Task SaveInstallersAsync(int organizationId, List<(int StoreAppId, MsProductSnapshot Snapshot)> products, DateTime now, CancellationToken cancellationToken)
    {
        var installers = products.Where(p => p.Snapshot.Kind == MsProductKind.MsiExe).ToList();

        if (installers.Count == 0)
            return;

        var storeAppIds = installers.Select(p => p.StoreAppId).ToList();
        var existing = await _packageRepository.GetAllAsync(p => storeAppIds.Contains(p.StoreAppId), cancellationToken: cancellationToken);
        var rows = MsInstallerRows.Merge(organizationId, installers.Select(i => (i.StoreAppId, (IReadOnlyList<MsInstallerPackageRecord>)i.Snapshot.Installers)), existing, now);

        await SaveInstallerRowsAsync(rows, cancellationToken);
    }

    /// <summary>
    /// Writes the outcome of <see cref="MsInstallerRows.Merge"/>: updates, inserts and deletes of the installer rows.
    /// </summary>
    private async Task SaveInstallerRowsAsync((List<MsInstallerPackage> Updated, List<MsInstallerPackage> Added, List<int> Removed) rows, CancellationToken cancellationToken)
    {
        if (rows.Updated.Count > 0)
        {
            await _packageRepository.UpdateAsync(rows.Updated,
                                                 cancellationToken,
                                                 p => p.PackageId,
                                                 p => p.PackageType,
                                                 p => p.Architectures,
                                                 p => p.Languages,
                                                 p => p.InstallerParameters,
                                                 p => p.IsSilentInstall,
                                                 p => p.GenericDocUrl,
                                                 p => p.SyncedDate);
        }

        if (rows.Added.Count > 0)
            await _packageRepository.AddRangeAsync(rows.Added, cancellationToken);

        if (rows.Removed.Count > 0)
            await _packageRepository.ExecuteDeleteAsync(p => rows.Removed.Contains(p.Id), cancellationToken: cancellationToken);
    }

    private async Task<int> SaveBuildsAsync(int organizationId, List<(int StoreAppId, MsProductSnapshot Snapshot)> products, DateTime now, CancellationToken cancellationToken)
    {
        var builds = products.SelectMany(p => p.Snapshot.Builds.Where(b => !string.IsNullOrEmpty(b.ExternalId)).Select(b => (p.StoreAppId, Build: b)))
                             .DistinctBy(b => (b.StoreAppId, b.Build.ExternalId))
                             .ToList();

        if (builds.Count == 0)
            return 0;

        var storeAppIds = products.Select(p => p.StoreAppId).ToList();
        var existing = (await _buildRepository.GetAllAsync(b => storeAppIds.Contains(b.StoreAppId),
                                                           projection: b => new Build { Id = b.Id, StoreAppId = b.StoreAppId, ExternalId = b.ExternalId },
                                                           cancellationToken: cancellationToken)).ToDictionary(b => (b.StoreAppId, b.ExternalId));

        var updated = new List<Build>();
        var added = new List<Build>();

        foreach (var (storeAppId, record) in builds)
        {
            var externalId = Trim(record.ExternalId, StoreApp.ExternalIdMaxLength);
            var row = existing.GetValueOrDefault((storeAppId, externalId)) ?? new Build { OrganizationId = organizationId, StoreAppId = storeAppId, Store = StoreKind.Microsoft, ExternalId = externalId };

            row.Version = Trim(record.Version, Build.TextMaxLength);
            row.BuildNumber = Trim(record.BuildNumber, Build.TextMaxLength);
            row.ProcessingState = record.ProcessingState;
            row.Tracks = Trim(record.Tracks, Build.TextMaxLength);
            row.SyncedDate = now;

            (row.Id == 0 ? added : updated).Add(row);
        }

        if (updated.Count > 0)
            await _buildRepository.UpdateAsync(updated, cancellationToken, b => b.Version, b => b.BuildNumber, b => b.ProcessingState, b => b.Tracks, b => b.SyncedDate);

        if (added.Count > 0)
            await _buildRepository.AddRangeAsync(added, cancellationToken);

        return added.Count;
    }

    private static MsAddonType ParseAddonType(string type, MsAddonType current) => type?.ToUpperInvariant() switch
    {
        "DURABLE" => MsAddonType.Durable,
        "CONSUMABLE" => MsAddonType.Consumable,
        "SUBSCRIPTION" => MsAddonType.Subscription,
        null => current == 0 ? MsAddonType.Unknown : current,
        _ => MsAddonType.Unknown,
    };

    private static string Trim(string value, int maxLength) => value?.Length > maxLength ? value[..maxLength] : value;
}

/// <summary>
/// Pure updates of submission rows shared by the sync and the write jobs.
/// </summary>
public static class MsSubmissionRows
{
    /// <summary>
    /// Records a status read: appends it to the history when it changed and keeps the details.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="status"></param>
    /// <param name="details"></param>
    /// <param name="now"></param>
    public static void Observe(MsSubmission row, MsSubmissionStatus status, MsStatusDetails details, DateTime now)
    {
        row.StatusHistory ??= [];

        if (row.StatusHistory.Count == 0 || row.StatusHistory[^1].Status != status)
            row.StatusHistory.Add(new MsStatusChange { Status = status, Date = now });

        row.Status = status;
        row.StatusDetails = details ?? row.StatusDetails ?? new MsStatusDetails();
    }

    /// <summary>
    /// Partner Center rule: a draft storeasy created whose content changed since storeasy wrote it can no longer be changed
    /// or committed through the API. Only a draft (<see cref="MsSubmissionStatus.PendingCommit"/>) is compared: once
    /// committed, Microsoft fills in the submission itself (package details, processing fields), which is no edit, and
    /// nothing can change it through the API anyway, so the flag is cleared.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="status"></param>
    /// <param name="isPending"></param>
    /// <param name="contentHash"></param>
    public static void ObserveContent(MsSubmission row, MsSubmissionStatus status, bool isPending, string contentHash)
    {
        if (!isPending || status != MsSubmissionStatus.PendingCommit)
        {
            row.ModifiedOutsideApi = false;
            return;
        }

        if (row.CreatedByStoreasy && row.ContentHash != null && contentHash != null && row.ContentHash != contentHash)
            row.ModifiedOutsideApi = true;
    }
}

/// <summary>
/// Merges installer packages read from the store into storeasy's rows, keeping the signature check of an unchanged URL.
/// </summary>
public static class MsInstallerRows
{
    /// <summary>
    /// Returns the rows to update, to add and the ids to delete.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="products"></param>
    /// <param name="existing"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static (List<MsInstallerPackage> Updated, List<MsInstallerPackage> Added, List<int> Removed) Merge(int organizationId,
                                                                                                           IEnumerable<(int StoreAppId, IReadOnlyList<MsInstallerPackageRecord> Packages)> products,
                                                                                                           List<MsInstallerPackage> existing,
                                                                                                           DateTime now)
    {
        var byKey = existing.GroupBy(p => (p.StoreAppId, p.PackageUrl)).ToDictionary(g => g.Key, g => g.First());
        var updated = new List<MsInstallerPackage>();
        var added = new List<MsInstallerPackage>();
        var kept = new HashSet<int>();
        var storeAppIds = new HashSet<int>();

        foreach (var (storeAppId, packages) in products)
        {
            storeAppIds.Add(storeAppId);

            foreach (var package in packages.Where(p => !string.IsNullOrEmpty(p.PackageUrl)).DistinctBy(p => p.PackageUrl))
            {
                var row = byKey.GetValueOrDefault((storeAppId, package.PackageUrl)) ?? new MsInstallerPackage { OrganizationId = organizationId, StoreAppId = storeAppId, PackageUrl = package.PackageUrl };

                row.PackageId = package.PackageId;
                row.PackageType = package.PackageType;
                row.Architectures = [.. package.Architectures];
                row.Languages = [.. package.Languages];
                row.InstallerParameters = package.InstallerParameters?.Length > MsInstallerPackage.InstallerParametersMaxLength ? package.InstallerParameters[..MsInstallerPackage.InstallerParametersMaxLength] : package.InstallerParameters;
                row.IsSilentInstall = package.IsSilentInstall;
                row.GenericDocUrl = package.GenericDocUrl;
                row.SyncedDate = now;

                if (row.Id == 0)
                {
                    added.Add(row);
                }
                else
                {
                    kept.Add(row.Id);
                    updated.Add(row);
                }
            }
        }

        var removed = existing.Where(p => storeAppIds.Contains(p.StoreAppId) && !kept.Contains(p.Id)).Select(p => p.Id).ToList();

        return (updated, added, removed);
    }
}
