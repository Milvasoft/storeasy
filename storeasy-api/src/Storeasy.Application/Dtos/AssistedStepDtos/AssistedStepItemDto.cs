namespace Storeasy.Application.Dtos.AssistedStepDtos;

/// <summary>
/// One item of an assisted step's checklist.
/// </summary>
public class AssistedStepItemDto
{
    /// <summary>
    /// Position, from 1.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// What to do, in the language of the request.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Whether it is marked done.
    /// </summary>
    public bool Done { get; set; }
}
