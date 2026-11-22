using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Firefox.PlanFirefoxChange;

/// <summary>
/// Validations of <see cref="PlanFirefoxChangeCommand"/>. The rules that depend on the add-on are checked by the planner,
/// which reports every problem at once.
/// </summary>
public sealed class PlanFirefoxChangeCommandValidator : AbstractValidator<PlanFirefoxChangeCommand>
{
    /// <summary>
    /// Most values, notes or files one plan takes.
    /// </summary>
    public const int MaxEntries = 200;

    ///<inheritdoc cref="PlanFirefoxChangeCommandValidator"/>
    public PlanFirefoxChangeCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.Kind)]);

        RuleFor(command => command.Channel)
            .Must(channel => channel is FirefoxVersionChannel.Listed or FirefoxVersionChannel.Unlisted)
            .WithMessage(localizer[MessageKey.FirefoxChannelInvalid]);

        RuleFor(command => command.SessionId)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Kind == FirefoxChangeKind.Version)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.SessionId)]);

        RuleFor(command => command.IconAssetId)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Kind == FirefoxChangeKind.Icon)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.IconAssetId)]);

        RuleFor(command => command.Listing)
            .Must(values => values == null || values.Count <= MaxEntries)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.Listing)]);

        RuleFor(command => command.ReleaseNotes)
            .Must(values => values == null || values.Count <= MaxEntries)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.ReleaseNotes)]);

        RuleFor(command => command.AddPreviewAssetIds)
            .Must(values => values == null || (values.Count <= 20 && values.TrueForAll(v => v > 0)))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.AddPreviewAssetIds)]);

        RuleFor(command => command.DeletePreviewIds)
            .Must(values => values == null || (values.Count <= 50 && values.TrueForAll(v => v > 0)))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.DeletePreviewIds)]);

        RuleFor(command => command.Categories)
            .Must(values => values == null || values.Count <= 10)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.Categories)]);

        RuleFor(command => command.License)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.License)]);

        RuleFor(command => command.Version)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(PlanFirefoxChangeCommand.Version)]);

        RuleFor(command => command.ApprovalNotes)
            .MaximumLength(Utils.Firefox.FirefoxFields.ApprovalNotesMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, nameof(PlanFirefoxChangeCommand.ApprovalNotes), Utils.Firefox.FirefoxFields.ApprovalNotesMaxLength]);
    }
}
