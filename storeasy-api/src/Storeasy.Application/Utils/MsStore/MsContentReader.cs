using Storeasy.Application.Dtos.MsStoreDtos;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Reads the current Microsoft Store content a change is planned and applied on. For an MSIX app, flight or add-on the
/// content is the pending submission when there is one (Microsoft allows one at a time), otherwise the last published one
/// (a new submission is a copy of it). For an MSI/EXE product it is the draft metadata, the packages and the readiness.
/// Used by the planning request and again by the apply job, so both see the same shape.
/// </summary>
public static class MsContentReader
{
    /// <summary>
    /// What to read.
    /// </summary>
    public sealed class Request
    {
        /// <summary>
        /// Which API manages the product.
        /// </summary>
        public MsProductKind Kind { get; init; }

        /// <summary>
        /// Store id of the product.
        /// </summary>
        public string ProductId { get; init; }

        /// <summary>
        /// Submission target.
        /// </summary>
        public MsSubmissionTarget Target { get; init; }

        /// <summary>
        /// Flight (flight targets).
        /// </summary>
        public string FlightId { get; init; }

        /// <summary>
        /// Add-on (add-on targets).
        /// </summary>
        public string AddonStoreId { get; init; }

        /// <summary>
        /// Published submission whose gradual rollout is read instead (rollout changes).
        /// </summary>
        public string RolloutSubmissionId { get; init; }
    }

