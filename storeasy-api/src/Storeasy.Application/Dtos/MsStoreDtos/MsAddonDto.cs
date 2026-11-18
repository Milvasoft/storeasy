using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// An add-on of an MSIX app. <see cref="ApiWritable"/> false means Partner Center only (assisted step).
/// </summary>
public class MsAddonDto
{
    /// <summary>
    /// Store record of the app.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store id of the add-on.
    /// </summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Developer's product id.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Product type.
    /// </summary>
    public MsAddonType Type { get; set; }

    /// <summary>
    /// Lifetime.
    /// </summary>
    public string Lifetime { get; set; }

    /// <summary>
    /// Price tier; null when Partner Center manages the price.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Last published submission.
    /// </summary>
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending submission.
    /// </summary>
    public string PendingSubmissionId { get; set; }

    /// <summary>
    /// Whether the API may write the add-on.
    /// </summary>
    public bool ApiWritable { get; set; }

    /// <summary>
    /// Why Partner Center manages it.
    /// </summary>
    public MsAssistedReason? AssistedReason { get; set; }

    /// <summary>
    /// UTC instant of the last read.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Projection from an add-on row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsAddon, MsAddonDto>> Projection { get; } = a => new MsAddonDto
    {
        StoreAppId = a.StoreAppId,
        StoreId = a.StoreId,
        ProductId = a.ProductId,
        Title = a.Title,
        Type = a.Type,
        Lifetime = a.Lifetime,
        PriceId = a.PriceId,
        LastPublishedSubmissionId = a.LastPublishedSubmissionId,
        PendingSubmissionId = a.PendingSubmissionId,
        ApiWritable = a.ApiWritable,
        AssistedReason = a.AssistedReason,
        SyncedDate = a.SyncedDate,
    };
}
