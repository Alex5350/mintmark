using System.Text.Json;
using Mintmark.Application.Ports;

namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// Strict parser of the identify-v1 contract JSON (prompts/identify-v1.md)
/// into <see cref="VisionIdentification"/>. Every reported field must be a
/// {value, confidence, evidence} object; unknown shapes throw rather than
/// degrade — a malformed model response is an error, not a null.
/// </summary>
public static class VisionResponseParser
{
    /// <summary>Parses a model response, tolerating markdown fences around the JSON.</summary>
    /// <exception cref="JsonException">Thrown when the response does not follow the contract.</exception>
    public static VisionIdentification Parse(string rawResponse, string modelName, string modelVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawResponse);

        using var document = JsonDocument.Parse(StripFences(rawResponse));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("identify-v1 response must be a JSON object.");
        }

        return new VisionIdentification(
            modelName,
            modelVersion,
            rawResponse,
            country: StringField(root, "country"),
            mint: StringField(root, "mint"),
            series: StringField(root, "series"),
            year: IntField(root, "year"),
            denomination: StringField(root, "denomination"),
            metal: StringField(root, "metal"),
            fineness: DecimalField(root, "fineness"),
            sizeEstimateTroyOz: DecimalField(root, "sizeEstimateTroyOz"),
            finish: StringField(root, "finish"),
            finishAttributes: StringList(root, "finishAttributes"),
            edge: StringField(root, "edge"),
            conditionNotes: StringList(root, "conditionNotes"),
            AuthenticityList(root, "authenticityFlags"),
            StringList(root, "imageQualityIssues"));
    }

    /// <summary>Removes ```json fences some models emit despite the contract.</summary>
    public static string StripFences(string response)
    {
        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd > 0)
            {
                trimmed = trimmed[(firstLineEnd + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }

    private static FieldObservation<string?> StringField(JsonElement root, string name)
    {
        var field = RequireField(root, name);
        var value = field.TryGetProperty("value", out var valueElement)
            && valueElement.ValueKind is JsonValueKind.String or JsonValueKind.Null
            ? (valueElement.ValueKind == JsonValueKind.String ? valueElement.GetString() : null)
            : throw new JsonException($"Field '{name}.value' must be a string or null.");

        return new FieldObservation<string?>(
            value,
            Confidence(field, name),
            Evidence(field, name));
    }

    private static FieldObservation<int?> IntField(JsonElement root, string name)
    {
        var field = RequireField(root, name);
        int? value = null;
        if (field.TryGetProperty("value", out var valueElement) && valueElement.ValueKind != JsonValueKind.Null)
        {
            if (valueElement.ValueKind != JsonValueKind.Number
                || !valueElement.TryGetInt32(out var parsed))
            {
                throw new JsonException($"Field '{name}.value' must be an integer or null.");
            }

            value = parsed;
        }

        return new FieldObservation<int?>(value, Confidence(field, name), Evidence(field, name));
    }

    private static FieldObservation<decimal?> DecimalField(JsonElement root, string name)
    {
        var field = RequireField(root, name);
        decimal? value = null;
        if (field.TryGetProperty("value", out var valueElement) && valueElement.ValueKind != JsonValueKind.Null)
        {
            if (valueElement.ValueKind != JsonValueKind.Number
                || !valueElement.TryGetDecimal(out var parsed))
            {
                throw new JsonException($"Field '{name}.value' must be a number or null.");
            }

            value = parsed;
        }

        return new FieldObservation<decimal?>(value, Confidence(field, name), Evidence(field, name));
    }

    private static List<string> StringList(JsonElement root, string name)
    {
        var field = RequireField(root, name);
        if (field.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Field '{name}' must be an array.");
        }

        var values = new List<string>();
        foreach (var item in field.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Field '{name}' must contain strings only.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static List<AuthenticityFlag> AuthenticityList(JsonElement root, string name)
    {
        var field = RequireField(root, name);
        if (field.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Field '{name}' must be an array.");
        }

        var flags = new List<AuthenticityFlag>();
        foreach (var item in field.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Field '{name}' must contain objects.");
            }

            string? signal = null;
            if (item.TryGetProperty("signal", out var signalElement) && signalElement.ValueKind == JsonValueKind.String)
            {
                signal = signalElement.GetString();
            }

            string? severity = null;
            if (item.TryGetProperty("severity", out var severityElement) && severityElement.ValueKind == JsonValueKind.String)
            {
                severity = severityElement.GetString();
            }

            flags.Add(new AuthenticityFlag(signal, severity));
        }

        return flags;
    }

    private static JsonElement RequireField(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var field)
            || (field.ValueKind != JsonValueKind.Object && field.ValueKind != JsonValueKind.Array))
        {
            throw new JsonException($"identify-v1 response is missing the '{name}' field.");
        }

        return field;
    }

    private static decimal Confidence(JsonElement field, string name)
    {
        if (!field.TryGetProperty("confidence", out var confidence)
            || confidence.ValueKind != JsonValueKind.Number
            || !confidence.TryGetDecimal(out var parsed)
            || parsed is < 0m or > 1m)
        {
            throw new JsonException($"Field '{name}.confidence' must be a number in [0, 1].");
        }

        return parsed;
    }

    private static string? Evidence(JsonElement field, string name)
    {
        if (!field.TryGetProperty("evidence", out var evidence))
        {
            throw new JsonException($"Field '{name}.evidence' is required (null when there is no evidence).");
        }

        return evidence.ValueKind == JsonValueKind.String ? evidence.GetString() : null;
    }
}
