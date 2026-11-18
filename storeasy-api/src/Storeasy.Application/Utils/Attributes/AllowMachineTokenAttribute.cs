namespace Storeasy.Application.Utils.Attributes;

/// <summary>
/// Lets CI tokens and upload tokens (no person behind them) call an endpoint or send a request. Everything else refuses
/// them: the token middleware for endpoints, the organization access behaviour for requests. The handler still limits a
/// machine token to its organization and apps (<see cref="Interfaces.ICurrentToken.AllowsApp"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowMachineTokenAttribute : Attribute
{
}
