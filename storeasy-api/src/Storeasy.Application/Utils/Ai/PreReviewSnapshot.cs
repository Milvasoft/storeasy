using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Utils.Ai;

/// <summary>
/// The compact, structured description of one store record's submission that an AI pre-review reads: tagged text (see
/// <see cref="PreReviewSnapshotBuilder"/>) plus the screenshots worth sending as images. Built from data; never contains a
/// password, a contact's personal details or a key.
/// </summary>
public sealed class PreReviewSnapshot
{
    /// <summary>
    /// The submission as tagged, escaped text (the <c>&lt;submission&gt;</c> element).
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Version being prepared, when known.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Screenshots to send as images, most important first.
    /// </summary>
    public List<PreReviewImageCandidate> ImageCandidates { get; set; } = [];

    /// <summary>
    /// Lower case hex SHA-256 of the text and the checksums of the image candidates: equal hashes mean the same submission.
    /// </summary>
    public string Hash { get; set; }
}

/// <summary>
/// A screenshot of the store that may be sent to the model.
/// </summary>
/// <param name="Label">How the prompt refers to it.</param>
/// <param name="Url">Address of the image (the store's public image host).</param>
/// <param name="Checksum">The store's checksum, when known.</param>
public sealed record PreReviewImageCandidate(string Label, string Url, string Checksum);

/// <summary>
/// Everything storeasy knows about one store record's submission, gathered by the pre-review job.
/// </summary>
public sealed class PreReviewInput
{
    /// <summary>
    /// The store record.
    /// </summary>
    public StoreAppContext App { get; set; }

    /// <summary>
    /// Live store content (App Store, Google Play); null for the Microsoft Store or when the read failed.
    /// </summary>
    public StoreContentSnapshot Content { get; set; }

    /// <summary>
    /// Live Microsoft Store content; null for the other stores.
    /// </summary>
    public MsContentDto MsContent { get; set; }

    /// <summary>
    /// Readiness check of the record; null when it could not run.
    /// </summary>
    public ReadinessReportDto Readiness { get; set; }

    /// <summary>
    /// Saved answers of the app by kind (the stored JSON documents; secrets are never part of them).
    /// </summary>
    public Dictionary<SavedAnswerKind, string> Answers { get; set; } = [];

    /// <summary>
    /// In-app products of the record.
    /// </summary>
    public List<PreReviewProduct> Products { get; set; } = [];

    /// <summary>
    /// Past rejections of the record, newest first.
    /// </summary>
    public List<PreReviewRejection> Rejections { get; set; } = [];
}

/// <summary>
/// An in-app product as the pre-review sees it.
/// </summary>
/// <param name="Kind">Kind.</param>
/// <param name="ProductId">Product id.</param>
/// <param name="ReferenceName">Reference name.</param>
/// <param name="State">State.</param>
/// <param name="Duration">Subscription period, if any.</param>
/// <param name="Localizations">Display names and descriptions.</param>
/// <param name="HasReviewNote">Whether a review note is written.</param>
/// <param name="HasReviewScreenshot">Whether a review screenshot is uploaded.</param>
/// <param name="PriceCount">Prices set.</param>
public sealed record PreReviewProduct(StoreProductKind Kind,
                                      string ProductId,
                                      string ReferenceName,
                                      ProductState State,
                                      string Duration,
                                      List<ProductLocalization> Localizations,
                                      bool HasReviewNote,
                                      bool HasReviewScreenshot,
                                      int PriceCount);

/// <summary>
/// A past rejection as the pre-review sees it.
/// </summary>
/// <param name="OpenedDate">When it was opened.</param>
/// <param name="VersionString">Version rejected.</param>
/// <param name="Kind">Kind.</param>
/// <param name="State">Open or closed.</param>
/// <param name="Guidelines">Guidelines cited.</param>
/// <param name="RawText">The reviewer's message as pasted, if any.</param>
public sealed record PreReviewRejection(DateTime OpenedDate, string VersionString, RejectionKind Kind, RejectionState State, List<RejectionGuideline> Guidelines, string RawText);
