using Storeasy.Application.Dtos.MsStoreDtos;
using System.Text.Json;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Turns a Microsoft Store change and the current store content into a plan (dry run): the field level diff, the
/// remarks, the store calls, the permission it needs, the fingerprint of the content it was made from and, when it cannot
/// be applied now, why. Pure: no store call, no database. The request handler plans; the apply job reads the store again,
/// plans again and refuses when <see cref="MsChangePlanDto.BaseHash"/> changed.
/// </summary>
public static class MsChangePlanner
{
    /// <summary>
    /// Longest value shown in a diff item.
    /// </summary>
    public const int PreviewLength = 120;

    private const string _add = "add";
    private const string _update = "update";
    private const string _remove = "remove";

    /// <summary>
    /// Change kinds only MSIX products have.
    /// </summary>
    public static readonly IReadOnlySet<MsChangeKind> MsixOnly = new HashSet<MsChangeKind>
    {
        MsChangeKind.Images,
        MsChangeKind.Packages,
        MsChangeKind.Rollout,
        MsChangeKind.CreateFlight,
        MsChangeKind.CreateAddon,
        MsChangeKind.AddonContent,
        MsChangeKind.DiscardSubmission,
    };

    /// <summary>
    /// Change kinds that edit a submission's content (and so need a draft submission).
    /// </summary>
    public static readonly IReadOnlySet<MsChangeKind> EditsSubmission = new HashSet<MsChangeKind>
    {
        MsChangeKind.Listings,
        MsChangeKind.Images,
        MsChangeKind.Packages,
        MsChangeKind.Pricing,
        MsChangeKind.AddonContent,
    };

    /// <summary>
    /// Whether <paramref name="kind"/> can be made on a product managed by <paramref name="productKind"/>.
    /// </summary>
    /// <param name="productKind"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static bool Supports(MsProductKind productKind, MsChangeKind kind)
        => productKind == MsProductKind.Msix ? kind != MsChangeKind.InstallerPackages : !MsixOnly.Contains(kind);

    /// <summary>
    /// The submission target a change writes to.
    /// </summary>
    /// <param name="productKind"></param>
    /// <param name="change"></param>
    /// <returns></returns>
    public static MsSubmissionTarget TargetOf(MsProductKind productKind, MsChangeInputDto change)
    {
        if (productKind == MsProductKind.MsiExe)
            return MsSubmissionTarget.Installer;

        return change.Kind switch
        {
            MsChangeKind.CreateFlight => MsSubmissionTarget.Flight,
            MsChangeKind.CreateAddon or MsChangeKind.AddonContent => MsSubmissionTarget.Addon,
            MsChangeKind.Packages or MsChangeKind.Rollout => string.IsNullOrEmpty(change.FlightId) ? MsSubmissionTarget.App : MsSubmissionTarget.Flight,
            MsChangeKind.DiscardSubmission => !string.IsNullOrEmpty(change.FlightId) ? MsSubmissionTarget.Flight : !string.IsNullOrEmpty(change.AddonStoreId) ? MsSubmissionTarget.Addon : MsSubmissionTarget.App,
            _ => MsSubmissionTarget.App,
        };
    }

    /// <summary>
    /// The permission applying a change needs: rollout of an app submission reaches customers right away
    /// (<see cref="OrgPermission.ReleaseProduction"/>), flights are test audiences (<see cref="OrgPermission.ReleaseTest"/>),
    /// everything else edits a draft (<see cref="OrgPermission.EditContent"/>; sending the draft needs
    /// <see cref="OrgPermission.SubmitForReview"/> separately).
    /// </summary>
    /// <param name="change"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static OrgPermission RequiredPermission(MsChangeInputDto change, MsSubmissionTarget target) => change.Kind switch
    {
        MsChangeKind.Rollout => target == MsSubmissionTarget.Flight ? OrgPermission.ReleaseTest : OrgPermission.ReleaseProduction,
        MsChangeKind.CreateFlight => OrgPermission.ReleaseTest,
        MsChangeKind.Packages when target == MsSubmissionTarget.Flight => OrgPermission.ReleaseTest,
        _ => OrgPermission.EditContent,
    };

