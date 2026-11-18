using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetAgeRating;

/// <summary>
/// Handles <see cref="GetAgeRatingQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
public class GetAgeRatingQueryHandler(IStoreContentService contentService,
                                      IRulesCatalog rulesCatalog) : IInterceptable, IQueryHandler<GetAgeRatingQuery, AgeRatingDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;

    /// <inheritdoc/>
    public async Task<Response<AgeRatingDto>> Handle(GetAgeRatingQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.AgeRating, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<AgeRatingDto>();

        var prefix = Storeasy.Domain.Content.ContentGroups.AgeRating + "|";
        var answers = read.Snapshot.Values.Where(v => v.Key.StartsWith(prefix, StringComparison.Ordinal))
                                          .Select(v => new AgeRatingAnswerDto { Key = Storeasy.Domain.Content.ContentKey.Parse(v.Key).Field, Value = v.Value })
                                          .OrderBy(a => a.Key, StringComparer.Ordinal)
                                          .ToList();

        var assisted = _rulesCatalog.Current.ForStore(StoreKind.Apple).Where(r => r.Category == "age-rating" && r.Check?.Kind == Storeasy.Domain.Rules.RuleCheck.Assisted).ToList();

        foreach (var rule in assisted)
        {
            var descriptors = rule.Params?.Extra?.TryGetValue("descriptors", out var list) == true && list.ValueKind == System.Text.Json.JsonValueKind.Array
                ? list.EnumerateArray().Select(d => d.GetString())
                : [];

            answers.AddRange(descriptors.Select(d => new AgeRatingAnswerDto { Key = d, IsNew = true, Assisted = true, DueDate = rule.Deadline }));
        }

        return Response<AgeRatingDto>.Success(new AgeRatingDto { StoreAppId = request.StoreAppId, Answers = answers, AssistedRuleIds = [.. assisted.Select(r => r.Id)] });
    }
}
