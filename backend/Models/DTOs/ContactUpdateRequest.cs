using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Payload for updating a contact. All fields are optional;
/// only fields explicitly provided in the JSON body are changed
/// (partial update, matching the Java reference behavior, which the
/// Java setters implement with the `xxxSet` flags).
/// </summary>
[JsonConverter(typeof(ContactUpdateRequestConverter))]
public class ContactUpdateRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    [JsonIgnore]
    public bool NameSet { get; set; }

    [RegularExpression(@"^\+[1-9]\d{1,14}$", ErrorMessage = "Phone number must be in E.164 format, e.g. +1234567890")]
    public string? PhoneNumber { get; set; }

    [JsonIgnore]
    public bool PhoneNumberSet { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [JsonIgnore]
    public bool EmailSet { get; set; }

    [StringLength(1000)]
    public string? Address { get; set; }

    [JsonIgnore]
    public bool AddressSet { get; set; }
}

/// <summary>
/// Deserializes a contact update and records which properties were
/// actually present in the JSON body, so the service layer can apply
/// a true partial update. Property names are matched case-insensitively
/// (the API serializes with a snake_case naming policy).
/// </summary>
public sealed class ContactUpdateRequestConverter : JsonConverter<ContactUpdateRequest>
{
    public override ContactUpdateRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected a JSON object.");

        var request = new ContactUpdateRequest();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a JSON property name.");

            var propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            switch (propertyName.ToLowerInvariant())
            {
                case "name":
                    request.Name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    request.NameSet = true;
                    break;
                case "phone_number":
                case "phonenumber":
                    request.PhoneNumber = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    request.PhoneNumberSet = true;
                    break;
                case "email":
                    request.Email = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    request.EmailSet = true;
                    break;
                case "address":
                    request.Address = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    request.AddressSet = true;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return request;
    }

    public override void Write(Utf8JsonWriter writer, ContactUpdateRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.NameSet) writer.WriteString("name", value.Name);
        if (value.PhoneNumberSet) writer.WriteString("phone_number", value.PhoneNumber);
        if (value.EmailSet) writer.WriteString("email", value.Email);
        if (value.AddressSet) writer.WriteString("address", value.Address);
        writer.WriteEndObject();
    }
}
