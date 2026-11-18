using FluentValidation;

namespace Storeasy.Application.Features.Mockups.GetMockupFrames;

/// <summary>
/// Validations of <see cref="GetMockupFramesQuery"/>.
/// </summary>
public sealed class GetMockupFramesQueryValidator : AbstractValidator<GetMockupFramesQuery>
{
    ///<inheritdoc cref="GetMockupFramesQueryValidator"/>
    public GetMockupFramesQueryValidator()
    {
    }
}
