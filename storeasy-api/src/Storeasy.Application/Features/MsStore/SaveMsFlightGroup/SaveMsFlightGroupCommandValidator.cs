using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.SaveMsFlightGroup;

/// <summary>
/// Validations of <see cref="SaveMsFlightGroupCommand"/>.
/// </summary>
public sealed class SaveMsFlightGroupCommandValidator : AbstractValidator<SaveMsFlightGroupCommand>
{
    ///<inheritdoc cref="SaveMsFlightGroupCommandValidator"/>
    public SaveMsFlightGroupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.GroupId)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsFlightGroup"]])
            .MaximumLength(MsProduct.IdMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsFlightGroup"]]);

        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(MsFlightGroup.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], MsFlightGroup.NameMaxLength]);

        RuleFor(command => command.MemberCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsFlightGroup"]])
            .When(command => command.MemberCount.HasValue);
    }
}
