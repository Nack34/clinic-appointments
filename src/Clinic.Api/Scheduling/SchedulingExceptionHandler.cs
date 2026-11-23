using Microsoft.AspNetCore.Diagnostics;

namespace Clinic.Api.Scheduling;

/// <summary>Turns broken scheduling rules into ProblemDetails responses with their own status code.</summary>
public class SchedulingExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not SchedulingException rule) return false;
        context.Response.StatusCode = rule.Status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = rule.Status, Detail = rule.Message },
        });
    }
}
