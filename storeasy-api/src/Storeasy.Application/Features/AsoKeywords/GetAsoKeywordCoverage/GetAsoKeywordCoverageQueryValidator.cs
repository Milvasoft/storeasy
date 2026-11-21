using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordCoverage;

/// <summary>
/// Validations of <see cref="GetAsoKeywordCoverageQuery"/>.
/// </summary>
public sealed class GetAsoKeywordCoverageQueryValidator : AbstractValidator<GetAsoKeywordCoverageQuery>
{
    ///<inheritdoc cref="GetAsoKeywordCoverageQueryValidator"/>
    public GetAsoKeywordCoverageQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
