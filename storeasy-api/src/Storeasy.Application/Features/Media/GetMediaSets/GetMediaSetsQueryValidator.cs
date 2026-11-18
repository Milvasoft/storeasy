using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.GetMediaSets;

/// <summary>
/// Validations of <see cref="GetMediaSetsQuery"/>.
/// </summary>
public sealed class GetMediaSetsQueryValidator : AbstractValidator<GetMediaSetsQuery>
{
    ///<inheritdoc cref="GetMediaSetsQueryValidator"/>
    public GetMediaSetsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalClassKey]]);
    }
}
