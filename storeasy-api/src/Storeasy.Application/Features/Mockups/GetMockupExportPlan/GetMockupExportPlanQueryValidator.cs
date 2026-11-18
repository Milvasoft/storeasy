using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Mockups.GetMockupExportPlan;

/// <summary>
/// Validations of <see cref="GetMockupExportPlanQuery"/>.
/// </summary>
public sealed class GetMockupExportPlanQueryValidator : AbstractValidator<GetMockupExportPlanQuery>
{
    ///<inheritdoc cref="GetMockupExportPlanQueryValidator"/>
    public GetMockupExportPlanQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        this.RuleForMockupExportSelection(query => query.Locales, query => query.ClassKeys, query => query.Targets, localizer);
    }
}