    /// <summary>
    /// Reads the content of <paramref name="request"/>.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="secretJson"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsContentDto> ReadAsync(IMicrosoftStoreClient client, string secretJson, Request request, CancellationToken cancellationToken)
    {
        if (request.Kind == MsProductKind.MsiExe)
            return await ReadProductAsync(client, secretJson, request.ProductId, cancellationToken);

        var content = new MsContentDto { Kind = MsProductKind.Msix, Target = request.Target, ProductId = request.ProductId };

        if (!string.IsNullOrEmpty(request.RolloutSubmissionId))
        {
            var owner = new MsSubmissionRef { Target = request.Target, ApplicationId = request.ProductId, FlightId = request.FlightId, SubmissionId = request.RolloutSubmissionId };
            var rollout = await client.GetRolloutAsync(secretJson, owner, cancellationToken);

            content.SubmissionId = request.RolloutSubmissionId;
            content.Rollout = new MsRolloutDto { IsPackageRollout = rollout.IsPackageRollout, Percentage = rollout.Percentage, Status = rollout.Status, FallbackSubmissionId = rollout.FallbackSubmissionId };

            return content;
        }

        string pending;
        string published;

        switch (request.Target)
        {
            case MsSubmissionTarget.Flight:
            {
                // A flight that does not exist yet (CreateFlight) has no content.
                if (string.IsNullOrEmpty(request.FlightId))
                    return content;

                var flight = (await client.ListFlightsAsync(secretJson, request.ProductId, cancellationToken)).FirstOrDefault(f => f.FlightId == request.FlightId)
                             ?? throw new StoreCallException($"404: Flight {request.FlightId} was not found.", 404, false);

                (pending, published) = (flight.PendingSubmissionId, flight.LastPublishedSubmissionId);
                break;
            }
            case MsSubmissionTarget.Addon:
            {
                if (string.IsNullOrEmpty(request.AddonStoreId))
                    return content;

                var addon = await client.GetAddonAsync(secretJson, request.AddonStoreId, cancellationToken);

                (pending, published) = (addon.PendingSubmissionId, addon.LastPublishedSubmissionId);
                break;
            }
            default:
            {
                var app = await client.GetApplicationAsync(secretJson, request.ProductId, cancellationToken);

                (pending, published) = (app.PendingSubmissionId, app.LastPublishedSubmissionId);
                break;
            }
        }

        var submissionId = pending ?? published;

        if (string.IsNullOrEmpty(submissionId))
            return content;

        var submission = new MsSubmissionRef { Target = request.Target, ApplicationId = request.ProductId, FlightId = request.FlightId, AddonStoreId = request.AddonStoreId, SubmissionId = submissionId };
        JsonObject document;

        try
        {
            document = await client.GetSubmissionAsync(secretJson, submission, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            // The API refuses products with mandatory updates and store-managed consumables; the plan blocks them as
            // assisted steps instead of failing.
            content.MandatoryUpdate = request.Target == MsSubmissionTarget.App;

            return content;
        }

        Fill(content, document, request.Target);

        content.SubmissionId = submissionId;
        content.SubmissionIsPending = pending != null;

        return content;
    }

    /// <summary>
    /// Fills <paramref name="content"/> from an MSIX submission document.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="document"></param>
    /// <param name="target"></param>
    public static void Fill(MsContentDto content, JsonObject document, MsSubmissionTarget target)
    {
        content.Document = document;
        content.SubmissionStatus = MsSubmissionJson.ParseStatus(MsSubmissionJson.String(document, "status"));
        content.ContentHash = MsSubmissionJson.ContentHash(document);
        content.Pricing = MsSubmissionJson.ReadPricing(document);
        content.PricingV2 = target == MsSubmissionTarget.App && !MsSubmissionJson.IsKnownPriceTier(content.Pricing?.PriceId);
        content.MandatoryUpdate = MsSubmissionJson.IsMandatoryUpdate(document);
        content.Rollout = MsSubmissionJson.ReadRollout(document);

        if (target == MsSubmissionTarget.Addon)
        {
            content.AddonListings = MsSubmissionJson.ReadAddonListings(document);
            content.AddonLifetime = MsSubmissionJson.String(document, "lifetime");

            return;
        }

        content.Packages = MsSubmissionJson.ReadPackages(document, target);

        if (target == MsSubmissionTarget.App)
        {
            content.Listings = MsSubmissionJson.ReadListings(document);
            content.Images = MsSubmissionJson.ReadImages(document);
        }
    }

    private static async Task<MsContentDto> ReadProductAsync(IMicrosoftStoreClient client, string secretJson, string productId, CancellationToken cancellationToken)
    {
        var metadata = await client.GetProductMetadataAsync(secretJson, productId, cancellationToken);
        var packages = await client.GetInstallerPackagesAsync(secretJson, productId, cancellationToken);
        var status = await client.GetProductStatusAsync(secretJson, productId, cancellationToken);
        var ongoing = string.IsNullOrEmpty(status.OngoingSubmissionId) ? null : status.OngoingSubmissionId;
        MsSubmissionStatus? submissionStatus = null;

        // The ongoing submission id may outlive the publication; only a submission still in progress blocks the draft.
        if (ongoing != null)
            submissionStatus = (await client.GetProductSubmissionStatusAsync(secretJson, productId, ongoing, cancellationToken)).Status;

        return new MsContentDto
        {
            Kind = MsProductKind.MsiExe,
            Target = MsSubmissionTarget.Installer,
            ProductId = productId,
            SubmissionId = ongoing,
            SubmissionStatus = submissionStatus,
            SubmissionIsPending = submissionStatus == MsSubmissionStatus.InProgress,
            Listings = MsSubmissionJson.ReadProductListings(metadata),
            Pricing = MsSubmissionJson.ReadAvailability(metadata),
            InstallerPackages = [.. packages.Select(ToInput).OrderBy(p => p.PackageUrl, StringComparer.Ordinal)],
            ContentHash = MsSubmissionJson.ContentHash(metadata),
            Document = metadata,
        };
    }

    /// <summary>
    /// Maps an API package to the input shape.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public static MsInstallerPackageInputDto ToInput(MsInstallerPackageRecord package) => new()
    {
        PackageUrl = package.PackageUrl,
        PackageType = package.PackageType,
        Architecture = package.Architectures.FirstOrDefault(),
        Languages = package.Languages,
        IsSilentInstall = package.IsSilentInstall,
        InstallerParameters = package.InstallerParameters,
        GenericDocUrl = package.GenericDocUrl,
    };
}
