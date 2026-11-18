using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetPlayTesters;

/// <summary>
/// Validations of <see cref="GetPlayTestersQuery"/>.
/// </summary>
public sealed class GetPlayTestersQueryValidator : AbstractValidator<GetPlayTestersQuery>
{
    ///<inheritdoc cref="GetPlayTestersQueryValidator"/>
    public GetPlayTestersQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Track)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.TrackInvalid]);
    }
}
