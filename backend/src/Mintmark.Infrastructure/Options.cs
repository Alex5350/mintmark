namespace Mintmark.Infrastructure;

/// <summary>
/// Strongly-typed configuration groups. Every group binds one section of
/// appsettings.json, whose values come from the MINTMARK_* environment
/// variables (the config contract in .env.example). Validated fail-fast at
/// startup by the composition root.
/// </summary>
public static class OptionsGroups
{
    /// <summary>The <c>Database</c> section name.</summary>
    public const string Database = "Database";

    /// <summary>The <c>Jwt</c> section name.</summary>
    public const string Jwt = "Jwt";

    /// <summary>The <c>Storage</c> section name.</summary>
    public const string Storage = "Storage";

    /// <summary>The <c>Price</c> section name.</summary>
    public const string Price = "Price";

    /// <summary>The <c>Vision</c> section name.</summary>
    public const string Vision = "Vision";

    /// <summary>The <c>Identification</c> section name.</summary>
    public const string Identification = "Identification";
}

/// <summary>PostgreSQL connection settings (binds <c>MINTMARK_DATABASE</c>).</summary>
public sealed class DatabaseOptions
{
    /// <summary>Gets the Npgsql connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether to apply EF migrations at startup.
    /// Development convenience only; production applies migrations
    /// deliberately (see docs/runbook.md).
    /// </summary>
    public bool AutoMigrate { get; init; }
}

/// <summary>JWT + refresh-token settings (binds <c>MINTMARK_JWT_*</c>).</summary>
public sealed class JwtOptions
{
    /// <summary>Gets the symmetric signing key (base64 or long random string; at least 32 characters).</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Gets the issuer claim value.</summary>
    public string Issuer { get; init; } = "mintmark-api";

    /// <summary>Gets the audience claim value.</summary>
    public string Audience { get; init; } = "mintmark-clients";

    /// <summary>Gets the access-token lifetime in minutes (ADR 0005: short by design).</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Gets the refresh-token lifetime in days.</summary>
    public int RefreshTokenDays { get; init; } = 30;
}

/// <summary>S3-compatible object-storage settings (binds <c>MINTMARK_STORAGE_*</c>).</summary>
public sealed class StorageOptions
{
    /// <summary>Gets the endpoint host[:port] (MinIO locally).</summary>
    public string Endpoint { get; init; } = "localhost:9000";

    /// <summary>Gets the access key.</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Gets the secret key.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Gets the private bucket name for coin images.</summary>
    public string Bucket { get; init; } = "mintmark-images";

    /// <summary>Gets a value indicating whether to talk HTTPS to the endpoint.</summary>
    public bool UseSsl { get; init; }
}

/// <summary>Spot-price provider settings (binds <c>MINTMARK_PRICE_*</c>).</summary>
public sealed class PriceOptions
{
    /// <summary>Gets the primary provider id (<c>metalsdev</c> or <c>goldapicom</c>).</summary>
    public string Primary { get; init; } = "metalsdev";

    /// <summary>Gets the primary provider API key.</summary>
    public string? PrimaryKey { get; init; }

    /// <summary>Gets the fallback provider id.</summary>
    public string Fallback { get; init; } = "goldapicom";

    /// <summary>Gets the fallback provider API key (optional for gold-api.com spot).</summary>
    public string? FallbackKey { get; init; }

    /// <summary>Gets the monthly request budget across providers; the poll interval is derived from it.</summary>
    public int MonthlyBudget { get; init; } = 250;

    /// <summary>Gets the base quote currency (three uppercase letters).</summary>
    public string BaseCurrency { get; init; } = "USD";
}

/// <summary>Vision identification settings (binds <c>MINTMARK_VISION_*</c>).</summary>
public sealed class VisionOptions
{
    /// <summary>Gets the provider selector: <c>offline</c>, <c>openai</c> or <c>gemini</c>.</summary>
    public string Provider { get; init; } = "offline";

    /// <summary>Gets the hosted vision model id (required when a hosted provider is selected).</summary>
    public string? Model { get; init; }

    /// <summary>Gets the OpenAI API key.</summary>
    public string? OpenAIKey { get; init; }

    /// <summary>Gets the Gemini API key.</summary>
    public string? GeminiKey { get; init; }
}

/// <summary>Identification limits (binds <c>MINTMARK_IDENTIFY_DAILY_LIMIT</c>).</summary>
public sealed class IdentificationOptions
{
    /// <summary>Gets the per-user identification runs allowed per UTC day.</summary>
    public int DailyLimit { get; init; } = 25;
}
