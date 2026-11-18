using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Organizations;
using System.Linq.Expressions;

namespace Storeasy.Application.Behaviours;

/// <summary>
/// Validation rules shared by the custom role requests (name, description, permissions).
/// </summary>
public static class OrganizationRoleValidationExtensions
{
    /// <summary>
    /// Name required (at most <see cref="OrganizationRole.NameMaxLength"/>), description at most
    /// <see cref="OrganizationRole.DescriptionMaxLength"/>, and permissions only from
    /// <see cref="OrgRoleRules.CustomAssignable"/> (never the owner only permission).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="permissions"></param>
    /// <param name="localizer"></param>
    public static void RuleForOrganizationRole<T>(this AbstractValidator<T> validator,
                                                  Expression<Func<T, string>> name,
                                                  Expression<Func<T, string>> description,
                                                  Expression<Func<T, List<OrgPermission>>> permissions,
                                                  IMilvaLocalizer localizer)
    {
        validator.RuleFor(name)
                 .NotNullOrEmpty(localizer, MessageKey.GlobalName)
                 .MaximumLength(OrganizationRole.NameMaxLength)
                 .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], OrganizationRole.NameMaxLength]);

        validator.RuleFor(description)
                 .MaximumLength(OrganizationRole.DescriptionMaxLength)
                 .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalDescription], OrganizationRole.DescriptionMaxLength]);

        validator.RuleFor(permissions)
                 .NotNull()
                 .Must(list => list.All(OrgRoleRules.CustomAssignable.Contains))
                 .WithMessage(localizer[MessageKey.OrganizationRolePermissionInvalid]);
    }
}