    /// <summary>
    /// Fingerprint of the part of <paramref name="content"/> a change of <paramref name="kind"/> is based on.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static string BaseHash(MsContentDto content, MsChangeKind kind)
    {
        object section = kind switch
        {
            MsChangeKind.Listings => content.Listings,
            MsChangeKind.Images => content.Images,
            MsChangeKind.Packages => new { content.Packages, content.Rollout },
            MsChangeKind.Pricing => content.Pricing,
            MsChangeKind.InstallerPackages => content.InstallerPackages,
            MsChangeKind.Rollout => content.Rollout,
            MsChangeKind.AddonContent => new { content.AddonListings, content.AddonLifetime, content.Pricing },
            MsChangeKind.DiscardSubmission => content.SubmissionStatus,
            _ => null,
        };

        return MsSubmissionJson.Hash(JsonSerializer.Serialize(new { kind, content.SubmissionId, content.SubmissionIsPending, section }));
    }

    /// <summary>
    /// Plans <paramref name="change"/> on <paramref name="content"/>.
    /// </summary>
    /// <param name="storeAppId"></param>
    /// <param name="content"></param>
    /// <param name="change"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static MsChangePlanDto Plan(int storeAppId, MsContentDto content, MsChangeInputDto change, MsPlanContext context)
    {
        var target = TargetOf(context.Kind, change);
        var plan = new MsChangePlanDto
        {
            StoreAppId = storeAppId,
            Kind = change.Kind,
            Target = target,
            SubmissionId = content.SubmissionId,
            BaseHash = BaseHash(content, change.Kind),
            RequiredPermission = RequiredPermission(change, target),
            DryRun = true,
        };

        switch (change.Kind)
        {
            case MsChangeKind.Listings:
                PlanListings(plan, content, change.Listings ?? [], context.Kind);
                break;
            case MsChangeKind.Images:
                PlanImages(plan, content, change.Images, context);
                break;
            case MsChangeKind.Packages:
                PlanPackages(plan, content, change.Packages, target, context);
                break;
            case MsChangeKind.Pricing:
                PlanPricing(plan, content, change.Pricing, context.Kind);
                break;
            case MsChangeKind.InstallerPackages:
                PlanInstallerPackages(plan, content, change.InstallerPackages ?? []);
                break;
            case MsChangeKind.Rollout:
                PlanRollout(plan, content, change.Rollout, change.FlightId, context.ProductId);
                break;
            case MsChangeKind.CreateFlight:
                PlanFlight(plan, change.Flight, context);
                break;
            case MsChangeKind.CreateAddon:
                PlanAddon(plan, change.Addon, context);
                break;
            case MsChangeKind.AddonContent:
                PlanAddonContent(plan, content, change.AddonContent);
                break;
            case MsChangeKind.DiscardSubmission:
                PlanDiscard(plan, content, target, change, context.ProductId);
                break;
        }

        ApplyBlockingRules(plan, content, change, target, context);

        return plan;
    }

