using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.GetBuildList;

/// <summary>
/// Validations of <see cref="GetBuildListQuery"/>.
/// </summary>
public sealed class GetBuildListQueryValidator : AbstractValidator<GetBuildListQuery>
{
    ///<inheritdoc cref="GetBuildListQueryValidator"/>
    public GetBuildListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
