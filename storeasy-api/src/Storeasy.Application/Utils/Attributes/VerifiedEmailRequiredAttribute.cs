namespace Storeasy.Application.Utils.Attributes;

/// <summary>
/// Declares on a request type that the caller's e-mail address must be verified (instance admins, the
/// <see cref="UserType.Manager"/> users, are exempt). Unverified users can sign in, browse and accept invitations, but
/// cannot create what costs or exposes something: organizations and store connections. Checked by
/// <see cref="Behaviours.VerifiedEmailBehavior{TRequest, TResponse}"/>; a failure is <see cref="MessageKey.EmailNotVerified"/> (403).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class VerifiedEmailRequiredAttribute : Attribute
{
}
