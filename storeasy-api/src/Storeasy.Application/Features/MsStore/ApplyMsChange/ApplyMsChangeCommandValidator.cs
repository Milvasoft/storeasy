using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.ApplyMsChange;

/// <summary>
/// Validations of <see cref="ApplyMsChangeCommand"/>: the section of the change kind is present and every text fits the
/// catalog limits (<see cref="MsListingLimits.From"/>).
/// </summary>
public sealed class ApplyMsChangeCommandValidator : AbstractValidator<ApplyMsChangeCommand>
{
    /// <summary>
    /// Architectures the MSI/EXE API accepts.
    /// </summary>
    public static readonly IReadOnlySet<string> Architectures = new HashSet<string>(StringComparer.Ordinal) { "Neutral", "X86", "X64", "Arm", "Arm64" };

    ///<inheritdoc cref="ApplyMsChangeCommandValidator"/>
    public ApplyMsChangeCommandValidator(IMilvaLocalizer localizer, IRulesCatalog rulesCatalog)
    {
        var limits = MsListingLimits.From(rulesCatalog.Current);

        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.Change)
            .NotNull()
            .WithMessage(localizer[MessageKey.MsChangeSectionMissing]);

        RuleFor(command => command.BaseHash)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.MsPlanStale])
            .When(command => !command.DryRun);

        When(command => command.Change != null, () =>
        {
            RuleFor(command => command.Change.Kind)
                .IsInEnum()
                .WithMessage(localizer[MessageKey.MsChangeSectionMissing]);

            RuleFor(command => command.Change)
                .Must(HasSection)
                .WithMessage(localizer[MessageKey.MsChangeSectionMissing]);

            RuleForEach(command => command.Change.Listings)
                .Must(l => !string.IsNullOrWhiteSpace(l.Locale) && l.Locale.Length <= 20)
                .WithMessage(localizer[MessageKey.PleaseSendCorrect, "locale"])
                .Custom((listing, context) =>
                {
                    foreach (var (field, limit) in limits.Violations(listing))
                        context.AddFailure(localizer[MessageKey.MsListingLimitExceeded, field, listing.Locale, limit]);
                })
                .When(command => command.Change.Kind == MsChangeKind.Listings);

            RuleFor(command => command.Change.Images)
                .Must(i => !string.IsNullOrWhiteSpace(i.Locale)
                           && (i.Add ?? []).All(a => a.AssetId > 0 && !string.IsNullOrWhiteSpace(a.ImageType) && (a.Description?.Length ?? 0) <= limits.ImageCaption))
                .WithMessage(localizer[MessageKey.MsAssetInvalid])
                .When(command => command.Change.Kind == MsChangeKind.Images && command.Change.Images != null);

            RuleFor(command => command.Change.Packages.RolloutPercentage)
                .Must(p => p is null or (> 0 and <= 100))
                .WithMessage(localizer[MessageKey.MsRolloutPercentageInvalid])
                .When(command => command.Change.Kind == MsChangeKind.Packages && command.Change.Packages != null);

            RuleForEach(command => command.Change.InstallerPackages)
                .Must(IsValidInstaller)
                .WithMessage(localizer[MessageKey.MsInstallerInvalid])
                .When(command => command.Change.Kind == MsChangeKind.InstallerPackages);

            RuleFor(command => command.Change.Rollout)
                .Must(r => !string.IsNullOrWhiteSpace(r.SubmissionId) && Enum.IsDefined(r.Action) && (r.Action != MsRolloutAction.SetPercentage || r.Percentage is > 0 and <= 100))
                .WithMessage(localizer[MessageKey.MsRolloutPercentageInvalid])
                .When(command => command.Change.Kind == MsChangeKind.Rollout && command.Change.Rollout != null);

            RuleFor(command => command.Change.Flight)
                .Must(f => !string.IsNullOrWhiteSpace(f.FriendlyName) && f.FriendlyName.Trim().Length <= MsFlight.FriendlyNameMaxLength && f.GroupIds?.Count > 0)
                .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsFlight"]])
                .When(command => command.Change.Kind == MsChangeKind.CreateFlight && command.Change.Flight != null);

            RuleFor(command => command.Change.Addon)
                .Must(a => !string.IsNullOrWhiteSpace(a.ProductId) && a.ProductId.Trim().Length <= MsAddon.ProductIdMaxLength && a.ProductType is MsAddonType.Durable or MsAddonType.Consumable)
                .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsAddon"]])
                .When(command => command.Change.Kind == MsChangeKind.CreateAddon && command.Change.Addon != null);

            RuleFor(command => command.Change.AddonContent)
                .Must(c => (c.Listings ?? []).All(l => !string.IsNullOrWhiteSpace(l.Locale)
                                                       && (l.Title?.Length ?? 0) <= MsListingLimits.AddonTitle
                                                       && (l.Description?.Length ?? 0) <= MsListingLimits.AddonDescription))
                .WithMessage(localizer[MessageKey.MsListingLimitExceeded, "title/description", "*", $"{MsListingLimits.AddonTitle}/{MsListingLimits.AddonDescription}"])
                .When(command => command.Change.Kind == MsChangeKind.AddonContent && command.Change.AddonContent != null);
        });
    }

    /// <summary>
    /// Whether the section the change kind reads is present.
    /// </summary>
    /// <param name="change"></param>
    /// <returns></returns>
    public static bool HasSection(MsChangeInputDto change) => change.Kind switch
    {
        MsChangeKind.Listings => change.Listings?.Count > 0,
        MsChangeKind.Images => change.Images != null && ((change.Images.Add?.Count ?? 0) + (change.Images.RemoveFileNames?.Count ?? 0)) > 0,
        MsChangeKind.Packages => change.Packages != null && ((change.Packages.AddAssetIds?.Count ?? 0) + (change.Packages.RemoveFileNames?.Count ?? 0) > 0 || change.Packages.RolloutPercentage.HasValue),
        MsChangeKind.Pricing => change.Pricing != null,
        MsChangeKind.InstallerPackages => change.InstallerPackages?.Count > 0,
        MsChangeKind.Rollout => change.Rollout != null,
        MsChangeKind.CreateFlight => change.Flight != null,
        MsChangeKind.CreateAddon => change.Addon != null,
        MsChangeKind.AddonContent => change.AddonContent != null && !string.IsNullOrWhiteSpace(change.AddonStoreId),
        MsChangeKind.DiscardSubmission => true,
        _ => false,
    };

    /// <summary>
    /// Whether an installer package has what the MSI/EXE API requires: an HTTPS address, one architecture, a language,
    /// parameters unless silent, and a return code page for EXE.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public static bool IsValidInstaller(MsInstallerPackageInputDto package)
        => Uri.TryCreate(package.PackageUrl, UriKind.Absolute, out var url)
           && url.Scheme == Uri.UriSchemeHttps
           && package.PackageUrl.Length <= MsInstallerPackage.UrlMaxLength
           && Enum.IsDefined(package.PackageType)
           && package.Architecture != null && Architectures.Contains(package.Architecture)
           && package.Languages?.Count > 0
           && (package.IsSilentInstall || !string.IsNullOrWhiteSpace(package.InstallerParameters))
           && (package.InstallerParameters?.Length ?? 0) <= MsInstallerPackage.InstallerParametersMaxLength
           && (package.PackageType != MsInstallerType.Exe || Uri.TryCreate(package.GenericDocUrl, UriKind.Absolute, out _));
}
