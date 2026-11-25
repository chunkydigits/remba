using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Remba.Model.Lists;
using Remba.Model.Users;
using Remba.Repository.Lists;
using Remba.Service.Lists;
using Remba.Service.Users;

namespace Remba.Service.Tests.Lists;

public class ListInviteServiceTests
{
    private readonly Mock<IListInviteRepository> _inviteRepository = new();
    private readonly Mock<IListShareService> _shareService = new();
    private readonly Mock<IListRepository> _listRepository = new();
    private readonly Mock<IUserService> _userService = new();

    private ListInviteService CreateService() =>
        new(
            _inviteRepository.Object,
            _shareService.Object,
            _listRepository.Object,
            _userService.Object,
            NullLogger<ListInviteService>.Instance);

    [Fact]
    public async Task CreateAsync_WhenOwner_ThenShouldCreateUnusedCode()
    {
        var list = new ListDbo
        {
            Id = "list1",
            OwnerId = "owner1",
            Name = "Shows",
            RecordType = ListRecordTypes.List
        };

        _shareService
            .Setup(service => service.RequireAccessAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListAccessContext { List = list, Role = ListShareRoles.Owner });
        _inviteRepository
            .Setup(repository => repository.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListInviteDbo?)null);
        _inviteRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<ListInviteDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListInviteDbo invite, CancellationToken _) => invite);

        var result = await CreateService().CreateAsync(
            "owner1",
            "list1",
            new CreateListInviteRequest { Role = "editor" });

        Assert.False(result.IsUsed);
        Assert.Equal(ListShareRoles.Editor, result.Role);
        Assert.Matches("^[A-Z0-9]{4}-[A-Z0-9]{4}$", result.Code);
    }

    [Fact]
    public async Task RedeemAsync_WhenValid_ThenShouldBurnCodeAndGrantShare()
    {
        var invite = new ListInviteDbo
        {
            Id = "invite1",
            OwnerId = "owner1",
            ListId = "list1",
            ListOwnerId = "owner1",
            ListName = "Shows",
            Code = "ABCD-EFGH",
            Role = ListShareRoles.Editor,
            Added = DateTime.UtcNow.AddMinutes(-5),
            AddedByUserId = "owner1",
            RecordType = ListRecordTypes.ListInvite
        };
        var list = new ListDbo
        {
            Id = "list1",
            OwnerId = "owner1",
            Name = "Shows",
            RecordType = ListRecordTypes.List
        };
        var member = new UserDto
        {
            Id = "member1",
            Username = "partner",
            DisplayName = "Partner"
        };

        _inviteRepository
            .Setup(repository => repository.GetByCodeAsync("ABCD-EFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);
        _shareService
            .Setup(service => service.TryResolveAccessAsync("member1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListAccessContext?)null);
        _listRepository
            .Setup(repository => repository.GetByIdAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
        _userService
            .Setup(service => service.GetByIdAsync("member1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _inviteRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ListInviteDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListInviteDbo updated, CancellationToken _) => updated);

        var result = await CreateService().RedeemAsync(
            "member1",
            new RedeemListInviteRequest { Code = "abcd-efgh" });

        Assert.Equal("list1", result.ListId);
        _shareService.Verify(
            service => service.GrantShareAsync(
                list,
                member,
                ListShareRoles.Editor,
                "owner1",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _inviteRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<ListInviteDbo>(item =>
                    item.UsedAt != null
                    && item.UsedByUserId == "member1"
                    && item.UsedByUsername == "partner"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RedeemAsync_WhenAlreadyUsed_ThenShouldReject()
    {
        _inviteRepository
            .Setup(repository => repository.GetByCodeAsync("ABCD-EFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListInviteDbo
                {
                    Id = "invite1",
                    OwnerId = "owner1",
                    ListId = "list1",
                    ListOwnerId = "owner1",
                    Code = "ABCD-EFGH",
                    UsedAt = DateTime.UtcNow,
                    UsedByUserId = "someone",
                    RecordType = ListRecordTypes.ListInvite
                });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().RedeemAsync("member1", new RedeemListInviteRequest { Code = "ABCD-EFGH" }));
    }

    [Fact]
    public async Task DeleteAsync_WhenUsed_ThenShouldReject()
    {
        var list = new ListDbo
        {
            Id = "list1",
            OwnerId = "owner1",
            Name = "Shows",
            RecordType = ListRecordTypes.List
        };

        _shareService
            .Setup(service => service.RequireAccessAsync("owner1", "list1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListAccessContext { List = list, Role = ListShareRoles.Owner });
        _inviteRepository
            .Setup(repository => repository.GetByIdAsync("owner1", "invite1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListInviteDbo
                {
                    Id = "invite1",
                    OwnerId = "owner1",
                    ListId = "list1",
                    UsedAt = DateTime.UtcNow,
                    RecordType = ListRecordTypes.ListInvite
                });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().DeleteAsync("owner1", "list1", "invite1"));
    }
}
