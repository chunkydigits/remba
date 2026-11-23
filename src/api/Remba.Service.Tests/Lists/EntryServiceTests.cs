using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Entities;
using Remba.Model.Lists;
using Remba.Repository.Entities;
using Remba.Repository.Lists;
using Remba.Service.Entities;
using Remba.Service.Lists;

namespace Remba.Service.Tests.Lists;

public class EntryServiceTests
{
    private readonly Mock<IListRepository> _listRepository = new();
    private readonly Mock<IEntryRepository> _entryRepository = new();
    private readonly Mock<IEntityRepository> _entityRepository = new();
    private readonly Mock<IEntityLookupService> _lookupService = new();

    private EntryService CreateService() =>
        new(
            _listRepository.Object,
            _entryRepository.Object,
            _entityRepository.Object,
            _lookupService.Object,
            NullLogger<EntryService>.Instance);

    private static ListDbo SampleList(string entityType = "film") =>
        new()
        {
            Id = "list1",
            OwnerId = "user1",
            RecordType = ListRecordTypes.List,
            Name = "Films",
            Configuration = new ListConfigurationDbo { EntityType = entityType },
            ViewSettings = new ListViewSettingsDbo()
        };

    [Fact]
    public async Task CreateAsync_WhenValid_ThenShouldCreateEntityAndEntry()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList());
        _entityRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo entity, CancellationToken _) => entity);
        _entryRepository
            .Setup(repository => repository.FindActiveByEntityAsync(
                "user1",
                "list1",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo?)null);
        _entryRepository
            .Setup(repository => repository.GetNextOrderAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _entryRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var result = await CreateService().CreateAsync(
            "user1",
            "list1",
            new CreateEntryRequest { Name = "  The Matrix  " });

        Assert.Equal("The Matrix", result.Name);
        Assert.Equal(1, result.Order);
    }

    [Fact]
    public async Task CreateAsync_WhenImdbIdentifier_ThenShouldEnrichDirectorAndActors()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("film"));
        _lookupService
            .Setup(service => service.GetByIdentifierAsync(
                "film",
                EntityIdentifierTypes.ImdbId,
                "tt0133093",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EntitySearchResultDto
                {
                    Name = "The Matrix",
                    Director = "Lana Wachowski, Lilly Wachowski",
                    Actors = "Keanu Reeves, Laurence Fishburne",
                    Genre = "Action, Sci-Fi",
                    Link = "https://www.imdb.com/title/tt0133093/"
                });
        _entityRepository
            .Setup(repository => repository.FindByIdentifierAsync(
                EntityIdentifierTypes.ImdbId,
                "tt0133093",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo?)null);
        _entityRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo entity, CancellationToken _) => entity);
        _entryRepository
            .Setup(repository => repository.FindActiveByEntityAsync(
                "user1",
                "list1",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo?)null);
        _entryRepository
            .Setup(repository => repository.GetNextOrderAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _entryRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var result = await CreateService().CreateAsync(
            "user1",
            "list1",
            new CreateEntryRequest
            {
                Name = "The Matrix",
                Identifier = new EntityIdentifierRequest
                {
                    Type = EntityIdentifierTypes.ImdbId,
                    Value = "tt0133093"
                }
            });

        Assert.Equal("Lana Wachowski, Lilly Wachowski", result.Director);
        Assert.Contains("Keanu Reeves", result.Actors);
        Assert.Equal("Action, Sci-Fi", result.Genre);
    }

    [Fact]
    public async Task CreateAsync_WhenBook_ThenShouldDefaultReadFalse()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("book"));
        _entityRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo entity, CancellationToken _) => entity);
        _entryRepository
            .Setup(repository => repository.FindActiveByEntityAsync(
                "user1",
                "list1",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo?)null);
        _entryRepository
            .Setup(repository => repository.GetNextOrderAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _entryRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var result = await CreateService().CreateAsync(
            "user1",
            "list1",
            new CreateEntryRequest { Name = "The Hobbit", Author = "J.R.R. Tolkien" });

        Assert.Equal("J.R.R. Tolkien", result.Author);
        Assert.False(result.Read);
    }

    [Fact]
    public async Task CreateAsync_WhenCountry_ThenShouldDefaultVisitedFalse()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("country"));
        _entityRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo entity, CancellationToken _) => entity);
        _entryRepository
            .Setup(repository => repository.FindActiveByEntityAsync(
                "user1",
                "list1",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo?)null);
        _entryRepository
            .Setup(repository => repository.GetNextOrderAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _entryRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var result = await CreateService().CreateAsync(
            "user1",
            "list1",
            new CreateEntryRequest { Name = "Japan" });

        Assert.Equal("Japan", result.Name);
        Assert.False(result.Visited);
    }

    [Fact]
    public async Task CreateAsync_WhenFilm_ThenShouldDefaultSeenFalse()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("film"));
        _entityRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntityDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityDbo entity, CancellationToken _) => entity);
        _entryRepository
            .Setup(repository => repository.FindActiveByEntityAsync(
                "user1",
                "list1",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo?)null);
        _entryRepository
            .Setup(repository => repository.GetNextOrderAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _entryRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var result = await CreateService().CreateAsync(
            "user1",
            "list1",
            new CreateEntryRequest { Name = "Inception" });

        Assert.Equal("Inception", result.Name);
        Assert.False(result.Seen);
        Assert.Null(result.SeenAt);
    }

    [Fact]
    public async Task UpdateAsync_WhenMarkingSeen_ThenShouldStampSeenAt()
    {
        var existing = new EntryDbo
        {
            Id = "entry1",
            ListId = "list1",
            OwnerId = "user1",
            Name = "Inception",
            Seen = false,
            SeenAt = null,
            Order = 1,
            Added = DateTime.UtcNow.AddDays(-1)
        };

        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("film"));
        _entryRepository
            .Setup(repository => repository.GetByIdAsync("user1", "entry1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _entryRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        var before = DateTime.UtcNow;
        var result = await CreateService().UpdateAsync(
            "user1",
            "list1",
            "entry1",
            new UpdateEntryRequest { Seen = true });

        Assert.True(result.Seen);
        Assert.NotNull(result.SeenAt);
        Assert.True(result.SeenAt >= before);
    }

    [Fact]
    public async Task GetByListAsync_WhenQueryProvided_ThenShouldFilterAndSort()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList("book"));
        _entryRepository
            .Setup(repository => repository.GetByListAsync("user1", "list1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EntryDbo
                {
                    Id = "1",
                    Name = "The Hobbit",
                    Author = "Tolkien",
                    Read = true,
                    Order = 2
                },
                new EntryDbo
                {
                    Id = "2",
                    Name = "Dune",
                    Author = "Herbert",
                    Read = false,
                    Order = 1
                }
            ]);

        var results = await CreateService().GetByListAsync(
            "user1",
            "list1",
            new EntryQuery { Q = "hobbit", SortBy = "name" });

        Assert.Single(results);
        Assert.Equal("The Hobbit", results[0].Name);
    }

    [Fact]
    public async Task GetListIdsContainingAsync_WhenIdentifierProvided_ThenShouldQueryRepository()
    {
        _entryRepository
            .Setup(repository => repository.FindActiveListIdsContainingAsync(
                "user1",
                "isbn",
                "9780000000000",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "list1", "list2" });

        var results = await CreateService().GetListIdsContainingAsync(
            "user1",
            "isbn",
            "9780000000000",
            null);

        Assert.Equal(2, results.Count);
        Assert.Contains("list1", results);
        Assert.Contains("list2", results);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenValid_ThenShouldMarkDeleted()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleList());
        _entryRepository
            .Setup(repository => repository.GetByIdAsync("user1", "entry1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EntryDbo
                {
                    Id = "entry1",
                    ListId = "list1",
                    OwnerId = "user1",
                    EntityId = "entity1",
                    Name = "Item",
                    RecordType = ListRecordTypes.Entry,
                    Order = 1
                });
        _entryRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<EntryDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntryDbo entry, CancellationToken _) => entry);

        await CreateService().SoftDeleteAsync("user1", "list1", "entry1");

        _entryRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<EntryDbo>(entry => entry.Deleted != null && entry.DeletedByUserId == "user1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
