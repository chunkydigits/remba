using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.System;
using Remba.Repository.System;
using Remba.Service.System;

namespace Remba.Service.Tests.System;

public class EntityTypeServiceTests
{
    private readonly Mock<IEntityTypeRepository> _repository = new();

    private EntityTypeService CreateService() =>
        new(_repository.Object, NullLogger<EntityTypeService>.Instance);

    [Fact]
    public async Task EnsureDefaultsAsync_WhenMissing_ThenShouldSeedSystemTypes()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityTypeDbo?)null);
        _repository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityTypeDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityTypeDbo item, CancellationToken _) => item);

        await CreateService().EnsureDefaultsAsync();

        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<EntityTypeDbo>(item =>
                    item.Visibility == EntityTypeVisibility.System
                    && item.Key == "film"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.CreateAsync(It.IsAny<EntityTypeDbo>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task CreatePrivateAsync_WhenValid_ThenShouldCreateWithoutLookup()
    {
        _repository
            .Setup(repository => repository.FindByKeyAsync("vinyl", "user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityTypeDbo?)null);
        _repository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityTypeDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityTypeDbo item, CancellationToken _) => item);

        var result = await CreateService().CreatePrivateAsync(
            "user1",
            new CreateEntityTypeRequest { Name = "  Vinyl Records  " });

        Assert.Equal("vinyl-records", result.Key);
        Assert.Equal("Vinyl Records", result.Name);
        Assert.Equal(EntityTypeVisibility.Private, result.Visibility);
        Assert.False(result.SupportsLookup);
    }

    [Fact]
    public async Task CreatePrivateAsync_WhenDuplicate_ThenShouldThrow()
    {
        _repository
            .Setup(repository => repository.FindByKeyAsync("film", "user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EntityTypeDbo
                {
                    Key = "film",
                    Name = "Film",
                    Visibility = EntityTypeVisibility.System
                });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreatePrivateAsync("user1", new CreateEntityTypeRequest { Name = "Film" }));
    }
}
