using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Fills the derived fields of a <see cref="TrustedPublisherDto"/>.
/// </summary>
public static class TrustedPublisherDtoExtensions
{
    /// <summary>
    /// Sets <see cref="TrustedPublisherDto.Audience"/> and <see cref="TrustedPublisherDto.ExpectedClaims"/> from the rule's
    /// fields and returns the same object.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    public static TrustedPublisherDto WithExpectedClaims(TrustedPublisherDto dto)
    {
        var rule = new TrustedPublisher
        {
            Provider = dto.Provider,
            Repository = dto.Repository,
            Workflow = dto.Workflow,
            Branch = dto.Branch,
            Environment = dto.Environment,
            AzureOrganization = dto.Organization,
            AzureProject = dto.Project,
            ServiceConnection = dto.ServiceConnection,
            Issuer = dto.Issuer,
        };

        dto.Audience = TrustedPublisherMatcher.AudienceOf(dto.Provider);
        dto.ExpectedClaims = TrustedPublisherMatcher.ExpectedClaims(rule);

        return dto;
    }
}
