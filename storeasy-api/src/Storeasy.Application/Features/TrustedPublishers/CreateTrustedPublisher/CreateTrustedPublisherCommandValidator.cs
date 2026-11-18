using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.TrustedPublishers.CreateTrustedPublisher;

/// <summary>
/// Validations of <see cref="CreateTrustedPublisherCommand"/>.
/// </summary>
public sealed class CreateTrustedPublisherCommandValidator : AbstractValidator<CreateTrustedPublisherCommand>
{
    ///<inheritdoc cref="CreateTrustedPublisherCommandValidator"/>
    public CreateTrustedPublisherCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Provider)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[nameof(OidcProvider)]]);

        RuleFor(x => x.Repository)
            .Must(repository => !string.IsNullOrWhiteSpace(repository) && repository.Trim().Contains('/') && repository.Length <= TrustedPublisher.FieldMaxLength && !repository.Contains('*'))
            .When(x => x.Provider != OidcProvider.AzureDevOps)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRepository]]);

        RuleFor(x => x.Branch)
            .Must(branch => !string.IsNullOrWhiteSpace(branch) && branch.Length <= TrustedPublisher.FieldMaxLength && branch != "*")
            .When(x => x.Provider != OidcProvider.AzureDevOps && string.IsNullOrWhiteSpace(x.Environment))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBranch]]);

        RuleFor(x => x.Environment)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalEnvironment]]);

        RuleFor(x => x.Workflow)
            .MaximumLength(300)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRepository]]);

        RuleFor(x => x.ServiceConnection)
            .Must((command, connection) => !string.IsNullOrWhiteSpace(command.Organization) && !string.IsNullOrWhiteSpace(command.Project) && !string.IsNullOrWhiteSpace(connection)
                                           && (command.Organization + command.Project + connection).Length <= 400 && !(command.Organization + command.Project + connection).Contains('*'))
            .When(x => x.Provider == OidcProvider.AzureDevOps)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalServiceConnection]]);

        RuleFor(x => x.Issuer)
            .Must(issuer => Uri.TryCreate(issuer, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.Query) && issuer.Length <= 300)
            .When(x => x.Provider == OidcProvider.GitLab && !string.IsNullOrWhiteSpace(x.Issuer))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalIssuer]]);
    }
}
