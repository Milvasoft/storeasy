using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Mockups.GetMockupExportZip;

/// <summary>
/// Validations of <see cref="GetMockupExportZipQuery"/>.
/// </summary>
public sealed class GetMockupExportZipQueryValidator : AbstractValidator<GetMockupExportZipQuery>
{
    ///<inheritdoc cref="GetMockupExportZipQueryValidator"/>
    public GetMockupExportZipQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(zip => zip.JobId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreJob]]);
    }
}
