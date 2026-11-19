namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// A model the organization can choose for an AI feature, with Anthropic's list prices.
/// </summary>
public class AiModelOptionDto
{
    /// <summary>
    /// Model id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Input price in US dollars per million tokens.
    /// </summary>
    public decimal InputUsdPerMTok { get; set; }

    /// <summary>
    /// Output price in US dollars per million tokens.
    /// </summary>
    public decimal OutputUsdPerMTok { get; set; }
}
