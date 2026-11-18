using Milvasoft.Components.CQRS.Command;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.PlanMediaSet;

/// <summary>
/// Plans one media set of a locale (screenshots, previews, Play graphics): existing items in the given order, deletions, and uploaded assets appended. Every file is checked against the catalog (size, format, alpha, count) and the set (same MD5 is skipped); rejected files carry a reason. When nothing is accepted and nothing else changes the answer is 422 with the file results.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanMediaSetCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Screenshots or previews.
    /// </summary>
    public MediaKind Kind { get; set; } = MediaKind.Screenshot;

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Store class key (APP_IPHONE_67, IPAD_PRO_3GEN_129, phoneScreenshots, featureGraphic...).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Existing item ids in the desired order.
    /// </summary>
    public List<string> Order { get; set; } = [];

    /// <summary>
    /// Existing item ids to remove.
    /// </summary>
    public List<string> Deletes { get; set; } = [];

    /// <summary>
    /// Uploaded assets to add, in order.
    /// </summary>
    public List<long> AssetIds { get; set; } = [];

    /// <summary>
    /// Poster frame of a preview (00:00:05:00).
    /// </summary>
    public string PreviewFrameTimeCode { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
