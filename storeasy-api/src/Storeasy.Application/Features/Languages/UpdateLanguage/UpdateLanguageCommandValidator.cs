using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Application.Features.Roles.UpdateRole;

namespace Storeasy.Application.Features.Languages.UpdateLanguage;

/// <summary>
/// Validations of <see cref="UpdateLanguageCommand"/>.
/// </summary>
public sealed class UpdateLanguageCommandValidator : AbstractValidator<UpdateLanguageCommand>
{
    ///<inheritdoc cref="UpdateRoleCommandValidator"/>
    public UpdateLanguageCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Id)
            .NotBeDefaultData()
            .WithMessage(localizer[MessageKey.DefaultValueCannotModify]);

        RuleFor(query => query.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.Language]]);
    }
}