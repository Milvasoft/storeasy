using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Artifacts.GetArtifactCounts;

/// <summary>
/// Validations of <see cref="GetArtifactCountsQuery"/>.
/// </summary>
public sealed class GetArtifactCountsQueryValidator : AbstractValidator<GetArtifactCountsQuery>
{
    ///<inheritdoc cref="GetArtifactCountsQueryValidator"/>
    public GetArtifactCountsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query)
            .Must(query => query.From == null || query.To == null || (query.From < query.To && (query.To.Value - query.From.Value).TotalDays <= GetArtifactCountsQuery.MaxDays))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalDateRange]]);
    }
}
