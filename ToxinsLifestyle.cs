using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class ToxinsLifestyle
{
    [JsonConverter(typeof(LabResultConverter))]
    public sealed record LabResult(
        double? Value,
        string? Unit,
        string? Notes
    );

    public sealed record Inputs(
        string? AlcoholIntake,
        string? AlcoholDrinksPerWeek,
        string? Smoking,
        string? ChewingTobacco,
        string? Vaping,
        string? OtherNicotineUse,
        string? CannabisUse,
        string? ScreenTime,
        string? UltraProcessedFoodIntake,
        string? MedicationsOrSupplementsImpact,
        string? PhysicalEnvironmentImpact,
        string? MediaExposureImpact,
        string? StressfulEnvironmentsOrRelationshipsImpact,
        LabResult? BloodLeadLevel,
        LabResult? BloodMercury
    );

    private sealed class LabResultConverter : JsonConverter<LabResult?>
    {
        public override LabResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.TryGetDouble(out var value)
                    ? new LabResult(value, null, null)
                    : null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;

                if (double.TryParse(s, out var parsed))
                    return new LabResult(parsed, null, null);

                return new LabResult(null, null, s);
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var obj = doc.RootElement;

                double? value = TryGetDouble(obj, "value");
                string? unit = TryGetString(obj, "unit");
                string? notes = TryGetString(obj, "notes");

                return new LabResult(value, unit, notes);
            }

            throw new JsonException($"Unsupported LabResult token: {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, LabResult? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Unit is null && value.Notes is null)
            {
                if (value.Value.HasValue)
                    writer.WriteNumberValue(value.Value.Value);
                else
                    writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            if (value.Value.HasValue)
                writer.WriteNumber("value", value.Value.Value);
            if (!string.IsNullOrWhiteSpace(value.Unit))
                writer.WriteString("unit", value.Unit);
            if (!string.IsNullOrWhiteSpace(value.Notes))
                writer.WriteString("notes", value.Notes);
            writer.WriteEndObject();
        }

        private static double? TryGetDouble(JsonElement obj, string name)
        {
            if (!TryGetPropertyCaseInsensitive(obj, name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), out var parsed))
                return parsed;

            return null;
        }

        private static string? TryGetString(JsonElement obj, string name)
        {
            if (!TryGetPropertyCaseInsensitive(obj, name, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
