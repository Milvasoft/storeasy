using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Subscriptions.SyncStoreProducts;

/// <summary>
/// Validations of <see cref="SyncStoreProductsCommand"/>.
/// </summary>
public sealed class SyncStoreProductsCommandValidator : AbstractValidator<SyncStoreProductsCommand>
{
    ///<inheritdoc cref="SyncStoreProductsCommandValidator"/>
    public SyncStoreProductsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
