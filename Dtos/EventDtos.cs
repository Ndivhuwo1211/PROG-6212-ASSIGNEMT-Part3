using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Used for both creating and updating an event.</summary>
public class EventRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Total event distance in kilometres.</summary>
    [Range(0.01, 1000)]
    public decimal DistanceKm { get; set; }

    /// <summary>"Run", "Walk" or "Cycle".</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
}

public record CategoryResponse(
    int CategoryId,
    int EventId,
    string Name,
    int? MinAge,
    int? MaxAge,
    decimal? DistanceKm);

public record EventResponse(
    int EventId,
    int OrganiserId,
    string OrganiserName,
    string Name,
    string Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    string EventType,
    string? BannerImageUrl,
    int EnrolmentCount);

public record EventDetailResponse(
    int EventId,
    int OrganiserId,
    string OrganiserName,
    string Name,
    string Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    string EventType,
    string? BannerImageUrl,
    int EnrolmentCount,
    List<CategoryResponse> Categories);

public class CategoryRequest
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }
}
