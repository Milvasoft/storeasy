using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.GetMockupFrames;

/// <summary>
/// Handles <see cref="GetMockupFramesQuery"/>. Classes come from the rules catalog, never from code.
/// </summary>
/// <param name="screenshotSizeSource"></param>
/// <param name="timeProvider"></param>
public class GetMockupFramesQueryHandler(IScreenshotSizeSource screenshotSizeSource, TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetMockupFramesQuery, List<MockupFrameDto>>
{
    private readonly IScreenshotSizeSource _screenshotSizeSource = screenshotSizeSource;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task<Response<List<MockupFrameDto>>> Handle(GetMockupFramesQuery request, CancellationToken cancellationToken)
    {
        var catalog = _screenshotSizeSource.Current;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var provided = MockupExportRules.DefaultProvided([.. catalog.Classes]);

        var frames = Enum.GetValues<MockupFrameKind>().Select(kind =>
        {
            var (radius, bezel) = MockupFrameDefaults.Of(kind);

            return new MockupFrameDto
            {
                Kind = kind,
                CornerRadius = radius,
                Bezel = bezel,
                Cutouts = kind == MockupFrameKind.Phone ? [.. MockupSpecRules.Cutouts.All] : [],
                Classes = [.. catalog.Classes.Where(c => c.Frame == kind).Select(c => new MockupClassDto
                {
                    ClassKey = c.Key,
                    Store = c.Store,
                    Label = c.LabelEn,
                    LabelTr = c.LabelTr,
                    Width = c.ExportSize.Width,
                    Height = c.ExportSize.Height,
                    Family = c.Family,
                    Status = MockupExportRules.PlanRequirement(c, today, provided, provided),
                    RequiredFrom = c.RequiredFrom,
                    MaxCount = c.MaxCount,
                })],
            };
        }).ToList();

        return Task.FromResult(Response<List<MockupFrameDto>>.Success(frames));
    }
}
