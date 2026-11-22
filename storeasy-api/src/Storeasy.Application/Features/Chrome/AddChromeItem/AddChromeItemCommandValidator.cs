using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Chrome.AddChromeItem;

/// <summary>
/// Validations of <see cref="AddChromeItemCommand"/>.
/// </summary>
public sealed class AddChromeItemCommandValidator : AbstractValidator<AddChromeItemCommand>
{
    ///<inheritdoc cref="AddChromeItemCommandValidator"/>
    public AddChromeItemCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.ItemId)
            .Must(id => ChromeVersions.IsItemId(id?.Trim()))
            .WithMessage(localizer[MessageKey.ChromeItemIdInvalid]);

        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .When(command => command.AppId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(command => command.Name)
            .MaximumLength(App.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], App.NameMaxLength]);
    }
}
