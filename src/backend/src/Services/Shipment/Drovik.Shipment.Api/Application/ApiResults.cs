using System.ComponentModel.DataAnnotations;

namespace Drovik.Shipment.Api.Application;

public static class ApiResults
{
    public static IResult? Validate<T>(T request) where T : class
    {
        var errors = new List<ValidationResult>();
        return Validator.TryValidateObject(request, new ValidationContext(request), errors, true)
            ? null
            : Results.ValidationProblem(errors.GroupBy(item => item.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(item => item.Key, item => item.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray()),
                title: "Datos inválidos", detail: "Revisa los campos enviados.");
    }

    public static IResult BadRequest(string title, string detail) => Results.Problem(statusCode: 400, title: title, detail: detail);

    public static IResult Conflict(string detail) => Results.Problem(statusCode: 409, title: "Conflicto", detail: detail);
}
