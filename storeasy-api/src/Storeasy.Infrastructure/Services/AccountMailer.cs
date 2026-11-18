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
/// put into links to the web app (<see cref="AppOptions.BaseUrl"/>). The e-mails go into the outbox
/// (<see cref="IEmailOutbox"/>); a failure to queue is logged without the link.
/// </summary>
/// <param name="tokenRepository"></param>
/// <param name="userRepository"></param>
/// <param name="emailOutbox"></param>
/// <param name="localizer"></param>
/// <param name="appOptions"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public class AccountMailer(IStoreasyRepositoryBase<AccountToken> tokenRepository,
                           IStoreasyRepositoryBase<User> userRepository,
                           IEmailOutbox emailOutbox,
                           IMilvaLocalizer localizer,
                           AppOptions appOptions,
                           TimeProvider timeProvider,
                           IMilvaLogger logger) : IAccountMailer
{
    private const string _verificationTokenPrefix = "evt_";
    private const string _resetTokenPrefix = "pwr_";

    private readonly IStoreasyRepositoryBase<AccountToken> _tokenRepository = tokenRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;
    private readonly IEmailOutbox _emailOutbox = emailOutbox;
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

        return await TryQueueAsync(new EmailMessage(email, name, CultureInfo.CurrentUICulture.Name, subject, html, link), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            await _emailOutbox.EnqueuePasswordResetAsync(email.Trim(), CultureInfo.CurrentUICulture.Name, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("A password reset request could not be queued: {Error}", SecretRedactor.Redact(ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<EmailMessage> CreatePasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalizedEmail))
            return null;

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail,
                                                                projection: u => new { u.Id, u.Email, u.Name },
                                                                cancellationToken: cancellationToken);

        if (user == null)
            return null;

        var token = await IssueTokenAsync(user.Id, AccountTokenPurpose.PasswordReset, _resetTokenPrefix, cancellationToken);
        var link = _appOptions.BuildTokenLink(AppOptions.ResetPasswordPath, token);
        var (subject, html) = EmailTemplates.PasswordReset(_localizer, user.Name, link);

        return new EmailMessage(user.Email, user.Name, CultureInfo.CurrentUICulture.Name, subject, html, link);
    }

    /// <inheritdoc/>
    public Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken cancellationToken)
    {
        var link = _appOptions.BuildTokenLink(AppOptions.InvitationPath, mail.Token);
        string roleName = mail.Role == OrgRole.Custom && !string.IsNullOrWhiteSpace(mail.CustomRoleName) ? mail.CustomRoleName : _localizer[$"{nameof(OrgRole)}.{mail.Role}"];
        var (subject, html) = EmailTemplates.Invitation(_localizer, mail.OrganizationName, mail.InviterName, roleName, link);

        return TryQueueAsync(new EmailMessage(mail.Email, null, CultureInfo.CurrentUICulture.Name, subject, html, link), cancellationToken);
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

    private async Task<bool> TryQueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _emailOutbox.EnqueueAsync([message], cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("E-mail '{Subject}' could not be queued: {Error}", message.Subject, SecretRedactor.Redact(ex.Message));

            return false;
        }
    }
}
