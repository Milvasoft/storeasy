using Storeasy.Domain.Feedback;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// A saved reply.
/// </summary>
public class SavedReplyDto
{
    /// <summary>Reply id.</summary>
    public int Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Text per language.</summary>
    public List<LocalizedText> Variants { get; set; }

    /// <summary>Apps it is suggested for (empty: all).</summary>
    public List<int> AppIds { get; set; }

    /// <summary>Times used.</summary>
    public int UseCount { get; set; }

    /// <summary>UTC instant of the last change.</summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Who changed it last.</summary>
    public string UpdatedByUserName { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<SavedReply, SavedReplyDto>> Projection { get; } = s => new SavedReplyDto
    {
        Id = s.Id,
        Name = s.Name,
        Variants = s.Variants,
        AppIds = s.AppIds,
        UseCount = s.UseCount,
        UpdatedDate = s.LastModificationDate ?? s.CreationDate,
        UpdatedByUserName = s.LastModifierUserName ?? s.CreatorUserName,
    };
}
