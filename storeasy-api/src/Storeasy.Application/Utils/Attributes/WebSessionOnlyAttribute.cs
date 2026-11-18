namespace Storeasy.Application.Utils.Attributes;

/// <summary>
/// An endpoint a personal access token must not call (creating or revoking tokens): only a web session may. The token
/// middleware answers 403 for any storeasy token.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class WebSessionOnlyAttribute : Attribute
{
}
