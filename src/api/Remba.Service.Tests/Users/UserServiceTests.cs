using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Remba.Model.Configuration;
using Remba.Model.Users;
using Remba.Repository.Users;
using Remba.Service.Users;

namespace Remba.Service.Tests.Users;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private UserService CreateService() =>
        new(
            _repository.Object,
            _cache,
            Options.Create(new UserCacheOptions { AbsoluteExpirationSeconds = 60 }),
            NullLogger<UserService>.Instance);

    [Fact]
    public async Task EnsureCurrentUserAsync_WhenUserMissing_ThenShouldCreateUser()
    {
        const string subject = "auth0|abc123";
        _repository
            .Setup(repository => repository.GetByAuth0SubjectAsync(subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDbo?)null);
        _repository
            .Setup(repository => repository.UsernameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(repository => repository.CreateAsync(It.IsAny<UserDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDbo user, CancellationToken _) => user);

        var service = CreateService();

        var result = await service.EnsureCurrentUserAsync(subject, "Ada Lovelace", "ada", "ada@example.com");

        Assert.False(string.IsNullOrWhiteSpace(result.Id));
        Assert.Equal("Ada Lovelace", result.DisplayName);
        Assert.Equal("ada@example.com", result.Email);
        Assert.StartsWith("ada-", result.Username);
        Assert.Equal(ProfileVisibility.Private, result.Profile.Visibility);
        _repository.Verify(
            repository => repository.CreateAsync(
                It.Is<UserDbo>(user => user.Email == "ada@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureCurrentUserAsync_WhenUserExists_ThenShouldReturnExistingWithoutCreate()
    {
        const string subject = "auth0|existing";
        var existing = new UserDbo
        {
            Id = "userid1",
            Auth0Subject = subject,
            Username = "existing-user",
            DisplayName = "Existing User",
            Profile = new UserProfileDbo { Visibility = ProfileVisibility.Private },
            Added = DateTime.UtcNow.AddDays(-1)
        };

        _repository
            .Setup(repository => repository.GetByAuth0SubjectAsync(subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<UserDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDbo user, CancellationToken _) => user);

        var service = CreateService();

        var result = await service.EnsureCurrentUserAsync(subject, "Ignored", "ignored", "existing@example.com");

        Assert.Equal("userid1", result.Id);
        Assert.Equal("existing-user", result.Username);
        Assert.Equal("existing@example.com", result.Email);
        _repository.Verify(repository => repository.CreateAsync(It.IsAny<UserDbo>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(
            repository => repository.UpdateAsync(
                It.Is<UserDbo>(user => user.Email == "existing@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenUsernameTaken_ThenShouldThrow()
    {
        var user = new UserDbo
        {
            Id = "userid1",
            Auth0Subject = "auth0|1",
            Username = "current",
            DisplayName = "Current",
            Profile = new UserProfileDbo(),
            Added = DateTime.UtcNow
        };

        _repository
            .Setup(repository => repository.GetByIdAsync("userid1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repository
            .Setup(repository => repository.UsernameExistsAsync("taken", "userid1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCurrentUserAsync("userid1", new UpdateUserRequest { Username = "taken" }));
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenValid_ThenShouldPersistAndReturnDto()
    {
        var user = new UserDbo
        {
            Id = "userid1",
            Auth0Subject = "auth0|1",
            Username = "current",
            DisplayName = "Current",
            Profile = new UserProfileDbo { Visibility = ProfileVisibility.Private },
            Added = DateTime.UtcNow
        };

        _repository
            .Setup(repository => repository.GetByIdAsync("userid1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<UserDbo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDbo updated, CancellationToken _) => updated);

        var service = CreateService();

        var result = await service.UpdateCurrentUserAsync(
            "userid1",
            new UpdateUserRequest
            {
                DisplayName = "  New Name  ",
                Profile = new UpdateUserProfileRequest
                {
                    Bio = "Hello",
                    Visibility = ProfileVisibility.Public
                }
            });

        Assert.Equal("New Name", result.DisplayName);
        Assert.Equal("Hello", result.Profile.Bio);
        Assert.Equal(ProfileVisibility.Public, result.Profile.Visibility);
        Assert.NotNull(result.Edited);
    }
}
