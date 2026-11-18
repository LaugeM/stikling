using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class CollectionNamesTests
{
    [Fact]
    public void A_name_someone_chose_is_kept()
    {
        Assert.Equal(new CollectionLabel(CollectionLabelKind.Named, "Greenhouse"), CollectionNames.Label("Greenhouse", true, "Anna"));
        Assert.Equal(new CollectionLabel(CollectionLabelKind.Named, "Greenhouse"), CollectionNames.Label("Greenhouse", false, "Anna"));
    }

    [Fact]
    public void The_servers_default_is_yours_for_your_own_collection()
    {
        Assert.Equal(CollectionLabelKind.Yours, CollectionNames.Label("My plants", true, "Anna").Kind);
        Assert.Equal(CollectionLabelKind.Yours, CollectionNames.Label(" my plants ", true, null).Kind);
    }

    [Fact]
    public void The_servers_default_is_the_owners_in_someone_elses_collection()
    {
        var label = CollectionNames.Label("My plants", false, " Anna ");

        Assert.Equal(CollectionLabelKind.Owners, label.Kind);
        Assert.Equal("Anna", label.Text);
    }

    [Fact]
    public void Without_an_owners_name_the_default_stays_as_it_is()
    {
        Assert.Equal(new CollectionLabel(CollectionLabelKind.Named, "My plants"), CollectionNames.Label("My plants", false, null));
        Assert.Equal(new CollectionLabel(CollectionLabelKind.Named, "My plants"), CollectionNames.Label("My plants", false, " "));
    }

    [Fact]
    public void The_registry_collection_is_labelled_the_same_way()
    {
        var shared = new RegisteredCollection(Guid.NewGuid(), "My plants", CollectionRole.Helper, false, false, "Bo");

        Assert.Equal(new CollectionLabel(CollectionLabelKind.Owners, "Bo"), CollectionNames.Label(shared));
    }
}