    private static void ApplyBlockingRules(MsChangePlanDto plan, MsContentDto content, MsChangeInputDto change, MsSubmissionTarget target, MsPlanContext context)
    {
        if (context.Kind == MsProductKind.MsiExe)
        {
            if (content.SubmissionIsPending)
                Block(plan, MessageKey.MsSubmissionInProgress);

            return;
        }

        if (target == MsSubmissionTarget.App && change.Kind != MsChangeKind.DiscardSubmission)
        {
            if (!context.FirstSubmissionDone)
                Block(plan, MessageKey.MsFirstSubmissionRequired);

            if (context.HasMandatoryUpdate || content.MandatoryUpdate)
                Block(plan, MessageKey.MsApiRefused, MsAssistedReason.MandatoryUpdate);
        }

        if (target == MsSubmissionTarget.Addon && change.Kind == MsChangeKind.AddonContent && !context.AddonWritable)
            Block(plan, MessageKey.MsAddonAssisted, context.AddonAssistedReason ?? MsAssistedReason.StoreManagedConsumable);

        if (change.Kind == MsChangeKind.Pricing && context.Kind == MsProductKind.Msix && (context.PricingV2 || content.PricingV2))
            Block(plan, MessageKey.MsPricingAssisted, MsAssistedReason.PricingV2);

        if (!EditsSubmission.Contains(change.Kind) && change.Kind != MsChangeKind.DiscardSubmission)
            return;

        if (!content.SubmissionIsPending)
        {
            if (EditsSubmission.Contains(change.Kind))
            {
                plan.CreatesSubmission = true;
                Warn(plan, MessageKey.MsWarnCreatesSubmission);
            }

            return;
        }

        if (content.SubmissionStatus != MsSubmissionStatus.PendingCommit)
        {
            Block(plan, MessageKey.MsSubmissionInProgress);
        }
        else if (context.Pending?.ModifiedOutsideApi == true || (context.Pending?.CreatedByStoreasy == true && context.Pending.ContentHash != null && context.Pending.ContentHash != content.ContentHash))
        {
            // Partner Center rule: a submission created through the API and then changed in Partner Center cannot be changed
            // or committed through the API anymore; it can only be deleted.
            if (change.Kind != MsChangeKind.DiscardSubmission)
                Block(plan, MessageKey.MsSubmissionModifiedOutsideApi, MsAssistedReason.ManualEdit);
        }
        else if (context.Pending?.CreatedByStoreasy != true && change.Kind != MsChangeKind.DiscardSubmission)
        {
            Block(plan, MessageKey.MsSubmissionNotOurs);
        }

        if (EditsSubmission.Contains(change.Kind))
            Warn(plan, MessageKey.MsWarnSubmitSeparately);
    }

    private static void PlanListings(MsChangePlanDto plan, MsContentDto content, List<MsListingDto> listings, MsProductKind productKind)
    {
        foreach (var desired in listings)
        {
            var locale = desired.Locale.ToLowerInvariant();
            var current = content.Listings.FirstOrDefault(l => l.Locale == locale);

            if (desired.Title != null && productKind == MsProductKind.MsiExe)
                plan.Warnings.Add(new MsPlanWarningDto { Key = MessageKey.MsWrongProductKind, Field = "title", Locale = locale });

            Diff(plan, "listing", locale, "title", current?.Title, productKind == MsProductKind.Msix ? desired.Title : null);
            Diff(plan, "listing", locale, "description", current?.Description, desired.Description);
            Diff(plan, "listing", locale, "shortDescription", current?.ShortDescription, desired.ShortDescription);
            Diff(plan, "listing", locale, "whatsNew", current?.WhatsNew, desired.WhatsNew);
            Diff(plan, "listing", locale, "features", Join(current?.Features), desired.Features == null ? null : Join(desired.Features));
            Diff(plan, "listing", locale, "searchTerms", Join(current?.SearchTerms), desired.SearchTerms == null ? null : Join(desired.SearchTerms));
        }

        if (productKind == MsProductKind.Msix)
        {
            AddSubmissionCalls(plan, content, MsSubmissionTarget.App, uploadsFiles: false);
        }
        else
        {
            foreach (var locale in listings.Select(l => l.Locale.ToLowerInvariant()).Distinct())
                plan.Calls.Add(new MsPlanCallDto { Method = "PATCH", Path = $"submission/v1/product/{content.ProductId}/metadata", Target = $"listings.{locale}" });
        }
    }

    private static void PlanImages(MsChangePlanDto plan, MsContentDto content, MsImageChangeDto images, MsPlanContext context)
    {
        var locale = images.Locale.ToLowerInvariant();
        var current = content.Images.Where(i => i.Locale == locale).ToList();
        var removed = (images.RemoveFileNames ?? []).ToHashSet(StringComparer.Ordinal);

        foreach (var fileName in removed)
        {
            var image = current.FirstOrDefault(i => i.FileName == fileName);

            if (image == null)
            {
                Block(plan, MessageKey.MsAssetInvalid);
                plan.Warnings.Add(new MsPlanWarningDto { Key = MessageKey.MsAssetInvalid, Field = fileName, Locale = locale });
                continue;
            }

            plan.Items.Add(Item("image", locale, image.ImageType, image.FileName, null, _remove));
        }

        foreach (var image in images.Add ?? [])
            plan.Items.Add(Item("image", locale, image.ImageType, null, context.AssetFileNames.GetValueOrDefault(image.AssetId), _add));

        var remaining = current.Where(i => !removed.Contains(i.FileName)).Select(i => i.ImageType).Concat((images.Add ?? []).Select(i => i.ImageType));

        foreach (var group in remaining.GroupBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            var limit = context.Limits.ScreenshotLimit(group.Key);

            if (limit.HasValue && group.Count() > limit.Value)
            {
                Block(plan, MessageKey.MsTooManyImages);
                plan.Warnings.Add(new MsPlanWarningDto { Key = MessageKey.MsTooManyImages, Field = group.Key, Locale = locale });
            }
        }

        AddSubmissionCalls(plan, content, MsSubmissionTarget.App, uploadsFiles: (images.Add?.Count ?? 0) > 0);
    }

