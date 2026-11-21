using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Core.PressAggregate;

public class PressCreate
{
    [Theory]
    [InlineData("daily")]
    [InlineData("the-weekly-2")]
    public void AcceptsSlugIds(string id) => Press.Create(id, "Name").Id.ShouldBe(id);

    [Theory]
    [InlineData("")]
    [InlineData("Daily")]
    [InlineData("-daily")]
    [InlineData("has space")]
    public void RejectsIdsThatAreNotSlugs(string id) => Should.Throw<DomainException>(() => Press.Create(id, "Name"));

    [Fact]
    public void ACategoryKeepsItsPress() => Category.Create("politics", "daily", "Politics").PressId.ShouldBe("daily");
}
