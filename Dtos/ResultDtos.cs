using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. "01:45:32".</summary>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int FinishPosition { get; set; }

    [Range(1, int.MaxValue)]
    public int TotalFinishers { get; set; }
}

public record ResultResponse(
    int ResultId,
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    string CategoryName,
    int ParticipantId,
    string ParticipantName,
    TimeSpan? FinishTime,
    int? FinishPosition,
    int? TotalFinishers,
    DateTime? PublishedAt);
