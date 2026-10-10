using System.ComponentModel.DataAnnotations;
using Phonebook.Api.Models.DTOs;

namespace Phonebook.Api.Validation;

/// <summary>
/// Endpoint filter that validates DataAnnotations on request DTOs.
/// A failing rule produces HTTP 400 with a {"detail": "field: message"}
/// body instead of silently storing invalid data.
/// </summary>
public sealed class DtoValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            // Only request payloads are validated (LoginRequest,
            // ContactCreateRequest, ContactUpdateRequest, ...).
            if (argument is null || !argument.GetType().Name.EndsWith("Request", StringComparison.Ordinal))
                continue;

            var errors = Validate(argument);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new ErrorResponse(string.Join("; ", errors)));
            }
        }

        return await next(context);
    }

    /// <summary>
    /// Runs every DataAnnotation rule on the object and formats the
    /// failures as "snake_case_field: message".
    /// </summary>
    public static List<string> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(instance);
        Validator.TryValidateObject(instance, validationContext, results, validateAllProperties: true);

        return results
            .Select(result =>
            {
                var member = result.MemberNames.FirstOrDefault() ?? string.Empty;
                var field = ToSnakeCase(member);
                return string.IsNullOrEmpty(field)
                    ? result.ErrorMessage ?? "Invalid value"
                    : $"{field}: {result.ErrorMessage}";
            })
            .ToList();
    }

    /// <summary>
    /// Converts a PascalCase CLR property name to the snake_case name
    /// used by the API (PhoneNumber -> phone_number).
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    builder.Append('_');
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
