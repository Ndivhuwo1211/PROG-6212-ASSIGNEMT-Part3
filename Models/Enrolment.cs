using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>Links a Participant to an Event under a chosen Category.</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }

    public DateTime EnrolmentDate { get; set; } = DateTime.Now;

    /// <summary>Pending, Confirmed or Cancelled.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = EnrolmentStatuses.Pending;

    public User? Participant { get; set; }
    public RaceEvent? Event { get; set; }
    public Category? Category { get; set; }
    public RaceResult? Result { get; set; }
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Cancelled };

    public static string? Normalise(string? value) =>
        All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
