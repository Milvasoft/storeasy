using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageGroupSuggestions;

/// <summary>
/// Handles <see cref="GetPackageGroupSuggestionsQuery"/>.
/// </summary>
/// <param name="packageRepository"></param>
public class GetPackageGroupSuggestionsQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository) : IInterceptable, IQueryHandler<GetPackageGroupSuggestionsQuery, List<PackageGroupSuggestionDto>>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;

    /// <inheritdoc/>
    public async Task<Response<List<PackageGroupSuggestionDto>>> Handle(GetPackageGroupSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var packages = await _packageRepository.GetAllAsync(p => p.OrganizationId == request.OrganizationId && p.PackageGroupId == null,
                                                            projection: p => new TrackedPackage { Id = p.Id, Name = p.Name, SourceRepository = p.SourceRepository },
                                                            cancellationToken: cancellationToken);
        var names = packages.ToDictionary(p => p.Id, p => p.Name);

        return Response<List<PackageGroupSuggestionDto>>.Success([.. PackageGroups.Suggest(packages).Select(s => new PackageGroupSuggestionDto
        {
            Name = s.Name,
            Repository = s.Repository,
            TrackedPackageIds = s.TrackedPackageIds,
            PackageNames = [.. s.TrackedPackageIds.Select(id => names[id])],
        })]);
    }
}
