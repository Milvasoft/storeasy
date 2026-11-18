using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Email;
using System.Globalization;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IAccountMailer"/>. Tokens are random (<see cref="SecureTokens"/>), stored as SHA-256 hashes only and
/// put into links to the web app (<see cref="AppOptions.BaseUrl"/>). Delivery failures are logged without the link.
/// </summary>
/// <param name="tokenRepository"></param>
/// <param name="emailSender"></param>
/// <param name="localizer"></param>
/// <param name="appOptions"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public class AccountMailer(IStoreasyRepositoryBase<AccountToken> tokenRepository,
                           IEmailSender emailSender,
                           IMilvaLocalizer localizer,
                           AppOptions appOptions,
                           TimeProvider timeProvider,
                           IMilvaLogger logger) : IAccountMailer
{
    private const string _verificationTokenPrefix = "evt_";
    private const string _resetTokenPrefix = "pwr_";

    private readonly IStoreasyRepositoryBase<AccountToken> _tokenRepository = tokenRepository;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly AppOptions _appOptions = appOptions;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<bool> SendEmailVerificationAsync(int userId, string email, string name, CancellationToken cancellationToken)
    {
        var token = await IssueTokenAsync(userId, AccountTokenPurpose.EmailVerification, _verificationTokenPrefix, cancellationToken);
        var link = _appOptions.BuildTokenLink(AppOptions.VerifyEmailPath, token);
        var (subject, html) = EmailTemplates.EmailVerification(_localizer, name, link);

        return await TrySendAsync(new EmailMessage(email, name, CultureInfo.CurrentUICulture.Name, subject, html, link), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendPasswordResetAsync(int userId, string email, string name, CancellationToken cancellationToken)
    {
        var token = await IssueTokenAsync(userId, AccountTokenPurpose.PasswordReset, _resetTokenPrefix, cancellationToken);
        var link = _appOptions.BuildTokenLink(AppOptions.ResetPasswordPath, token);
        var (subject, html) = EmailTemplates.PasswordReset(_localizer, name, link);

        return await TrySendAsync(new EmailMessage(email, name, CultureInfo.CurrentUICulture.Name, subject, html, link), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> SendInvitationAsync(string email, string organizationName, string inviterName, OrgRole role, string token, CancellationToken cancellationToken)
    {
        var link = _appOptions.BuildTokenLink(AppOptions.InvitationPath, token);
        string roleName = _localizer[$"{nameof(OrgRole)}.{role}"];
        var (subject, html) = EmailTemplates.Invitation(_localizer, organizationName, inviterName, roleName, link);

        return TrySendAsync(new EmailMessage(email, null, CultureInfo.CurrentUICulture.Name, subject, html, link), cancellationToken);
    }

    private async Task<string> IssueTokenAsync(int userId, AccountTokenPurpose purpose, string prefix, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _tokenRepository.RetireAccountTokensAsync(userId, purpose, now, cancellationToken);

        var token = SecureTokens.Create(prefix);

        await _tokenRepository.AddAsync(new AccountToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = SecureTokens.Hash(token),
            ExpiryDate = now.Add(AccountToken.LifetimeOf(purpose)),
        }, cancellationToken);

        return token;
    }

    private async Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(message, cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("E-mail '{Subject}' could not be sent: {Error}", message.Subject, SecretRedactor.Redact(ex.Message));

            return false;
        }
    }
}
