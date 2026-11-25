using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Users;
using Remba.Repository.Users;
using Remba.Utility;

namespace Remba.Service.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly UserCacheOptions _cacheOptions;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IMemoryCache memoryCache,
        IOptions<UserCacheOptions> cacheOptions,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserDto> EnsureCurrentUserAsync(
        string auth0Subject,
        string? preferredDisplayName,
        string? preferredUsernameSeed,
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(auth0Subject))
        {
            throw new ArgumentException("Auth0 subject is required.", nameof(auth0Subject));
        }

        var normalisedEmail = StringUtilities.NormalizeWhitespace(email).ToLowerInvariant();

        var cacheKey = BuildAuth0CacheKey(auth0Subject);
        if (_memoryCache.TryGetValue(cacheKey, out UserDto? cached)
            && cached is not null
            && (string.IsNullOrWhiteSpace(normalisedEmail)
                || string.Equals(cached.Email, normalisedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            return cached;
        }

        var existing = await _userRepository
            .GetByAuth0SubjectAsync(auth0Subject, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(normalisedEmail)
                && !string.Equals(existing.Email, normalisedEmail, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = normalisedEmail;
                existing.Edited = DateTime.UtcNow;
                existing = await _userRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            }

            var dto = MapToDto(existing);
            CacheUser(auth0Subject, dto);
            return dto;
        }

        var username = await CreateUniqueUsernameAsync(preferredUsernameSeed, auth0Subject, cancellationToken)
            .ConfigureAwait(false);

        var displayName = StringUtilities.NormalizeWhitespace(preferredDisplayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = username;
        }

        var now = DateTime.UtcNow;
        var created = await _userRepository.CreateAsync(
                new UserDbo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Auth0Subject = auth0Subject,
                    Username = username,
                    DisplayName = displayName,
                    Email = normalisedEmail,
                    Profile = new UserProfileDbo
                    {
                        Visibility = ProfileVisibility.Private
                    },
                    SocialProfiles = [],
                    Added = now,
                    Edited = null
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Created Remba user {UserId} for Auth0 subject mapping", created.Id);

        var createdDto = MapToDto(created);
        CacheUser(auth0Subject, createdDto);
        return createdDto;
    }

    public async Task<UserDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var user = await _userRepository
            .GetByUsernameAsync(username.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> UpdateCurrentUserAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("User was not found.");

        if (request.DisplayName is not null)
        {
            var displayName = StringUtilities.NormalizeWhitespace(request.DisplayName);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name cannot be empty.");
            }

            user.DisplayName = displayName;
        }

        if (request.Username is not null)
        {
            var username = request.Username.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty.");
            }

            if (!string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _userRepository
                    .UsernameExistsAsync(username, user.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    throw new InvalidOperationException("Username is already taken.");
                }

                user.Username = username;
            }
        }

        if (request.Profile is not null)
        {
            if (request.Profile.Bio is not null)
            {
                user.Profile.Bio = request.Profile.Bio.Trim();
            }

            if (request.Profile.Image is not null)
            {
                user.Profile.Image = request.Profile.Image.Trim();
            }

            if (request.Profile.Visibility is not null)
            {
                var visibility = request.Profile.Visibility.Trim().ToLowerInvariant();
                if (visibility is not ProfileVisibility.Private and not ProfileVisibility.Public)
                {
                    throw new ArgumentException("Profile visibility must be 'private' or 'public'.");
                }

                user.Profile.Visibility = visibility;
            }
        }

        if (request.SocialProfiles is not null)
        {
            user.SocialProfiles = request.SocialProfiles
                .Select(profile => new SocialProfileDbo
                {
                    Platform = profile.Platform.Trim().ToLowerInvariant(),
                    Url = profile.Url.Trim()
                })
                .ToList();
        }

        user.Edited = DateTime.UtcNow;

        var updated = await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        var dto = MapToDto(updated);
        CacheUser(updated.Auth0Subject, dto);
        return dto;
    }

    private async Task<string> CreateUniqueUsernameAsync(
        string? preferredUsernameSeed,
        string auth0Subject,
        CancellationToken cancellationToken)
    {
        var candidate = UsernameUtilities.CreateProvisionalUsername(preferredUsernameSeed, auth0Subject);
        var exists = await _userRepository.UsernameExistsAsync(candidate, null, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return candidate;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            candidate = UsernameUtilities.CreateProvisionalUsername($"{preferredUsernameSeed}{attempt}", auth0Subject);
            exists = await _userRepository.UsernameExistsAsync(candidate, null, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return candidate;
            }
        }

        return UsernameUtilities.CreateProvisionalUsername(Guid.NewGuid().ToString("N"), auth0Subject);
    }

    private void CacheUser(string auth0Subject, UserDto dto)
    {
        _memoryCache.Set(
            BuildAuth0CacheKey(auth0Subject),
            dto,
            TimeSpan.FromSeconds(Math.Max(30, _cacheOptions.AbsoluteExpirationSeconds)));
    }

    private static string BuildAuth0CacheKey(string auth0Subject) => $"user:auth0:{auth0Subject}";

    private static UserDto MapToDto(UserDbo user) =>
        new()
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Profile = new UserProfileDto
            {
                Bio = user.Profile.Bio,
                Image = user.Profile.Image,
                Visibility = user.Profile.Visibility
            },
            SocialProfiles = user.SocialProfiles
                .Select(profile => new SocialProfileDto
                {
                    Platform = profile.Platform,
                    Url = profile.Url
                })
                .ToList(),
            Added = user.Added,
            Edited = user.Edited
        };
}
