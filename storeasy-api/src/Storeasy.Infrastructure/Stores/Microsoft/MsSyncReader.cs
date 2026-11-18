using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// What the sync read of one Microsoft Store product.
/// </summary>
public sealed class MsProductSnapshot
{
    /// <summary>Store id.</summary>
    public string ProductId { get; set; }

    /// <summary>Which API manages it.</summary>
    public MsProductKind Kind { get; set; }

    /// <summary>Name in the store, if known.</summary>
    public string Name { get; set; }

    /// <summary>Package family name (MSIX).</summary>
    public string PackageFamilyName { get; set; }

    /// <summary>Whether a submission was published.</summary>
    public bool FirstSubmissionDone { get; set; }

    /// <summary>Whether the app uses mandatory updates.</summary>
    public bool HasMandatoryUpdate { get; set; }

    /// <summary>Whether the price is on Pricing Version 2.</summary>
    public bool PricingV2 { get; set; }

    /// <summary>MSI/EXE draft readiness.</summary>
    public bool IsReady { get; set; }

    /// <summary>Last published app submission.</summary>
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>Pending app (or ongoing MSI/EXE) submission.</summary>
    public string PendingSubmissionId { get; set; }

    /// <summary>Submissions read.</summary>
    public List<MsSubmissionSnapshot> Submissions { get; } = [];

    /// <summary>Flights.</summary>
    public List<MsFlightRecord> Flights { get; set; } = [];

    /// <summary>Add-ons.</summary>
    public List<MsAddonSnapshot> Addons { get; } = [];

    /// <summary>MSI/EXE installers.</summary>
    public List<MsInstallerPackageRecord> Installers { get; set; } = [];

    /// <summary>Packages as builds.</summary>
    public List<StoreBuildRecord> Builds { get; } = [];

    /// <summary>Non fatal problems of the read (redacted).</summary>
    public List<string> Problems { get; } = [];
}

/// <summary>
/// A submission as the sync read it.
/// </summary>
public sealed class MsSubmissionSnapshot
{
    /// <summary>Target.</summary>
    public MsSubmissionTarget Target { get; set; }

    /// <summary>Submission id.</summary>
    public string ExternalId { get; set; }

    /// <summary>Flight.</summary>
    public string FlightId { get; set; }

    /// <summary>Add-on.</summary>
    public string AddonStoreId { get; set; }

    /// <summary>Partner Center name.</summary>
    public string FriendlyName { get; set; }

    /// <summary>Status.</summary>
    public MsSubmissionStatus Status { get; set; }

    /// <summary>Errors, warnings, reports.</summary>
    public MsStatusDetails Details { get; set; }

    /// <summary>Whether it is the pending one.</summary>
    public bool IsPending { get; set; }

    /// <summary>Content fingerprint.</summary>
    public string ContentHash { get; set; }

    /// <summary>Highest package version.</summary>
    public string PackageVersion { get; set; }

    /// <summary>Rollout percentage.</summary>
    public double? RolloutPercentage { get; set; }

    /// <summary>Rollout state.</summary>
    public MsRolloutStatus? RolloutStatus { get; set; }
}

/// <summary>
/// An add-on as the sync read it.
/// </summary>
public sealed class MsAddonSnapshot
{
    /// <summary>The API record.</summary>
    public MsAddonRecord Record { get; set; }

    /// <summary>Title of the first listing.</summary>
    public string Title { get; set; }

    /// <summary>Price tier.</summary>
    public string PriceId { get; set; }

    /// <summary>Lifetime.</summary>
    public string Lifetime { get; set; }

    /// <summary>Whether the API refused it (409).</summary>
    public bool Refused { get; set; }
}

