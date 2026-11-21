using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Entities;
using Remba.Repository.Entities;
using Remba.Service.Entities;

namespace Remba.Service.Tests.Entities;

public class EntityLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenProviderSupportsType_ThenShouldDelegate()
    {
        var provider = new Mock<IEntityLookupProvider>();
        provider.SetupGet(item => item.ProviderKey).Returns(EntityLookupProviders.Imdb);
        provider.Setup(item => item.SupportsEntityType("film")).Returns(true);
        provider
            .Setup(item => item.SearchAsync("film", "Matrix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EntitySearchResultDto
                {
                    Provider = EntityLookupProviders.Imdb,
                    IdentifierType = EntityIdentifierTypes.ImdbId,
                    IdentifierValue = "tt0133093",
                    Name = "The Matrix"
                }
            ]);

        var entities = new Mock<IEntityRepository>();
        var service = new EntityLookupService(
            [provider.Object],
            entities.Object,
            NullLogger<EntityLookupService>.Instance);

        var results = await service.SearchAsync(" film ", "  Matrix ");

        Assert.Single(results);
        Assert.Equal("tt0133093", results[0].IdentifierValue);
        provider.Verify(
            item => item.SearchAsync("film", "Matrix", It.IsAny<CancellationToken>()),
            Times.Once);
        entities.Verify(
            item => item.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenProviderEmpty_ThenShouldSearchLocalCatalog()
    {
        var provider = new Mock<IEntityLookupProvider>();
        provider.Setup(item => item.SupportsEntityType("book")).Returns(true);
        provider
            .Setup(item => item.SearchAsync("book", "Obscure Title", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var entities = new Mock<IEntityRepository>();
        entities
            .Setup(item => item.SearchAsync("book", "Obscure Title", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EntityDbo
                {
                    Id = "local1",
                    Type = "book",
                    Name = "Obscure Title",
                    Metadata = new Dictionary<string, string> { ["author"] = "Ada" }
                }
            ]);

        var service = new EntityLookupService(
            [provider.Object],
            entities.Object,
            NullLogger<EntityLookupService>.Instance);

        var results = await service.SearchAsync("book", "Obscure Title");

        Assert.Single(results);
        Assert.Equal(EntityLookupProviders.Remba, results[0].Provider);
        Assert.Equal("local1", results[0].EntityId);
        Assert.Equal("Ada", results[0].Author);
    }

    [Fact]
    public async Task SearchAsync_WhenNoProvider_ThenShouldSearchLocalCatalog()
    {
        var entities = new Mock<IEntityRepository>();
        entities
            .Setup(item => item.SearchAsync("recipe", "pasta", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EntityDbo { Id = "r1", Type = "recipe", Name = "Pasta primavera" }
            ]);

        var service = new EntityLookupService(
            [],
            entities.Object,
            NullLogger<EntityLookupService>.Instance);

        var results = await service.SearchAsync("recipe", "pasta");

        Assert.Single(results);
        Assert.Equal("Pasta primavera", results[0].Name);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryBlank_ThenShouldThrow()
    {
        var provider = new Mock<IEntityLookupProvider>();
        provider.Setup(item => item.SupportsEntityType("book")).Returns(true);

        var service = new EntityLookupService(
            [provider.Object],
            Mock.Of<IEntityRepository>(),
            NullLogger<EntityLookupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("book", "  "));
    }
}
