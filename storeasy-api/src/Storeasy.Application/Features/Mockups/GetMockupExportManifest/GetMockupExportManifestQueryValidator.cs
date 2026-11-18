using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Mockups.GetMockupExportManifest;

/// <summary>
/// Validations of <see cref="GetMockupExportManifestQuery"/>.
/// </summary>
public sealed class GetMockupExportManifestQueryValidator : AbstractValidator<GetMockupExportManifestQuery>
{
    ///<inheritdoc cref="GetMockupExportManifestQueryValidator"/>
    public GetMockupExportManifestQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.JobId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreJob]]);
    }
}
