using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.MsStore.AddMsProduct;

/// <summary>
/// Validations of <see cref="AddMsProductCommand"/>.
/// </summary>
public sealed class AddMsProductCommandValidator : AbstractValidator<AddMsProductCommand>
{
    ///<inheritdoc cref="AddMsProductCommandValidator"/>
    public AddMsProductCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.ProductId)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.MsProductIdInvalid])
            .Matches("^[A-Za-z0-9]{12}$")
            .WithMessage(localizer[MessageKey.MsProductIdInvalid]);

        RuleFor(command => command.Name)
            .MaximumLength(App.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], App.NameMaxLength]);
    }
}
