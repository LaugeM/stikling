using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class ParentGuessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Copy_and_duplicate_keep_the_guess()
    {
        var plant = new Plant { Nickname = "Cutting", ParentPlantId = Guid.NewGuid(), ParentIsGuess = true };
        var propagation = new Propagation { Nickname = "Cutting", ParentPlantId = Guid.NewGuid(), ParentIsGuess = true };

        Assert.True(plant.Copy().ParentIsGuess);
        Assert.True(plant.Duplicate().ParentIsGuess);
        Assert.True(propagation.Copy().ParentIsGuess);
    }

    [Fact]
    public void Older_records_read_as_a_sure_parent()
    {
        var plant = JsonSerializer.Deserialize<Plant>("""{"nickname":"Old","parentPlantId":"3f2c7c36-7c6c-4a41-9f1e-0d1f4b2f6a11"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.False(plant.ParentIsGuess);
    }

    [Fact]
    public async Task Taking_the_parent_away_takes_the_guess_with_it()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var plant = new Plant { Nickname = "Cutting", ParentPlantId = Guid.NewGuid(), ParentIsGuess = true };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.ParentPlantId = null;

        await service.UpdateAsync(before, plant, e => e.ToString());

        Assert.False(plants.Plants[plant.Id].ParentIsGuess);
    }
}
