using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.RuleWatcher.RejectRuleProposal;

/// <summary>
/// Validations of <see cref="RejectRuleProposalCommand"/>.
/// </summary>
public sealed class RejectRuleProposalCommandValidator : AbstractValidator<RejectRuleProposalCommand>
{
    ///<inheritdoc cref="RejectRuleProposalCommandValidator"/>
    public RejectRuleProposalCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Reason)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRuleProposal]]);

        RuleFor(command => command.Note)
            .MaximumLength(1000)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
