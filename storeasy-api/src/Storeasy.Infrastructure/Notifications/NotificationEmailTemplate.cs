using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Constants;
using System.Net;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// The notification e-mail: the notification text, a button to the record in the web app and the preferences hint. The
/// text never carries secrets, review or rejection contents; links need a session and carry no token.
/// </summary>
public static class NotificationEmailTemplate
{
    private const int _subjectMaxLength = 120;

    /// <summary>
    /// Subject and HTML body of one notification.
    /// </summary>
    /// <param name="localizer"></param>
    /// <param name="text"></param>
    /// <param name="link"></param>
    /// <returns></returns>
    public static (string Subject, string Html) Render(IMilvaLocalizer localizer, string text, string link)
    {
        var subject = text.Length > _subjectMaxLength ? text[..(_subjectMaxLength - 3)] + "..." : text;
        string action = localizer[MessageKey.EmailNotificationAction];
        string hint = localizer[MessageKey.EmailNotificationHint];
        var encodedLink = WebUtility.HtmlEncode(link ?? string.Empty);

        var html = $"""
                    <!DOCTYPE html>
                    <html>
                    <body style="margin:0;padding:24px;background:#f5f5f7;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1d1d1f;">
                    <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px;">
                    <p style="margin:0 0 24px;font-size:20px;font-weight:700;">store<span style="color:#4636FF;">e</span>asy</p>
                    <p style="margin:0 0 24px;font-size:15px;line-height:1.5;">{WebUtility.HtmlEncode(text)}</p>
                    <p style="margin:0 0 24px;"><a href="{encodedLink}" style="display:inline-block;background:#4636FF;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600;">{WebUtility.HtmlEncode(action)}</a></p>
                    <p style="margin:0;font-size:13px;color:#6e6e73;">{WebUtility.HtmlEncode(hint)}</p>
                    </div>
                    </body>
                    </html>
                    """;

        return (subject, html);
    }
}
