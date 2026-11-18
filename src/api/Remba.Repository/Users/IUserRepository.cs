using Remba.Model.Users;

namespace Remba.Repository.Users;

public interface IUserRepository
{
    Task<UserDbo?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserDbo?> GetByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default);

    Task<UserDbo> CreateAsync(UserDbo user, CancellationToken cancellationToken = default);

    Task<UserDbo> UpdateAsync(UserDbo user, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, string? excludeUserId = null, CancellationToken cancellationToken = default);
}
