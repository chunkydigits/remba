using System.Collections.Concurrent;
using Remba.Model.Users;
using Remba.Utility;

namespace Remba.Repository.Users;

/// <summary>
/// Local-development fallback when Cosmos DB is not configured.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, UserDbo> _usersById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idsByAuth0Subject = new(StringComparer.Ordinal);

    public Task<UserDbo?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(Clone(user));
    }

    public Task<UserDbo?> GetByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default)
    {
        if (!_idsByAuth0Subject.TryGetValue(auth0Subject, out var userId))
        {
            return Task.FromResult<UserDbo?>(null);
        }

        return GetByIdAsync(userId, cancellationToken);
    }

    public Task<UserDbo> CreateAsync(UserDbo user, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(user.Id))
        {
            throw new ArgumentException("User id is required.", nameof(user));
        }

        if (StringUtilities.IsNullOrWhiteSpace(user.Auth0Subject))
        {
            throw new ArgumentException("Auth0 subject is required.", nameof(user));
        }

        if (!_usersById.TryAdd(user.Id, Clone(user)!))
        {
            throw new InvalidOperationException($"User '{user.Id}' already exists.");
        }

        if (!_idsByAuth0Subject.TryAdd(user.Auth0Subject, user.Id))
        {
            _usersById.TryRemove(user.Id, out _);
            throw new InvalidOperationException($"Auth0 subject '{user.Auth0Subject}' is already mapped.");
        }

        return Task.FromResult(Clone(user)!);
    }

    public Task<UserDbo> UpdateAsync(UserDbo user, CancellationToken cancellationToken = default)
    {
        if (!_usersById.ContainsKey(user.Id))
        {
            throw new InvalidOperationException($"User '{user.Id}' was not found.");
        }

        _usersById[user.Id] = Clone(user)!;
        return Task.FromResult(Clone(user)!);
    }

    public Task<bool> UsernameExistsAsync(string username, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var exists = _usersById.Values.Any(user =>
            string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(user.Id, excludeUserId, StringComparison.Ordinal));

        return Task.FromResult(exists);
    }

    private static UserDbo? Clone(UserDbo? source)
    {
        if (source is null)
        {
            return null;
        }

        return new UserDbo
        {
            Id = source.Id,
            Auth0Subject = source.Auth0Subject,
            Username = source.Username,
            DisplayName = source.DisplayName,
            Email = source.Email,
            Profile = new UserProfileDbo
            {
                Bio = source.Profile.Bio,
                Image = source.Profile.Image,
                Visibility = source.Profile.Visibility
            },
            SocialProfiles = source.SocialProfiles
                .Select(profile => new SocialProfileDbo
                {
                    Platform = profile.Platform,
                    Url = profile.Url
                })
                .ToList(),
            Added = source.Added,
            Edited = source.Edited
        };
    }
}
