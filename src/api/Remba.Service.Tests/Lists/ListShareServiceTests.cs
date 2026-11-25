using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Lists;
using Remba.Model.Users;
using Remba.Repository.Lists;
using Remba.Service.Lists;
using Remba.Service.Users;

namespace Remba.Service.Tests.Lists;

public class ListShareServiceTests
{
    private readonly Mock<IListRepository> _listRepository = new();
    private readonly Mock<IListShareRepository> _shareRepository = new();
    private readonly Mock<IShareGroupRepository> _groupRepository = new();
    private readonly Mock<IUserService> _userService = new();

    private ListShareService CreateService() =>
        new(
            _listRepository.Object,
            _shareRepository.Object,
            _groupRepository.Object,
            _userService.Object,
            NullLogger<ListShareService>.Instance);

    [Fact]
    public async Task ShareAsync_WhenPartnerUsername_ThenShouldDualWriteShares()
    {
        var list = new ListDbo
        {
            Id = "list1",
            OwnerId = "owner1",
            Name = "Shows to watch",
            RecordType = ListRecordTypes.List
        };

        _listRepository
            .Setup(repository => repository.GetByIdAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
        _userService
            .Setup(service => service.GetByUsernameAsync("partner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto
            {
                Id = "partner1",
                Username = "partner",
                DisplayName = "Partner"
            });
        _shareRepository
            .Setup(repository => repository.GetByListAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ListShareDbo
                {
                    Id = "share:list1:partner1",
                    OwnerId = "owner1",
                    ListId = "list1",
                    ListOwnerId = "owner1",
                    MemberUserId = "partner1",
                    MemberUsername = "partner",
                    MemberDisplayName = "Partner",
                    Role = ListShareRoles.Editor,
                    ShareLabel = "Us"
                }
            ]);

        var result = await CreateService().ShareAsync(
            "owner1",
            "list1",
            new ShareListRequest
            {
                Usernames = ["partner"],
                Role = "editor",
                ShareLabel = "Us"
            });

        Assert.Single(result);
        Assert.Equal("partner", result[0].Username);
        Assert.Equal("Us", result[0].ShareLabel);

        _shareRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<ListShareDbo>(share =>
                    share.OwnerId == "owner1"
                    && share.MemberUserId == "partner1"
                    && share.Role == ListShareRoles.Editor),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _shareRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<ListShareDbo>(share =>
                    share.OwnerId == "partner1"
                    && share.MemberUserId == "partner1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryResolveAccessAsync_WhenSharedMember_ThenShouldReturnSharedRole()
    {
        _listRepository
            .Setup(repository => repository.GetByIdAsync("partner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListDbo?)null);
        _shareRepository
            .Setup(repository => repository.GetForMemberAsync("partner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListShareDbo
                {
                    Id = "share:list1:partner1",
                    OwnerId = "partner1",
                    ListId = "list1",
                    ListOwnerId = "owner1",
                    MemberUserId = "partner1",
                    Role = ListShareRoles.Viewer
                });
        _listRepository
            .Setup(repository => repository.GetByIdAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListDbo
                {
                    Id = "list1",
                    OwnerId = "owner1",
                    Name = "Ideas",
                    RecordType = ListRecordTypes.List
                });

        var access = await CreateService().TryResolveAccessAsync("partner1", "list1");

        Assert.NotNull(access);
        Assert.Equal(ListShareRoles.Viewer, access!.Role);
        Assert.Equal("owner1", access.List.OwnerId);
    }
}
