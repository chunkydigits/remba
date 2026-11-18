using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Users;
using Remba.Utility;

namespace Remba.Repository.Users;

public class UserRepository : IUserRepository
{
    private readonly Container _container;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<UserRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.Users);
    }

    public async Task<UserDbo?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<UserDbo>(userId, new PartitionKey(userId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserDbo?> GetByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(auth0Subject))
        {
            return null;
        }

        const string queryText = "SELECT * FROM c WHERE c.auth0Subject = @auth0Subject";
        var query = new QueryDefinition(queryText).WithParameter("@auth0Subject", auth0Subject);

        using var iterator = _container.GetItemQueryIterator<UserDbo>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var match = page.Resource.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<UserDbo> CreateAsync(UserDbo user, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(user);

        try
        {
            var response = await _container
                .CreateItemAsync(user, new PartitionKey(user.Id), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(ex, "Conflict creating user {UserId}", user.Id);
            throw new InvalidOperationException($"User '{user.Id}' already exists.", ex);
        }
    }

    public async Task<UserDbo> UpdateAsync(UserDbo user, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(user);

        var response = await _container
            .ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<bool> UsernameExistsAsync(string username, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        const string queryText = """
            SELECT VALUE COUNT(1)
            FROM c
            WHERE LOWER(c.username) = @username
              AND (@excludeUserId = null OR c.id != @excludeUserId)
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@username", username.Trim().ToLowerInvariant())
            .WithParameter("@excludeUserId", excludeUserId);

        using var iterator = _container.GetItemQueryIterator<int>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            if (page.Resource.FirstOrDefault() > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateForWrite(UserDbo user)
    {
        if (StringUtilities.IsNullOrWhiteSpace(user.Id))
        {
            throw new ArgumentException("User id is required.", nameof(user));
        }

        if (StringUtilities.IsNullOrWhiteSpace(user.Auth0Subject))
        {
            throw new ArgumentException("Auth0 subject is required.", nameof(user));
        }

        if (StringUtilities.IsNullOrWhiteSpace(user.Username))
        {
            throw new ArgumentException("Username is required.", nameof(user));
        }
    }
}
