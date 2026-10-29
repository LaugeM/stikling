using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class SavedTextTests
{
    [Fact]
    public void A_departure_is_saved_in_English_and_shown_in_the_language_of_the_app()
    {
        var plant = new Plant { Status = PlantStatus.Sold, LeftTo = "Anna", LeftFor = "150" };
        var was = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");
            Assert.Equal("Sold to Anna for 150", plant.DescribeDepartureForHistory());
            Assert.NotEqual(plant.DescribeDepartureForHistory(), plant.DescribeDeparture());
            Assert.Equal("da-DK", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
    }
}
