using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>A run / walk / cycle event. Mapped to the "Events" table.</summary>
public class RaceEvent
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required, MaxLength(10)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? BannerImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? Organiser { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";

    public static readonly string[] All = { Run, Walk, Cycle };

    /// <summary>Returns the canonical casing ("run" -> "Run"), or null when invalid.</summary>
    public static string? Normalise(string? value) =>
        All.FirstOrDefault(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
