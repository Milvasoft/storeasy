using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

namespace Storeasy.Domain.Monetization;

/// <summary>
/// A product sold inside an app, store neutral, as last synced from the store: App Store subscriptions and In-App
/// Purchases, Play subscriptions (with base plans in <see cref="Plans"/>) and one-time products (purchase options in
/// <see cref="Plans"/>). Writes never start from this row: a plan reads the store live and fingerprints what it changes.
/// </summary>
[Table(TableNames.StoreProducts)]
[Index(nameof(StoreAppId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(Kind))]
public class StoreProduct : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="ProductId"/>.
    /// </summary>
    public const int ProductIdMaxLength = 150;

    /// <summary>
    /// Column length of <see cref="ReferenceName"/>.
    /// </summary>
    public const int ReferenceNameMaxLength = 64;

    /// <summary>
    /// Column length of <see cref="ReviewNote"/>.
    /// </summary>
    public const int ReviewNoteMaxLength = 4000;

    /// <summary>
    /// Column length of short store texts (state, duration, multiseat status).
    /// </summary>
    public const int TextMaxLength = 50;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// What the product is.
    /// </summary>
    public StoreProductKind Kind { get; set; }

    /// <summary>
    /// The store's id (App Store resource id, Play product id).
    /// </summary>
    [MaxLength(StoreApp.ExternalIdMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Product id the app uses (never changes after creation).
    /// </summary>
    [MaxLength(ProductIdMaxLength)]
    public string ProductId { get; set; }

    /// <summary>
    /// Reference name (App Store) or the default listing title (Play).
    /// </summary>
    [MaxLength(ReferenceNameMaxLength)]
    public string ReferenceName { get; set; }

    /// <summary>
    /// Subscription group (App Store subscriptions).
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// The store's id of the subscription group.
    /// </summary>
    [MaxLength(StoreApp.ExternalIdMaxLength)]
    public string GroupExternalId { get; set; }

    /// <summary>
    /// Level in the group (1 is the highest service level).
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Subscription period (ONE_MONTH...) of an App Store subscription.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Duration { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// The store's own state text.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string StoreState { get; set; }

    /// <summary>
    /// Whether Family Sharing is on (it cannot be turned off again).
    /// </summary>
    public bool? FamilySharable { get; set; }

    /// <summary>
    /// Note for App Review.
    /// </summary>
    [MaxLength(ReviewNoteMaxLength)]
    public string ReviewNote { get; set; }

    /// <summary>
    /// App Review screenshot.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ProductScreenshot ReviewScreenshot { get; set; }

    /// <summary>
    /// Localizations.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ProductLocalization> Localizations { get; set; } = [];

    /// <summary>
    /// Territories where the product is sold.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ProductAvailability Availability { get; set; }

    /// <summary>
    /// Base territory of the price (App Store).
    /// </summary>
    [MaxLength(10)]
    public string BaseTerritory { get; set; }

    /// <summary>
    /// Prices per territory (App Store).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ProductPrice> Prices { get; set; } = [];

    /// <summary>
    /// Offers.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ProductOffer> Offers { get; set; } = [];

    /// <summary>
    /// Play base plans or purchase options.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ProductPlan> Plans { get; set; } = [];

    /// <summary>
    /// Reference name of the retention message shown to subscribers about to cancel, if one is assigned.
    /// </summary>
    [MaxLength(ReferenceNameMaxLength)]
    public string RetentionMessage { get; set; }

    /// <summary>
    /// Multiseat purchase status for organizations (ENABLED, DISABLED).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string MultiSeatStatus { get; set; }

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the group.
    /// </summary>
    [ForeignKey(nameof(GroupId))]
    public virtual ProductGroup Group { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }

    /// <summary>
    /// Column sets read in more than one place.
    /// </summary>
    public static class Projections
    {
        /// <summary>
        /// Every column the read models use (no navigation).
        /// </summary>
        public static Expression<Func<StoreProduct, StoreProduct>> Detail { get; } = p => new StoreProduct
        {
            Id = p.Id,
            OrganizationId = p.OrganizationId,
            AppId = p.AppId,
            StoreAppId = p.StoreAppId,
            Store = p.Store,
            Kind = p.Kind,
            ExternalId = p.ExternalId,
            ProductId = p.ProductId,
            ReferenceName = p.ReferenceName,
            GroupId = p.GroupId,
            GroupExternalId = p.GroupExternalId,
            Level = p.Level,
            Duration = p.Duration,
            State = p.State,
            StoreState = p.StoreState,
            FamilySharable = p.FamilySharable,
            ReviewNote = p.ReviewNote,
            ReviewScreenshot = p.ReviewScreenshot,
            Localizations = p.Localizations,
            Availability = p.Availability,
            BaseTerritory = p.BaseTerritory,
            Prices = p.Prices,
            Offers = p.Offers,
            Plans = p.Plans,
            RetentionMessage = p.RetentionMessage,
            MultiSeatStatus = p.MultiSeatStatus,
            SyncedDate = p.SyncedDate,
        };
    }
}
