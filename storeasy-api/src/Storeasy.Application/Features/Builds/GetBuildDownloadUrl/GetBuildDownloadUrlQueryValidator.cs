using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Builds.GetBuildDownloadUrl;

/// <summary>
/// Validations of <see cref="GetBuildDownloadUrlQuery"/>.
/// </summary>
public sealed class GetBuildDownloadUrlQueryValidator : AbstractValidator<GetBuildDownloadUrlQuery>
{
    ///<inheritdoc cref="GetBuildDownloadUrlQueryValidator"/>
    public GetBuildDownloadUrlQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.BuildId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBuild]]);
    }
}
