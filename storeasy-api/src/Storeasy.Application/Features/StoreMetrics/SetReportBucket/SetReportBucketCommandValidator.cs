using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreMetrics.SetReportBucket;

/// <summary>
/// Validations of <see cref="SetReportBucketCommand"/>.
/// </summary>
public sealed class SetReportBucketCommandValidator : AbstractValidator<SetReportBucketCommand>
{
    ///<inheritdoc cref="SetReportBucketCommandValidator"/>
    public SetReportBucketCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.Bucket)
            .Must(bucket => (bucket?.Length ?? 0) <= StoreConnection.ReportBucketMaxLength + 20 && ReportBuckets.Normalize(bucket) != null)
            .WithMessage(localizer[MessageKey.ReportBucketInvalid]);
    }
}