    private static void PlanPackages(MsChangePlanDto plan, MsContentDto content, MsPackageChangeDto packages, MsSubmissionTarget target, MsPlanContext context)
    {
        foreach (var fileName in packages.RemoveFileNames ?? [])
        {
            var package = content.Packages.FirstOrDefault(p => p.FileName == fileName);

            if (package == null)
            {
                Block(plan, MessageKey.MsAssetInvalid);
                plan.Warnings.Add(new MsPlanWarningDto { Key = MessageKey.MsAssetInvalid, Field = fileName });
                continue;
            }

            plan.Items.Add(Item("package", null, package.Architecture ?? "package", $"{package.FileName} {package.Version}".Trim(), null, _remove));
        }

        foreach (var assetId in packages.AddAssetIds ?? [])
            plan.Items.Add(Item("package", null, "package", null, context.AssetFileNames.GetValueOrDefault(assetId), _add));

        if (target == MsSubmissionTarget.App && packages.RolloutPercentage.HasValue)
            Diff(plan, "rollout", null, "packageRolloutPercentage", content.Rollout?.IsPackageRollout == true ? Number(content.Rollout.Percentage) : null, Number(packages.RolloutPercentage.Value));

        // Release notes go into every listing language the submission already has.
        if (target == MsSubmissionTarget.App && packages.ReleaseNotes != null)
        {
            foreach (var listing in content.Listings)
                Diff(plan, "listing", listing.Locale, "whatsNew", listing.WhatsNew, packages.ReleaseNotes);
        }

        AddSubmissionCalls(plan, content, target, uploadsFiles: (packages.AddAssetIds?.Count ?? 0) > 0);
    }

    private static void PlanPricing(MsChangePlanDto plan, MsContentDto content, MsPricingDto pricing, MsProductKind productKind)
    {
        var current = content.Pricing ?? new MsPricingDto();

        if (productKind == MsProductKind.Msix)
        {
            Diff(plan, "pricing", null, "priceId", current.PriceId, pricing.PriceId);
            Diff(plan, "pricing", null, "trialPeriod", current.TrialPeriod, pricing.TrialPeriod);
            Diff(plan, "pricing", null, "visibility", current.Visibility, pricing.Visibility);

            AddSubmissionCalls(plan, content, MsSubmissionTarget.App, uploadsFiles: false);

            return;
        }

        Diff(plan, "availability", null, "markets", Join(current.Markets?.Order(StringComparer.Ordinal)), pricing.Markets == null ? null : Join(pricing.Markets.Select(m => m.ToUpperInvariant()).Order(StringComparer.Ordinal)));
        Diff(plan, "availability", null, "discoverability", current.Discoverability, pricing.Discoverability);
        Diff(plan, "availability", null, "pricing", current.PricingModel, pricing.PricingModel);
        Diff(plan, "availability", null, "freeTrial", current.FreeTrial, pricing.FreeTrial);

        plan.Calls.Add(new MsPlanCallDto { Method = "PATCH", Path = $"submission/v1/product/{content.ProductId}/metadata", Target = "availability" });
    }

