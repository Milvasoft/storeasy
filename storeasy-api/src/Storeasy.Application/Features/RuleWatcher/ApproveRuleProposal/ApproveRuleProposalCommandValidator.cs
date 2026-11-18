using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.RuleWatcher.ApproveRuleProposal;

/// <summary>
/// Validations of <see cref="ApproveRuleProposalCommand"/>.
/// </summary>
public sealed class ApproveRuleProposalCommandValidator : AbstractValidator<ApproveRuleProposalCommand>
{
    ///<inheritdoc cref="ApproveRuleProposalCommandValidator"/>
    public ApproveRuleProposalCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Note)
            .MaximumLength(1000)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
