namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Masks e-mail addresses shown to someone who is not signed in.
/// </summary>
public static class EmailMasking
{
    /// <summary>
    /// Keeps the first character of the local part and the whole domain: <c>can@example.com</c> becomes
    /// <c>c**@example.com</c>. A value without a domain is masked completely.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public static string Mask(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var at = email.LastIndexOf('@');

        if (at <= 0)
            return "***";

        var local = email[..at];

        return $"{local[0]}{new string('*', Math.Clamp(local.Length - 1, 2, 8))}{email[at..]}";
    }
}
