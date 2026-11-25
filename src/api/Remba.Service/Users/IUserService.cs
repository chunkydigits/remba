using Remba.Model.Users;

namespace Remba.Service.Users;

public interface IUserService
{
    Task<UserDto> EnsureCurrentUserAsync(
        string auth0Subject,
        string? preferredDisplayName,
        string? preferredUsernameSeed,
        string? email,
        CancellationToken cancellationToken = default);

    Task<UserDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateCurrentUserAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);
}
