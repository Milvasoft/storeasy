using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Users.DeleteUser;

/// <summary>
/// Validations of <see cref="DeleteUserCommand"/>.
/// </summary>
public sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    ///<inheritdoc cref="DeleteUserCommandValidator"/>
    public DeleteUserCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.UserId)
            .NotBeDefaultData()
            .WithMessage(localizer[MessageKey.DefaultValueCannotModify]);

        RuleFor(query => query.UserId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.User]]);
    }
}