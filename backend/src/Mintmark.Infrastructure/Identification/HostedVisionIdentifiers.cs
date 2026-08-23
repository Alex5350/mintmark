using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mintmark.Application;
using Mintmark.Application.Ports;

namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// OpenAI vision adapter. Sends the identify-v1 prompt from
/// <see cref="PromptCatalog"/> (with the edge clause substituted) plus the
/// photo(s) as data URLs, and strictly parses the JSON reply. Selected by
/// configuration only when an OpenAI key is present.
/// </summary>
public sealed class OpenAIVisionIdentifier : IVisionIdentifier
{
    private const string ApiVersion = "openai-chat-v1";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    /// <summary>Initializes the adapter.</summary>
    public OpenAIVisionIdentifier(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _http.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        _http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
    }

    /// <inheritdoc />
    public async Task<VisionIdentification> IdentifyAsync(ImageInput input, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = BuildContent(input),
                },
            },
            response_format = new { type = "json_object" },
        };

        var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken)
            ?? throw new InvalidOperationException("OpenAI returned an empty body.");

        var content = body.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("OpenAI returned no choices.");

        return VisionResponseParser.Parse(content, _model, ApiVersion);
    }

    private static List<object> BuildContent(ImageInput input)
    {
        var prompt = PromptCatalog.IdentifyPromptTemplate.Replace(
            "{EDGE_CLAUSE}",
            input.EdgeBytes is null ? string.Empty : ", and the edge", StringComparison.Ordinal);

        var content = new List<object> { new { type = "text", text = prompt } };
        foreach (var bytes in new[] { input.ObverseBytes, input.ReverseBytes, input.EdgeBytes })
        {
            if (bytes is null)
            {
                continue;
            }

            content.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}", detail = "high" },
            });
        }

        return content;
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

/// <summary>
/// Gemini vision adapter (generativelanguage.googleapis.com). Same identify-v1
/// prompt and strict parsing as the OpenAI adapter; the key travels in the
/// <c>x-goog-api-key</c> header.
/// </summary>
public sealed class GeminiVisionIdentifier : IVisionIdentifier
{
    private const string ApiVersion = "gemini-generate-content-v1";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    /// <summary>Initializes the adapter.</summary>
    public GeminiVisionIdentifier(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _http.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/");
        _http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    }

    /// <inheritdoc />
    public async Task<VisionIdentification> IdentifyAsync(ImageInput input, CancellationToken cancellationToken = default)
    {
        var prompt = PromptCatalog.IdentifyPromptTemplate.Replace(
            "{EDGE_CLAUSE}",
            input.EdgeBytes is null ? string.Empty : ", and the edge", StringComparison.Ordinal);

        var parts = new List<object> { new { text = prompt } };
        foreach (var bytes in new[] { input.ObverseBytes, input.ReverseBytes, input.EdgeBytes })
        {
            if (bytes is null)
            {
                continue;
            }

            parts.Add(new
            {
                inline_data = new { mime_type = "image/jpeg", data = Convert.ToBase64String(bytes) },
            });
        }

        var payload = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts } },
            generationConfig = new { responseMimeType = "application/json" },
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(
            $"v1beta/models/{_model}:generateContent",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken),
            default);
        var text = string.Concat(document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")
            .EnumerateArray()
            .Where(p => p.TryGetProperty("text", out _))
            .Select(p => p.GetProperty("text").GetString()));

        return VisionResponseParser.Parse(text, _model, ApiVersion);
    }
}
