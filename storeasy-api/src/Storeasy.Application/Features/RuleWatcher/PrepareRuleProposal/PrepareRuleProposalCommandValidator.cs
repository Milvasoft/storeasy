using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.PrepareRuleProposal;

/// <summary>
/// Validations of <see cref="PrepareRuleProposalCommand"/>.
/// </summary>
public sealed class PrepareRuleProposalCommandValidator : AbstractValidator<PrepareRuleProposalCommand>
{
    ///<inheritdoc cref="PrepareRuleProposalCommandValidator"/>
    public PrepareRuleProposalCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.RuleIds)
            .NotNull()
            .Must(ids => ids.Count <= 100 && ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 200))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRuleProposal]]);

        RuleFor(command => command.CatalogPatch)
            .MaximumLength(RuleProposal.ExcerptMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleFor(command => command.Note)
            .MaximumLength(2000)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
