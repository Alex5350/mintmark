using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mintmark.Domain;
using Mintmark.Domain.ValueObjects;

namespace Mintmark.Application;

/// <summary>
/// Wire form of the Domain's strongly-typed ids: plain JSON numbers. Keeps
/// the Application DTOs usable at the API edge without leaking their
/// struct shape into client contracts.
/// </summary>
public sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsValueType
        && typeToConvert.Namespace == typeof(HoldingId).Namespace
        && typeToConvert.Name.EndsWith("Id", StringComparison.Ordinal)
        && typeToConvert.GetProperty("Value")?.PropertyType == typeof(long)
        && typeToConvert.GetConstructor([typeof(long)]) is not null;

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(
            typeof(TypedIdJsonConverter<>).MakeGenericType(typeToConvert));

    private sealed class TypedIdJsonConverter<T> : JsonConverter<T>
        where T : struct
    {
        private static readonly Func<T, long> ToLong = BuildToLong();
        private static readonly Func<long, T> FromLong = BuildFromLong();

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return FromLong(reader.GetInt64());
            // Tolerate the pre-converter persisted shape {"Value":n} so
            // existing rows re-read cleanly instead of crashing.
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                long id = 0;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("Value"))
                    {
                        reader.Read();
                        id = reader.GetInt64();
                    }
                }
                return FromLong(id);
            }
            throw new JsonException($"Expected a numeric id for {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(ToLong(value));

        private static Func<T, long> BuildToLong()
        {
            var parameter = Expression.Parameter(typeof(T), "value");
            return Expression.Lambda<Func<T, long>>(
                Expression.Property(parameter, nameof(HoldingId.Value)),
                parameter).Compile();
        }

        private static Func<long, T> BuildFromLong()
        {
            var constructor = typeof(T).GetConstructor([typeof(long)])
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no long constructor.");
            var parameter = Expression.Parameter(typeof(long), "value");
            return Expression.Lambda<Func<long, T>>(
                Expression.New(constructor, parameter),
                parameter).Compile();
        }
    }
}

/// <summary>Wire form of <see cref="Currency"/>: the three-letter code string.</summary>
public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    /// <inheritdoc />
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new Currency(reader.GetString()!)
            : throw new JsonException("Expected a three-letter currency code.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Code);
}
