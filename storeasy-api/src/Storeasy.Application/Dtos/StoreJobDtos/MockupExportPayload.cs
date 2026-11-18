namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.MockupExport"/> job: the template, its version at the time of the request, and
/// what to render.
/// </summary>
/// <param name="TemplateId"></param>
/// <param name="TemplateVersion"></param>
/// <param name="Locales"></param>
/// <param name="Targets"></param>
public sealed record MockupExportPayload(int TemplateId, int TemplateVersion, List<string> Locales, List<MockupExportTarget> Targets);

/// <summary>
/// A screenshot class and the size to render it at.
/// </summary>
/// <param name="ClassKey"></param>
/// <param name="Width"></param>
/// <param name="Height"></param>
public sealed record MockupExportTarget(string ClassKey, int Width, int Height);
