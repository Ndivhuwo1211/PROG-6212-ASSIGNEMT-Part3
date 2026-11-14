using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Security;

/// <summary>Base controller exposing the current session user. Only valid after [SessionAuthorize].</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    protected string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role) ?? string.Empty;

    protected ObjectResult ProblemResponse(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };

    /// <summary>
    /// Loads an event and verifies the logged-in Organiser owns it.
    /// Returns 404 when it does not exist and 403 when it belongs to a different Organiser.
    /// </summary>
    protected async Task<(RaceEvent? Event, IActionResult? Error)> LoadOwnedEventAsync(RaceDayDbContext db, int eventId)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return (null, ProblemResponse(StatusCodes.Status404NotFound, "Not found", "Event not found."));
        if (ev.OrganiserId != CurrentUserId)
            return (null, ProblemResponse(StatusCodes.Status403Forbidden, "Forbidden", "You can only manage events that you created."));
        return (ev, null);
    }
}
