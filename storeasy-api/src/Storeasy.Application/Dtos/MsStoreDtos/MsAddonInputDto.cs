namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// An add-on to create.
/// </summary>
public class MsAddonInputDto
{
    /// <summary>
    /// Developer's product id, unique in the app.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Durable or Consumable.
    /// </summary>
    public MsAddonType ProductType { get; set; }
}