/// <summary>
/// Reads everything the sync stores about a product. A 409 marks the product or add-on as Partner Center only; other
/// permanent failures of a detail are collected as problems; transient failures fail the attempt (the job retries).
/// </summary>
public static class MsSyncReader
{
    /// <summary>
    /// Reads an MSIX app: its app, flight and add-on submissions, flights and add-ons.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="secretJson"></param>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsProductSnapshot> ReadAppAsync(IMicrosoftStoreClient client, string secretJson, MsApplicationRecord app, CancellationToken cancellationToken)
    {
        var snapshot = new MsProductSnapshot
        {
            ProductId = app.Id,
            Kind = MsProductKind.Msix,
            Name = app.PrimaryName,
            PackageFamilyName = app.PackageFamilyName,
            FirstSubmissionDone = app.LastPublishedSubmissionId != null,
            LastPublishedSubmissionId = app.LastPublishedSubmissionId,
            PendingSubmissionId = app.PendingSubmissionId,
        };

        var owner = new MsSubmissionRef { Target = MsSubmissionTarget.App, ApplicationId = app.Id };

        foreach (var (id, pending) in Submissions(app.PendingSubmissionId, app.LastPublishedSubmissionId))
        {
            var document = await TryReadAsync(snapshot, () => client.GetSubmissionAsync(secretJson, owner.WithSubmission(id), cancellationToken), refused: () => snapshot.HasMandatoryUpdate = true);

            if (document == null)
                continue;

            AddSubmission(snapshot, document, MsSubmissionTarget.App, id, pending, null, null, null);

            if (!pending)
            {
                snapshot.HasMandatoryUpdate |= MsSubmissionJson.IsMandatoryUpdate(document);
                snapshot.PricingV2 = !MsSubmissionJson.IsKnownPriceTier(MsSubmissionJson.ReadPricing(document)?.PriceId);
            }
        }

        snapshot.Flights = [.. await TryReadAsync(snapshot, () => client.ListFlightsAsync(secretJson, app.Id, cancellationToken)) ?? []];

        foreach (var flight in snapshot.Flights)
        {
            var flightOwner = new MsSubmissionRef { Target = MsSubmissionTarget.Flight, ApplicationId = app.Id, FlightId = flight.FlightId };

            foreach (var (id, pending) in Submissions(flight.PendingSubmissionId, flight.LastPublishedSubmissionId))
            {
                var document = await TryReadAsync(snapshot, () => client.GetSubmissionAsync(secretJson, flightOwner.WithSubmission(id), cancellationToken));

                if (document != null)
                    AddSubmission(snapshot, document, MsSubmissionTarget.Flight, id, pending, flight.FlightId, null, flight.FriendlyName);
            }
        }

        foreach (var addonId in await TryReadAsync(snapshot, () => client.ListAddonIdsAsync(secretJson, app.Id, cancellationToken)) ?? [])
        {
            var addon = new MsAddonSnapshot();
            var record = await TryReadAsync(snapshot, () => client.GetAddonAsync(secretJson, addonId, cancellationToken), refused: () => addon.Refused = true);

            addon.Record = record ?? new MsAddonRecord(addonId, null, null, null, null);
            snapshot.Addons.Add(addon);

            if (record == null)
                continue;

            var addonOwner = new MsSubmissionRef { Target = MsSubmissionTarget.Addon, AddonStoreId = addonId };

            foreach (var (id, pending) in Submissions(record.PendingSubmissionId, record.LastPublishedSubmissionId))
            {
                var document = await TryReadAsync(snapshot, () => client.GetSubmissionAsync(secretJson, addonOwner.WithSubmission(id), cancellationToken), refused: () => addon.Refused = true);

                if (document == null)
                    continue;

                AddSubmission(snapshot, document, MsSubmissionTarget.Addon, id, pending, null, addonId, null);

                addon.Title ??= MsSubmissionJson.ReadAddonListings(document).FirstOrDefault()?.Title;
                addon.PriceId ??= MsSubmissionJson.ReadPricing(document)?.PriceId;
                addon.Lifetime ??= MsSubmissionJson.String(document, "lifetime");
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Reads an MSI/EXE product: readiness, ongoing submission, installers. Throws when the store refuses the product.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="secretJson"></param>
    /// <param name="productId"></param>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsProductSnapshot> ReadProductAsync(IMicrosoftStoreClient client, string secretJson, string productId, string name, CancellationToken cancellationToken)
    {
        var status = await client.GetProductStatusAsync(secretJson, productId, cancellationToken);
        var snapshot = new MsProductSnapshot { ProductId = productId, Kind = MsProductKind.MsiExe, Name = name, IsReady = status.IsReady };

        snapshot.Installers = [.. await client.GetInstallerPackagesAsync(secretJson, productId, cancellationToken)];

        if (!string.IsNullOrEmpty(status.OngoingSubmissionId))
        {
            var publishing = await client.GetProductSubmissionStatusAsync(secretJson, productId, status.OngoingSubmissionId, cancellationToken);

            snapshot.Submissions.Add(new MsSubmissionSnapshot
            {
                Target = MsSubmissionTarget.Installer,
                ExternalId = status.OngoingSubmissionId,
                Status = publishing.HasFailed ? MsSubmissionStatus.Failed : publishing.Status,
                Details = new MsStatusDetails { Errors = [.. status.Errors] },
                IsPending = publishing.Status == MsSubmissionStatus.InProgress,
            });

            snapshot.PendingSubmissionId = publishing.Status == MsSubmissionStatus.InProgress ? status.OngoingSubmissionId : null;
            snapshot.FirstSubmissionDone = publishing.Status == MsSubmissionStatus.Published;
        }

        snapshot.Builds.AddRange(snapshot.Installers.Where(i => !string.IsNullOrEmpty(i.PackageUrl)).Select(i => new StoreBuildRecord(i.PackageId ?? MsSubmissionJson.Hash(i.PackageUrl)[..32],
                                                                                                                                    VersionFromUrl(i.PackageUrl),
                                                                                                                                    i.PackageType.ToString(),
                                                                                                                                    status.IsReady ? BuildProcessingState.Valid : BuildProcessingState.Processing,
                                                                                                                                    null,
                                                                                                                                    null,
                                                                                                                                    string.Join(",", i.Architectures),
                                                                                                                                    null)));

        return snapshot;
    }

    private static IEnumerable<(string Id, bool Pending)> Submissions(string pending, string published)
    {
        if (!string.IsNullOrEmpty(pending))
            yield return (pending, true);

        if (!string.IsNullOrEmpty(published) && published != pending)
            yield return (published, false);
    }

    private static void AddSubmission(MsProductSnapshot snapshot, JsonObject document, MsSubmissionTarget target, string id, bool pending, string flightId, string addonId, string flightName)
    {
        var packages = target == MsSubmissionTarget.Addon ? [] : MsSubmissionJson.ReadPackages(document, target);
        var rollout = MsSubmissionJson.ReadRollout(document);

        snapshot.Submissions.Add(new MsSubmissionSnapshot
        {
            Target = target,
            ExternalId = id,
            FlightId = flightId,
            AddonStoreId = addonId,
            FriendlyName = MsSubmissionJson.String(document, "friendlyName"),
            Status = MsSubmissionJson.ParseStatus(MsSubmissionJson.String(document, "status")),
            Details = MsSubmissionJson.ReadStatusDetails(document["statusDetails"]),
            IsPending = pending,
            ContentHash = MsSubmissionJson.ContentHash(document),
            PackageVersion = packages.Select(p => p.Version).Where(v => v != null).OrderByDescending(v => Version.TryParse(v, out var parsed) ? parsed : new Version()).FirstOrDefault(),
            RolloutPercentage = rollout?.IsPackageRollout == true ? rollout.Percentage : null,
            RolloutStatus = rollout?.IsPackageRollout == true ? rollout.Status : null,
        });

        var tracks = target == MsSubmissionTarget.Flight ? $"flight:{flightName ?? flightId}" : pending ? "pending" : "published";

        snapshot.Builds.AddRange(packages.Select(p => new StoreBuildRecord(p.Id ?? p.FileName,
                                                                           p.Version,
                                                                           p.Version,
                                                                           pending ? BuildProcessingState.Processing : BuildProcessingState.Valid,
                                                                           null,
                                                                           null,
                                                                           p.Architecture == null ? tracks : $"{tracks} {p.Architecture}",
                                                                           null)));
    }

    private static async Task<T> TryReadAsync<T>(MsProductSnapshot snapshot, Func<Task<T>> read, Action refused = null) where T : class
    {
        try
        {
            return await read();
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
            if (ex.StatusCode == 409)
                refused?.Invoke();

            snapshot.Problems.Add(ex.Message);

            return null;
        }
    }

    private static string VersionFromUrl(string url)
    {
        var name = Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);
        var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+(\.\d+){1,3}");

        return match.Success ? match.Value : null;
    }
}
