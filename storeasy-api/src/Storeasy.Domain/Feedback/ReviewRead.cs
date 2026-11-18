using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Feedback;

/// <summary>
/// A member read a review (the read mark is personal).
/// </summary>
[Table(TableNames.ReviewReads)]
[Index(nameof(UserId), nameof(StoreReviewId), IsUnique = true)]
public class ReviewRead : BaseEntity<long>
{
    /// <summary>
    /// Reader.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Review.
    /// </summary>
    public long StoreReviewId { get; set; }

    /// <summary>
    /// UTC instant it was read.
    /// </summary>
    public DateTime ReadDate { get; set; }

    /// <summary>
    /// Navigation property of the review.
    /// </summary>
    [ForeignKey(nameof(StoreReviewId))]
    public virtual StoreReview StoreReview { get; set; }
}
