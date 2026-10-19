using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskFlow.Domain.Common;

namespace TaskFlow.Api.Common;

/// <summary>The one place where a failed Result becomes an HTTP ProblemDetails response.</summary>
public static class ResultHttp
{
    public static IActionResult ToProblem(this ControllerBase controller, IReadOnlyList<Error> errors)
    {
        var status = errors[0].Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        if (errors.All(e => e.Field is null))
            return controller.Problem(statusCode: status, title: errors[0].Message);

        var fields = new ModelStateDictionary();
        foreach (var error in errors.Where(e => e.Field is not null))
            fields.AddModelError(error.Field!, error.Message);

        var details = controller.ProblemDetailsFactory.CreateValidationProblemDetails(controller.HttpContext, fields, status);
        return new ObjectResult(details) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
