using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Winget.PlanWingetChange;

/// <summary>
/// Validations of <see cref="PlanWingetChangeCommand"/>. The manifest rules themselves are checked by the planner, which
/// reports every problem at once.
/// </summary>
public sealed class PlanWingetChangeCommandValidator : AbstractValidator<PlanWingetChangeCommand>
{
    /// <summary>
    /// Most installers of one version.
    /// </summary>
    public const int MaxInstallers = 64;

    /// <summary>
    /// Most locales edited at once.
    /// </summary>
    public const int MaxLocales = 100;

    ///<inheritdoc cref="PlanWingetChangeCommandValidator"/>
    public PlanWingetChangeCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanWingetChangeCommand.Kind)]);

        RuleFor(command => command.Version)
            .NotEmpty()
            .When(command => command.Kind == WingetChangeKind.NewVersion)
            .WithMessage(localizer[MessageKey.WingetFieldRequired, "PackageVersion", "version"]);

        RuleFor(command => command.Version)
            .MaximumLength(128)
            .WithMessage(localizer[MessageKey.WingetFieldTooLong, "PackageVersion", 128]);

        RuleFor(command => command.Installers)
            .Must(list => list == null || list.Count <= MaxInstallers)
            .WithMessage(localizer[MessageKey.MaxLengthReached, nameof(PlanWingetChangeCommand.Installers), MaxInstallers]);

        RuleForEach(command => command.Installers)
            .Must(installer => installer != null
                               && (installer.Url?.Length ?? 0) <= 2048
                               && (installer.SilentSwitches?.Length ?? 0) <= 512
                               && (installer.SilentWithProgressSwitches?.Length ?? 0) <= 512
                               && (installer.ProductCode?.Length ?? 0) <= 255)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanWingetChangeCommand.Installers)]);

        RuleFor(command => command.Locales)
            .Must(list => list == null || list.Count <= MaxLocales)
            .WithMessage(localizer[MessageKey.MaxLengthReached, nameof(PlanWingetChangeCommand.Locales), MaxLocales]);

        RuleForEach(command => command.Locales)
            .Must(locale => locale != null && (locale.Fields?.Values.All(v => (v?.Length ?? 0) <= 10000) ?? true) && (locale.Tags?.Count ?? 0) <= 100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanWingetChangeCommand.Locales)]);
    }
}
