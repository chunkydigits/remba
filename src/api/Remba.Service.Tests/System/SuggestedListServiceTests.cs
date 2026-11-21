using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.System;
using Remba.Repository.System;
using Remba.Service.System;

namespace Remba.Service.Tests.System;

public class SuggestedListServiceTests
{
    private readonly Mock<ISuggestedListRepository> _repository = new();

    private SuggestedListService CreateService() =>
        new(_repository.Object, NullLogger<SuggestedListService>.Instance);

    [Fact]
    public async Task EnsureDefaultsAsync_WhenMissing_ThenShouldCreateBuiltInTemplates()
    {
        _repository
            .Setup(repository => repository.GetBySeedKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedListDbo?)null);
        _repository
            .Setup(repository => repository.CreateAsync(It.IsAny<SuggestedListDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedListDbo item, CancellationToken _) => item);

        var service = CreateService();

        await service.EnsureDefaultsAsync();

        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<SuggestedListDbo>(item => item.SeedKey == SuggestedListSeedKeys.Films),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<SuggestedListDbo>(item => item.SeedKey == SuggestedListSeedKeys.Books),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<SuggestedListDbo>(item => item.SeedKey == SuggestedListSeedKeys.Recipes),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<SuggestedListDbo>(item => item.SeedKey == SuggestedListSeedKeys.Tv),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<SuggestedListDbo>(item =>
                    item.SeedKey == SuggestedListSeedKeys.Countries
                    && item.Identifier.Type == SuggestedListIdentifierTypes.Iso2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultsAsync_WhenAlreadyPresent_ThenShouldNotOverwrite()
    {
        var existing = SuggestedListDefaults.CreateFilms(DateTime.UtcNow);
        existing.Name = "Movies";

        _repository
            .Setup(repository => repository.GetBySeedKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService();

        await service.EnsureDefaultsAsync();

        _repository.Verify(
            repository => repository.CreateAsync(It.IsAny<SuggestedListDbo>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<SuggestedListDbo>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureDefaultsAsync_WhenCountriesStillOnUrlProvider_ThenShouldUpgrade()
    {
        var existing = SuggestedListDefaults.CreateCountries(DateTime.UtcNow);
        existing.Identifier.Type = SuggestedListIdentifierTypes.Url;
        existing.Enrichment.Provider = "url";

        _repository
            .Setup(repository => repository.GetBySeedKeyAsync(
                SuggestedListSeedKeys.Countries,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(repository => repository.GetBySeedKeyAsync(
                It.Is<string>(key => key != SuggestedListSeedKeys.Countries),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuggestedListDefaults.CreateFilms(DateTime.UtcNow));
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<SuggestedListDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedListDbo item, CancellationToken _) => item);

        var service = CreateService();

        await service.EnsureDefaultsAsync();

        _repository.Verify(
            repository => repository.UpdateAsync(
                It.Is<SuggestedListDbo>(item =>
                    item.SeedKey == SuggestedListSeedKeys.Countries
                    && item.Identifier.Type == SuggestedListIdentifierTypes.Iso2
                    && item.Enrichment.Provider == "flagdex"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WhenIncludeDisabledFalse_ThenShouldOmitDisabled()
    {
        _repository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SuggestedListDbo
                {
                    Id = "films",
                    SeedKey = "films",
                    Name = "Films",
                    Enabled = true,
                    SortOrder = 1
                },
                new SuggestedListDbo
                {
                    Id = "books",
                    SeedKey = "books",
                    Name = "Books",
                    Enabled = false,
                    SortOrder = 2
                }
            ]);

        var service = CreateService();

        var results = await service.GetAllAsync(includeDisabled: false);

        Assert.Single(results);
        Assert.Equal("films", results[0].SeedKey);
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_ThenShouldPersistChanges()
    {
        var existing = SuggestedListDefaults.CreateFilms(DateTime.UtcNow);
        _repository
            .Setup(repository => repository.GetBySeedKeyAsync("films", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<SuggestedListDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedListDbo item, CancellationToken _) => item);

        var service = CreateService();

        var result = await service.UpdateAsync(
            "films",
            new UpdateSuggestedListRequest
            {
                Name = "  Cinema  ",
                Enabled = false,
                SortOrder = 10
            });

        Assert.Equal("Cinema", result.Name);
        Assert.False(result.Enabled);
        Assert.Equal(10, result.SortOrder);
        Assert.NotNull(result.Edited);
        _repository.Verify(
            repository => repository.UpdateAsync(
                It.Is<SuggestedListDbo>(item =>
                    item.Name == "Cinema"
                    && item.Enabled == false
                    && item.SortOrder == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ThenShouldThrowNotFound()
    {
        _repository
            .Setup(repository => repository.GetBySeedKeyAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuggestedListDbo?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync("missing", new UpdateSuggestedListRequest { Name = "Nope" }));
    }
}
