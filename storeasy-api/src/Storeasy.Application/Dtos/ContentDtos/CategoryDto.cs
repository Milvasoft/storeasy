namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Categories, copyright and content rights of an App Store record.
/// </summary>
public class CategoryDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Primary category id.
    /// </summary>
    public string Primary { get; set; }

    /// <summary>
    /// Secondary category id.
    /// </summary>
    public string Secondary { get; set; }

    /// <summary>
    /// Categories the store offers.
    /// </summary>
    public List<StoreOptionDto> Options { get; set; }

    /// <summary>
    /// Copyright of the version.
    /// </summary>
    public string Copyright { get; set; }

    /// <summary>
    /// DOES_NOT_USE_THIRD_PARTY_CONTENT or USES_THIRD_PARTY_CONTENT.
    /// </summary>
    public string ContentRightsDeclaration { get; set; }
}
