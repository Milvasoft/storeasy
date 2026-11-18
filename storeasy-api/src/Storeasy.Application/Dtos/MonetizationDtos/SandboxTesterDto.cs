namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// An App Store sandbox tester of the developer account (read live; testers are created in App Store Connect).
/// </summary>
public class SandboxTesterDto
{
    /// <summary>
    /// The store's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// First name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Apple Account name of the tester.
    /// </summary>
    public string AccountName { get; set; }

    /// <summary>
    /// Storefront territory.
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Whether purchases are interrupted (to test the interrupted purchase flow).
    /// </summary>
    public bool? InterruptPurchases { get; set; }

    /// <summary>
    /// How fast subscriptions renew.
    /// </summary>
    public string SubscriptionRenewalRate { get; set; }

    /// <summary>
    /// Whether the tester can pay with Apple Pay.
    /// </summary>
    public bool? ApplePayCompatible { get; set; }
}
