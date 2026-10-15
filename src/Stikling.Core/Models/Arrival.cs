using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>How a plant was when it arrived.</summary>
public enum ArrivalRating
{
    Good,
    Ok,
    Poor
}

/// <summary>What was wrong when a plant arrived. More than one can apply.</summary>
public enum ArrivalProblem
{
    Pests,
    Damaged,
    RootRot,
    WrongPlant,
    SmallerThanShown
}

/// <summary>Writes an optional enum as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantNullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
{
    public override bool HandleNull => true;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<T>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.TrySkip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>Writes a list of enums as text and drops the names this version doesn't know, so a later value can't break reading the record.</summary>
public sealed class TolerantEnumListConverter<T> : JsonConverter<List<T>> where T : struct, Enum
{
    public override bool HandleNull => true;

    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<T>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.TrySkip();
            return list;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<T>(reader.GetString(), out var value)
                && Enum.IsDefined(value))
            {
                if (!list.Contains(value))
                    list.Add(value);
            }
            else
                reader.TrySkip();
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value ?? [])
            writer.WriteStringValue(item.ToString());
        writer.WriteEndArray();
    }
}

/// <summary>How a plant or propagation arrived, said in a few words.</summary>
public static class Arrival
{
    public static string Text(ArrivalProblem problem) => problem switch
    {
        ArrivalProblem.Pests => "pests",
        ArrivalProblem.Damaged => "damaged or broken",
        ArrivalProblem.RootRot => "root rot",
        ArrivalProblem.WrongPlant => "the wrong plant",
        ArrivalProblem.SmallerThanShown => "smaller than shown",
        _ => problem.ToString()
    };

    /// <summary>"Arrived in poor shape: pests, root rot" when it was rated Poor, else null.</summary>
    public static string? PoorText(ArrivalRating? rating, IEnumerable<ArrivalProblem> problems)
    {
        if (rating != ArrivalRating.Poor)
            return null;
        var list = problems.Select(Text).ToList();
        return list.Count == 0 ? "arrived in poor shape" : $"arrived in poor shape: {string.Join(", ", list)}";
    }
}
