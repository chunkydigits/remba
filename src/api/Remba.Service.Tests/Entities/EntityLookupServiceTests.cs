using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Entities;
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

        var service = new EntityLookupService(
            [provider.Object],
            NullLogger<EntityLookupService>.Instance);

        var results = await service.SearchAsync(" film ", "  Matrix ");

        Assert.Single(results);
        Assert.Equal("tt0133093", results[0].IdentifierValue);
        provider.Verify(
            item => item.SearchAsync("film", "Matrix", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WhenNoProvider_ThenShouldThrow()
    {
        var service = new EntityLookupService([], NullLogger<EntityLookupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("recipe", "pasta"));
    }

    [Fact]
    public async Task SearchAsync_WhenQueryBlank_ThenShouldThrow()
    {
        var provider = new Mock<IEntityLookupProvider>();
        provider.Setup(item => item.SupportsEntityType("book")).Returns(true);

        var service = new EntityLookupService(
            [provider.Object],
            NullLogger<EntityLookupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("book", "  "));
    }
}
