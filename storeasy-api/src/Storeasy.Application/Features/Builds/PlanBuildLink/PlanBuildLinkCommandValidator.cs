using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.PlanBuildLink;

/// <summary>
/// Validations of <see cref="PlanBuildLinkCommand"/>.
/// </summary>
public sealed class PlanBuildLinkCommandValidator : AbstractValidator<PlanBuildLinkCommand>
{
    ///<inheritdoc cref="PlanBuildLinkCommandValidator"/>
    public PlanBuildLinkCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.BuildId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBuild]]);
    }
}
