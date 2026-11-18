namespace Storeasy.Application.Dtos.TestFlightDtos;

/// <summary>
/// A TestFlight build with its 90 day expiry.
/// </summary>
public class BetaBuildDto
{
    /// <summary>Build id.</summary>
    public long BuildId { get; set; }

    /// <summary>Version.</summary>
    public string VersionString { get; set; }

    /// <summary>Build number.</summary>
    public string BuildNumber { get; set; }

    /// <summary>Processing state.</summary>
    public BuildProcessingState ProcessingState { get; set; }

    /// <summary>UTC instant uploaded.</summary>
    public DateTime? UploadedDate { get; set; }

    /// <summary>UTC instant the build stops being testable.</summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Whether it already expired.</summary>
    public bool Expired { get; set; }

    /// <summary>Whole days left (0 when expired); null when unknown.</summary>
    public int? DaysLeft { get; set; }

    /// <summary>Beta app review state.</summary>
    public BetaReviewState BetaReviewState { get; set; }

    /// <summary>Groups the build is in.</summary>
    public List<int> GroupIds { get; set; } = [];

    /// <summary>"What to test" per language.</summary>
    public List<LocalizedText> WhatToTest { get; set; } = [];
}
