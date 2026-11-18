using System.Text.Json;

namespace Storeasy.Application.Dtos.AssistedStepDtos;

/// <summary>
/// An assisted step with its checklist, store page and prepared answers.
/// </summary>
public class AssistedStepDetailDto : AssistedStepListDto
{
    /// <summary>
    /// Direct address of the store page.
    /// </summary>
    public string StoreUrl { get; set; }

    /// <summary>
    /// Checklist.
    /// </summary>
    public List<AssistedStepItemDto> Steps { get; set; }

    /// <summary>
    /// Known traps, in the language of the request.
    /// </summary>
    public List<string> Pitfalls { get; set; }

    /// <summary>
    /// Saved answer documents the step uses.
    /// </summary>
    public List<SavedAnswerKind> AnswerKeys { get; set; }

    /// <summary>
    /// The prepared answers by kind (no secrets).
    /// </summary>
    public Dictionary<string, JsonElement> Answers { get; set; }

    /// <summary>
    /// UTC instant of the last hand-off.
    /// </summary>
    public DateTime? HandedOffDate { get; set; }

    /// <summary>
    /// Who handed it off.
    /// </summary>
    public string HandedOffByUserName { get; set; }

    /// <summary>
    /// Note on the result.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Evidence screenshot.
    /// </summary>
    public long? EvidenceAssetId { get; set; }
}
