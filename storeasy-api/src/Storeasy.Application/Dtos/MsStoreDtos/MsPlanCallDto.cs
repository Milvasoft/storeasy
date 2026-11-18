namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A store call the apply job makes.
/// </summary>
public class MsPlanCallDto
{
    /// <summary>
    /// HTTP method.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Path relative to the API base.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// What the call writes.
    /// </summary>
    public string Target { get; set; }
}
