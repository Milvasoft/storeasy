namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// One listing field of one locale as the store has it now, with the catalog limit.
/// </summary>
public class ListingFieldDto
{
    /// <summary>
    /// Field name.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Content group (AppInfo, Version, App, Listing, Details).
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Locale; null for fields that are not localized.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Value in the store.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Most characters (catalog).
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Most UTF-8 bytes (catalog; App Store keywords).
    /// </summary>
    public int? MaxBytes { get; set; }

    /// <summary>
    /// Whether a submission needs it.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Fingerprint of the store value (changes when someone edits the store).
    /// </summary>
    public string StoreValueHash { get; set; }
}