    private static void PlanInstallerPackages(MsChangePlanDto plan, MsContentDto content, List<MsInstallerPackageInputDto> packages)
    {
        var current = content.InstallerPackages.ToDictionary(p => p.PackageUrl, StringComparer.Ordinal);
        var desired = packages.ToDictionary(p => p.PackageUrl, StringComparer.Ordinal);

        foreach (var url in current.Keys.Where(u => !desired.ContainsKey(u)))
            plan.Items.Add(Item("installer", null, "packageUrl", url, null, _remove));

        foreach (var package in packages)
        {
            if (!current.TryGetValue(package.PackageUrl, out var existing))
            {
                plan.Items.Add(Item("installer", null, "packageUrl", null, package.PackageUrl, _add));
                plan.Warnings.Add(new MsPlanWarningDto { Key = MessageKey.MsWarnInstallerUnchecked, Field = package.PackageUrl });
                continue;
            }

            Diff(plan, "installer", null, $"{package.PackageUrl} architecture", existing.Architecture, package.Architecture);
            Diff(plan, "installer", null, $"{package.PackageUrl} languages", Join(existing.Languages), Join(package.Languages));
            Diff(plan, "installer", null, $"{package.PackageUrl} isSilentInstall", existing.IsSilentInstall.ToString(), package.IsSilentInstall.ToString());
            Diff(plan, "installer", null, $"{package.PackageUrl} installerParameters", existing.InstallerParameters ?? string.Empty, package.InstallerParameters ?? string.Empty);
            Diff(plan, "installer", null, $"{package.PackageUrl} packageType", existing.PackageType.ToString(), package.PackageType.ToString());
        }

        plan.Calls.Add(new MsPlanCallDto { Method = "PUT", Path = $"submission/v1/product/{content.ProductId}/packages", Target = "packages" });
        plan.Calls.Add(new MsPlanCallDto { Method = "POST", Path = $"submission/v1/product/{content.ProductId}/packages/commit", Target = "packages" });
    }

    private static void PlanRollout(MsChangePlanDto plan, MsContentDto content, MsRolloutChangeDto rollout, string flightId, string productId)
    {
        plan.SubmissionId = rollout.SubmissionId;

        if (content.Rollout?.IsPackageRollout != true)
        {
            Block(plan, MessageKey.MsSubmissionNotFound);
            return;
        }

        var after = rollout.Action switch
        {
            MsRolloutAction.SetPercentage => Number(rollout.Percentage ?? 0),
            MsRolloutAction.Complete => "100",
            _ => Number(content.Rollout.Percentage),
        };

        Diff(plan, "rollout", null, "packageRolloutPercentage", Number(content.Rollout.Percentage), after);
        Diff(plan, "rollout", null, "packageRolloutStatus", content.Rollout.Status?.ToString(), rollout.Action switch
        {
            MsRolloutAction.Halt => MsRolloutStatus.Stopped.ToString(),
            MsRolloutAction.Complete => MsRolloutStatus.Complete.ToString(),
            _ => MsRolloutStatus.InProgress.ToString(),
        });

        var owner = new MsSubmissionRef { Target = string.IsNullOrEmpty(flightId) ? MsSubmissionTarget.App : MsSubmissionTarget.Flight, ApplicationId = productId, FlightId = flightId, SubmissionId = rollout.SubmissionId };
        var action = rollout.Action switch
        {
            MsRolloutAction.Halt => "haltpackagerollout",
            MsRolloutAction.Complete => "finalizepackagerollout",
            _ => "updatepackagerolloutpercentage",
        };

        plan.Calls.Add(new MsPlanCallDto { Method = "POST", Path = $"{owner.Path()}/{action}", Target = "packageRollout" });
    }

    private static void PlanFlight(MsChangePlanDto plan, MsFlightInputDto flight, MsPlanContext context)
    {
        if (context.FlightNames.Contains(flight.FriendlyName.Trim(), StringComparer.OrdinalIgnoreCase))
            Block(plan, MessageKey.MsFlightNameTaken);

        plan.Items.Add(Item("flight", null, "friendlyName", null, flight.FriendlyName.Trim(), _add));
        plan.Items.Add(Item("flight", null, "groupIds", null, Join(flight.GroupIds), _add));

        if (!string.IsNullOrEmpty(flight.RankHigherThan))
            plan.Items.Add(Item("flight", null, "rankHigherThan", null, flight.RankHigherThan, _add));

        plan.Calls.Add(new MsPlanCallDto { Method = "POST", Path = $"applications/{context.ProductId}/flights", Target = "flight" });
    }

