using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanExportCompliance;

/// <summary>
/// Validations of <see cref="PlanExportComplianceCommand"/>.
/// </summary>
public sealed class PlanExportComplianceCommandValidator : AbstractValidator<PlanExportComplianceCommand>
{
    ///<inheritdoc cref="PlanExportComplianceCommandValidator"/>
    public PlanExportComplianceCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
