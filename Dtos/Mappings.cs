using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Entity -> DTO projections. Queryable versions are translated to SQL by EF Core.</summary>
public static class Mappings
{
    public static UserResponse ToResponse(this User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.ProfilePictureUrl,
            u.Role?.RoleName ?? string.Empty, u.CreatedAt);

    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm);

    /// <summary>Enrolment count excludes cancelled enrolments.</summary>
    public static IQueryable<EventResponse> ProjectToResponse(this IQueryable<RaceEvent> query) =>
        query.Select(e => new EventResponse(
            e.EventId,
            e.OrganiserId,
            e.Organiser!.FirstName + " " + e.Organiser.LastName,
            e.Name,
            e.Description,
            e.EventDate,
            e.Location,
            e.DistanceKm,
            e.EventType,
            e.BannerImageUrl,
            e.Enrolments.Count(x => x.Status != "Cancelled")));

    public static IQueryable<EnrolmentResponse> ProjectToResponse(this IQueryable<Enrolment> query) =>
        query.Select(e => new EnrolmentResponse(
            e.EnrolmentId,
            e.EventId,
            e.Event!.Name,
            e.Event.EventDate,
            e.CategoryId,
            e.Category!.Name,
            e.ParticipantId,
            e.Participant!.FirstName + " " + e.Participant.LastName,
            e.Participant.Email,
            e.EnrolmentDate,
            e.Status));

    public static IQueryable<ResultResponse> ProjectToResponse(this IQueryable<RaceResult> query) =>
        query.Select(r => new ResultResponse(
            r.ResultId,
            r.EnrolmentId,
            r.Enrolment!.EventId,
            r.Enrolment.Event!.Name,
            r.Enrolment.Event.EventDate,
            r.Enrolment.Category!.Name,
            r.Enrolment.ParticipantId,
            r.Enrolment.Participant!.FirstName + " " + r.Enrolment.Participant.LastName,
            r.FinishTime,
            r.FinishPosition,
            r.TotalFinishers,
            r.PublishedAt));
}
