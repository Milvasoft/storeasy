using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A Microsoft Store product (MSIX app or MSI/EXE product) with its active submission, the Partner Center rules that
/// apply to it and its assisted steps.
/// </summary>
public class MsProductDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name in the store.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Store id of the product.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Package family name (MSIX).
    /// </summary>
    public string PackageFamilyName { get; set; }

    /// <summary>
    /// Which API manages the product.
    /// </summary>
    public MsProductKind Kind { get; set; }

    /// <summary>
    /// Whether the price is on Pricing Version 2 (Partner Center only).
    /// </summary>
    public bool PricingV2 { get; set; }

    /// <summary>
    /// Whether the first submission was made (the API works only after it).
    /// </summary>
    public bool FirstSubmissionDone { get; set; }

    /// <summary>
    /// Whether the product uses mandatory updates (the API refuses it).
    /// </summary>
    public bool HasMandatoryUpdate { get; set; }

    /// <summary>
    /// MSI/EXE: whether the draft is ready to submit.
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Last published submission.
    /// </summary>
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending (or ongoing) submission.
    /// </summary>
    public string PendingSubmissionId { get; set; }

    /// <summary>
    /// UTC instant of the last sync; null until the store confirmed the product.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// The pending submission, if storeasy knows it.
    /// </summary>
    public MsSubmissionDto ActiveSubmission { get; set; }

    /// <summary>
    /// Partner Center rules shown on every submission screen (localized).
    /// </summary>
    public List<string> Notices { get; set; } = [];

    /// <summary>
    /// Assisted steps of the product.
    /// </summary>
    public List<MsAssistedStepDto> AssistedSteps { get; set; } = [];

    /// <summary>
    /// Projection from a product row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsProduct, MsProductDto>> Projection { get; } = p => new MsProductDto
    {
        StoreAppId = p.StoreAppId,
        AppId = p.StoreApp.AppId,
        Name = p.StoreApp.Name,
        ProductId = p.StoreApp.ExternalId,
        PackageFamilyName = p.StoreApp.BundleId,
        Kind = p.Kind,
        PricingV2 = p.PricingV2,
        FirstSubmissionDone = p.FirstSubmissionDone,
        HasMandatoryUpdate = p.HasMandatoryUpdate,
        IsReady = p.IsReady,
        LastPublishedSubmissionId = p.LastPublishedSubmissionId,
        PendingSubmissionId = p.PendingSubmissionId,
        SyncedDate = p.SyncedDate,
    };
}
