using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Validation rules shared by the requests about one app in one store.
/// </summary>
public static class AppStoreValidatorExtensions
{
    /// <summary>
    /// The app must be sent and the store must be known.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="localizer"></param>
    public static void AppStoreRules<T>(this AbstractValidator<T> validator, IMilvaLocalizer localizer) where T : IAppStoreRequest
    {
        validator.RuleFor(x => x.AppId)
                 .GreaterThan(0)
                 .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        validator.RuleFor(x => x.Store)
                 .IsInEnum()
                 .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStore]]);
    }
}
