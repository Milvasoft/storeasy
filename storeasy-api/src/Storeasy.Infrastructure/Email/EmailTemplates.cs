using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Constants;
using System.Net;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// The account e-mails as simple, inline styled HTML. Every text comes from the resx files in the culture of the current
/// request (<see cref="IMilvaLocalizer"/>); every value put into the HTML is encoded.
/// </summary>
public static class EmailTemplates
{
    /// <summary>
    /// The e-mail verification message.
    /// </summary>
    /// <param name="localizer"></param>
    /// <param name="name"></param>
    /// <param name="link"></param>
    /// <returns></returns>
    public static (string Subject, string Html) EmailVerification(IMilvaLocalizer localizer, string name, string link)
        => Render(localizer, MessageKey.EmailVerifySubject, MessageKey.EmailVerifyTitle, localizer[MessageKey.EmailVerifyBody, name ?? string.Empty], MessageKey.EmailVerifyAction, link);

    /// <summary>
    /// The password reset message.
    /// </summary>
    /// <param name="localizer"></param>
    /// <param name="name"></param>
    /// <param name="link"></param>
    /// <returns></returns>
    public static (string Subject, string Html) PasswordReset(IMilvaLocalizer localizer, string name, string link)
        => Render(localizer, MessageKey.EmailResetSubject, MessageKey.EmailResetTitle, localizer[MessageKey.EmailResetBody, name ?? string.Empty], MessageKey.EmailResetAction, link);

    /// <summary>
    /// The organization invitation message.
    /// </summary>
    /// <param name="localizer"></param>
    /// <param name="organizationName"></param>
    /// <param name="inviterName"></param>
    /// <param name="roleName"></param>
    /// <param name="link"></param>
    /// <returns></returns>
    public static (string Subject, string Html) Invitation(IMilvaLocalizer localizer, string organizationName, string inviterName, string roleName, string link)
    {
        string subject = localizer[MessageKey.EmailInvitationSubject, organizationName ?? string.Empty];
        string body = localizer[MessageKey.EmailInvitationBody, inviterName ?? string.Empty, organizationName ?? string.Empty, roleName ?? string.Empty];

        return (subject, Layout(localizer[MessageKey.EmailInvitationTitle], body, localizer[MessageKey.EmailInvitationAction], link, localizer));
    }

    private static (string Subject, string Html) Render(IMilvaLocalizer localizer, string subjectKey, string titleKey, string body, string actionKey, string link)
        => (localizer[subjectKey], Layout(localizer[titleKey], body, localizer[actionKey], link, localizer));

    private static string Layout(string title, string body, string action, string link, IMilvaLocalizer localizer)
    {
        var encodedLink = WebUtility.HtmlEncode(link);
        string linkHint = localizer[MessageKey.EmailLinkHint];
        string ignoreHint = localizer[MessageKey.EmailIgnoreHint];

        return $"""
                <!DOCTYPE html>
                <html>
                <body style="margin:0;padding:24px;background:#f5f5f7;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1d1d1f;">
                <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px;">
                <p style="margin:0 0 24px;font-size:20px;font-weight:700;">store<span style="color:#4636FF;">e</span>asy</p>
                <h1 style="margin:0 0 16px;font-size:20px;">{WebUtility.HtmlEncode(title)}</h1>
                <p style="margin:0 0 24px;font-size:15px;line-height:1.5;">{WebUtility.HtmlEncode(body)}</p>
                <p style="margin:0 0 24px;"><a href="{encodedLink}" style="display:inline-block;background:#4636FF;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600;">{WebUtility.HtmlEncode(action)}</a></p>
                <p style="margin:0 0 8px;font-size:13px;color:#6e6e73;">{WebUtility.HtmlEncode(linkHint)}</p>
                <p style="margin:0 0 24px;font-size:13px;word-break:break-all;"><a href="{encodedLink}" style="color:#4636FF;">{encodedLink}</a></p>
                <p style="margin:0;font-size:13px;color:#6e6e73;">{WebUtility.HtmlEncode(ignoreHint)}</p>
                </div>
                </body>
                </html>
                """;
    }
}
