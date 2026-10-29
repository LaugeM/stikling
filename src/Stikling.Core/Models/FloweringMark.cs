using System.Text.Json;
using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>How far along a flowering is. A mark says which of these a plant had reached.</summary>
public enum FloweringStage
{
    /// <summary>A flower spike or buds have shown.</summary>
    Buds,

    /// <summary>The flowers are open.</summary>
    InFlower,

    /// <summary>The flowering is over.</summary>
    Done
}

/// <summary>Writes the stage as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantFloweringStageConverter : JsonConverter<FloweringStage?>
{
    public override bool HandleNull => true;

    public override FloweringStage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<FloweringStage>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        // Skip throws when a backup is read from a stream in parts. A converter always gets the whole value, so TrySkip is enough.
        reader.TrySkip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, FloweringStage? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// A plant reached a stage of flowering on a day. A flowering is worked out from the marks, not
/// stored, so two devices marking the same plant only add records and never overwrite each other.
/// </summary>
public sealed class FloweringMark : Entity
{
    public Guid PlantId { get; set; }

    /// <summary>
    /// Null when a later version wrote a stage this one doesn't know. Such a mark is kept but
    /// ignored, so it isn't lost and doesn't stop anything loading.
    /// </summary>
    [JsonConverter(typeof(TolerantFloweringStageConverter))]
    public FloweringStage? Stage { get; set; }

    /// <summary>A whole day for marks made now. A past flowering can be told as a month or a year.</summary>
    public LooseDate On { get; set; } = LooseDate.Of(1);

    /// <summary>The history line written with the mark, so removing the mark takes it off again.</summary>
    public Guid? TimelineEntryId { get; set; }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (PlantId == Guid.Empty)
            errors.Add(Strings.Pests.FloweringMarkNeedsPlant);
        if (Stage is null)
            errors.Add(Strings.Pests.FloweringMarkPickStage);
        if (On is null)
            errors.Add(Strings.Pests.PickDate);
        else if (On.Start > today)
            errors.Add(Strings.Pests.FloweringInFuture);
        return errors;
    }
}
