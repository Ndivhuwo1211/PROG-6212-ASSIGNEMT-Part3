using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

/// <summary>
/// Session-based authorisation. Returns 401 when there is no authenticated session and
/// 403 when the logged-in user's role is not one of the allowed roles.
/// Usage: [SessionAuthorize] (any logged-in user) or [SessionAuthorize(RoleNames.Organiser)].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public SessionAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "You must be logged in to access this resource."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This action is not available to the '{role}' role."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
