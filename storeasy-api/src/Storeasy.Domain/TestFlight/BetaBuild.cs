using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.TestFlight;

/// <summary>
/// TestFlight details of a build: beta review state and "what to test" texts. The expiry date stays on the build.
/// </summary>
[Table(TableNames.BetaBuilds)]
[Index(nameof(BuildId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class BetaBuild : BaseEntity<long>
{
    /// <summary>
    /// Longest "what to test" text App Store Connect accepts.
    /// </summary>
    public const int WhatToTestMaxLength = 4000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Build.
    /// </summary>
    public long BuildId { get; set; }

    /// <summary>
    /// Beta app review state.
    /// </summary>
    public BetaReviewState BetaReviewState { get; set; } = BetaReviewState.NotSubmitted;

    /// <summary>
    /// UTC instant the build was submitted to beta review through storeasy.
    /// </summary>
    public DateTime? BetaReviewSubmittedDate { get; set; }

    /// <summary>
    /// "What to test" per language.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<LocalizedText> WhatToTest { get; set; } = [];

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the build.
    /// </summary>
    [ForeignKey(nameof(BuildId))]
    public virtual Build Build { get; set; }
}
