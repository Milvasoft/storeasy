using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a store visitor came from, normalized over the three stores. <see cref="All"/> marks a row that is not split by
/// source (the total of every source). Values are explicit and never reused.
/// </summary>
public enum TrafficSource : byte
{
    /// <summary>Every source together (a row that is not split by source).</summary>
    [Description("All sources")]
    All = 0,

    /// <summary>Store search (App Store search, Google Play search).</summary>
    [Description("Search")]
    Search = 1,

    /// <summary>Browsing the store (App Store browse, Google Play explore).</summary>
    [Description("Browse")]
    Browse = 2,

    /// <summary>Another app sent the visitor (App Store app referrer).</summary>
    [Description("App referrer")]
    AppReferrer = 3,

    /// <summary>A web page sent the visitor (App Store web referrer).</summary>
    [Description("Web referrer")]
    WebReferrer = 4,

    /// <summary>A third party sent the visitor, app or web (Google Play third-party referrals).</summary>
    [Description("Third-party referral")]
    Referral = 5,

    /// <summary>An App Clip.</summary>
    [Description("App Clip")]
    AppClip = 6,

    /// <summary>A store notification.</summary>
    [Description("Notification")]
    Notification = 7,

    /// <summary>The Microsoft Store app on the device.</summary>
    [Description("Store app")]
    StoreClient = 8,

    /// <summary>The store's web site (Microsoft Store web).</summary>
    [Description("Store web")]
    StoreWeb = 9,

    /// <summary>A source the store names but storeasy does not group (institutional purchases, small sources).</summary>
    [Description("Other")]
    Other = 10,

    /// <summary>The store does not know the source.</summary>
    [Description("Unavailable")]
    Unavailable = 11,
}