    private static void PlanAddon(MsChangePlanDto plan, MsAddonInputDto addon, MsPlanContext context)
    {
        if (context.AddonProductIds.Contains(addon.ProductId.Trim(), StringComparer.OrdinalIgnoreCase))
            Block(plan, MessageKey.MsAddonProductIdTaken);

        plan.Items.Add(Item("addon", null, "productId", null, addon.ProductId.Trim(), _add));
        plan.Items.Add(Item("addon", null, "productType", null, addon.ProductType.ToString(), _add));
        plan.Calls.Add(new MsPlanCallDto { Method = "POST", Path = "inappproducts", Target = "addon" });
    }

    private static void PlanAddonContent(MsChangePlanDto plan, MsContentDto content, MsAddonContentDto addonContent)
    {
        foreach (var listing in addonContent.Listings ?? [])
        {
            var locale = listing.Locale.ToLowerInvariant();
            var current = content.AddonListings.FirstOrDefault(l => l.Locale == locale);

            Diff(plan, "addon", locale, "title", current?.Title, listing.Title);
            Diff(plan, "addon", locale, "description", current?.Description, listing.Description);
        }

        Diff(plan, "addon", null, "priceId", content.Pricing?.PriceId, addonContent.PriceId);
        Diff(plan, "addon", null, "lifetime", content.AddonLifetime, addonContent.Lifetime);

        AddSubmissionCalls(plan, content, MsSubmissionTarget.Addon, uploadsFiles: false);
    }

    private static void PlanDiscard(MsChangePlanDto plan, MsContentDto content, MsSubmissionTarget target, MsChangeInputDto change, string productId)
    {
        if (!content.SubmissionIsPending)
        {
            Block(plan, MessageKey.MsNoPendingSubmission);
            return;
        }

        plan.Items.Add(Item("submission", null, "submission", content.SubmissionId, null, _remove));

        var submission = new MsSubmissionRef { Target = target, ApplicationId = productId, FlightId = change.FlightId, AddonStoreId = change.AddonStoreId, SubmissionId = content.SubmissionId };

        plan.Calls.Add(new MsPlanCallDto { Method = "DELETE", Path = submission.Path(), Target = "submission" });
    }

    private static void AddSubmissionCalls(MsChangePlanDto plan, MsContentDto content, MsSubmissionTarget target, bool uploadsFiles)
    {
        var path = target switch
        {
            MsSubmissionTarget.Flight => "applications/{id}/flights/{flightId}/submissions",
            MsSubmissionTarget.Addon => "inappproducts/{id}/submissions",
            _ => "applications/{id}/submissions",
        };

        if (!content.SubmissionIsPending)
            plan.Calls.Add(new MsPlanCallDto { Method = "POST", Path = path, Target = "submission" });

        plan.Calls.Add(new MsPlanCallDto { Method = "PUT", Path = $"{path}/{{submissionId}}", Target = "submission" });

        if (uploadsFiles)
            plan.Calls.Add(new MsPlanCallDto { Method = "PUT", Path = "fileUploadUrl (Azure Blob SAS)", Target = "files.zip" });
    }

    private static void Diff(MsChangePlanDto plan, string group, string locale, string field, string before, string after)
    {
        if (after == null || string.Equals(before ?? string.Empty, after, StringComparison.Ordinal))
            return;

        plan.Items.Add(Item(group, locale, field, before, after, string.IsNullOrEmpty(before) ? _add : after.Length == 0 ? _remove : _update));
    }

    private static MsPlanItemDto Item(string group, string locale, string field, string before, string after, string action) => new()
    {
        Group = group,
        Locale = locale,
        Field = field,
        Before = Shorten(before),
        After = Shorten(after),
        Action = action,
    };

    private static void Block(MsChangePlanDto plan, string key, MsAssistedReason? reason = null)
    {
        plan.Blocked = true;
        plan.BlockKey ??= key;
        plan.AssistedReason ??= reason;
    }

    private static void Warn(MsChangePlanDto plan, string key)
    {
        if (!plan.Warnings.Any(w => w.Key == key && w.Field == null))
            plan.Warnings.Add(new MsPlanWarningDto { Key = key });
    }

    private static string Join(IEnumerable<string> values) => values == null ? null : string.Join(" | ", values);

    private static string Number(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string Shorten(string value) => value?.Length > PreviewLength ? value[..PreviewLength] + "..." : value;
}
