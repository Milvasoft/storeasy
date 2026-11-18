using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// Everything the monetization planner needs, already read: no store or database call happens while planning.
/// </summary>
/// <param name="App">Store record.</param>
/// <param name="Snapshot">The store's current products.</param>
/// <param name="Kind">Plan kind (monetization, server notifications, sandbox testers).</param>
/// <param name="Changes">Requested changes in order.</param>
/// <param name="Equalizations">The store's equalization of every requested base price point (by base price point id;
/// the base territory's own point included).</param>
/// <param name="PricePoints">Requested territory price points (by id), for the diff.</param>
/// <param name="Assets">Checked uploads of review screenshots.</param>
/// <param name="Testers">Sandbox testers, for sandbox tester changes.</param>
/// <param name="Today">Current UTC day.</param>
/// <param name="Localizer">Texts of warnings.</param>
public sealed record MonetizationPlanInput(StoreAppContext App,
                                          MonetizationSnapshot Snapshot,
                                          ChangePlanKind Kind,
                                          IReadOnlyList<MonetizationChangeDto> Changes,
                                          IReadOnlyDictionary<string, IReadOnlyList<PricePointDto>> Equalizations,
                                          IReadOnlyDictionary<string, PricePointDto> PricePoints,
                                          IReadOnlyList<InspectedAsset> Assets,
                                          IReadOnlyList<SandboxTesterDto> Testers,
                                          DateOnly Today,
                                          IMilvaLocalizer Localizer);
