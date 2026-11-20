using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Lists;
using Remba.Repository.Lists;
using Remba.Service.Lists;

namespace Remba.Service.Tests.Lists;

public class ListServiceTests
{
    private readonly Mock<IListRepository> _repository = new();

    private ListService CreateService() =>
        new(_repository.Object, NullLogger<ListService>.Instance);

    [Fact]
    public async Task CreateAsync_WhenValid_ThenShouldCreatePrivateListByDefault()
    {
        _repository
            .Setup(repository => repository.CreateAsync(It.IsAny<ListDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListDbo list, CancellationToken _) => list);

        var service = CreateService();

        var result = await service.CreateAsync(
            "user1",
            new CreateListRequest { Name = "  Films to Watch  ", Description = " Weekend picks " });

        Assert.Equal("Films to Watch", result.Name);
        Assert.Equal("Weekend picks", result.Description);
        Assert.Equal(ListVisibility.Private, result.Visibility);
        Assert.Equal("manual", result.ViewSettings.SortBy);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<ListDbo>(list =>
                    list.OwnerId == "user1"
                    && list.RecordType == ListRecordTypes.List
                    && list.Visibility == ListVisibility.Private),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenListMissing_ThenShouldThrowNotFound()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync("user1", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListDbo?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync("user1", "missing", new UpdateListRequest { Name = "Renamed" }));
    }

    [Fact]
    public async Task UpdateViewSettingsAsync_WhenValid_ThenShouldPersistSettings()
    {
        var existing = new ListDbo
        {
            Id = "list1",
            OwnerId = "user1",
            RecordType = ListRecordTypes.List,
            Name = "Books",
            Visibility = ListVisibility.Private,
            Configuration = new ListConfigurationDbo(),
            ViewSettings = new ListViewSettingsDbo(),
            Added = DateTime.UtcNow.AddDays(-1),
            AddedByUserId = "user1"
        };

        _repository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ListDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListDbo list, CancellationToken _) => list);

        var service = CreateService();

        var result = await service.UpdateViewSettingsAsync(
            "user1",
            "list1",
            new UpdateListViewSettingsRequest
            {
                SortBy = "name",
                SortDirection = "descending",
                ViewMode = "grid",
                VisibleFields = ["name", "Author", "read"]
            });

        Assert.Equal("name", result.ViewSettings.SortBy);
        Assert.Equal(ListSortDirections.Descending, result.ViewSettings.SortDirection);
        Assert.Equal(ListViewModes.Grid, result.ViewSettings.ViewMode);
        Assert.Equal(["name", "author", "read"], result.ViewSettings.VisibleFields);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwned_ThenShouldDelete()
    {
        var existing = new ListDbo
        {
            Id = "list1",
            OwnerId = "user1",
            RecordType = ListRecordTypes.List,
            Name = "Books",
            Added = DateTime.UtcNow,
            AddedByUserId = "user1"
        };

        _repository
            .Setup(repository => repository.GetByIdAsync("user1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService();

        await service.DeleteAsync("user1", "list1");

        _repository.Verify(
            repository => repository.DeleteAsync("user1", "list1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
