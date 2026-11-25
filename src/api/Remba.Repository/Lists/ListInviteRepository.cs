using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class ListInviteRepository : IListInviteRepository
{
    private readonly Container _container;
    private readonly ILogger<ListInviteRepository> _logger;

    public ListInviteRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<ListInviteRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.UserLists);
    }

    public async Task<ListInviteDbo?> GetByIdAsync(
        string listOwnerId,
        string inviteId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(listOwnerId) || StringUtilities.IsNullOrWhiteSpace(inviteId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<ListInviteDbo>(
                    inviteId,
                    new PartitionKey(listOwnerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == ListRecordTypes.ListInvite ? response.Resource : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ListInviteDbo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.recordType = @recordType
              AND c.code = @code
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@recordType", ListRecordTypes.ListInvite)
            .WithParameter("@code", code.Trim().ToUpperInvariant());

        using var iterator = _container.GetItemQueryIterator<ListInviteDbo>(query);
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

    public async Task<IReadOnlyList<ListInviteDbo>> GetByListAsync(
        string listOwnerId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(listOwnerId) || StringUtilities.IsNullOrWhiteSpace(listId))
        {
            return [];
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.ownerId = @ownerId
              AND c.recordType = @recordType
              AND c.listId = @listId
            ORDER BY c.added DESC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", listOwnerId)
            .WithParameter("@recordType", ListRecordTypes.ListInvite)
            .WithParameter("@listId", listId);

        var results = new List<ListInviteDbo>();
        using var iterator = _container.GetItemQueryIterator<ListInviteDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(listOwnerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<ListInviteDbo> CreateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(invite);

        var response = await _container
            .CreateItemAsync(invite, new PartitionKey(invite.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<ListInviteDbo> UpdateAsync(ListInviteDbo invite, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(invite);

        var response = await _container
            .ReplaceItemAsync(
                invite,
                invite.Id,
                new PartitionKey(invite.OwnerId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task DeleteAsync(
        string listOwnerId,
        string inviteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _container
                .DeleteItemAsync<ListInviteDbo>(
                    inviteId,
                    new PartitionKey(listOwnerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "List invite {InviteId} for owner {OwnerId} was already missing during delete",
                inviteId,
                listOwnerId);
        }
    }

    private static void ValidateForWrite(ListInviteDbo invite)
    {
        if (StringUtilities.IsNullOrWhiteSpace(invite.Id))
        {
            throw new ArgumentException("Invite id is required.", nameof(invite));
        }

        if (StringUtilities.IsNullOrWhiteSpace(invite.OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(invite));
        }

        if (StringUtilities.IsNullOrWhiteSpace(invite.Code))
        {
            throw new ArgumentException("Invite code is required.", nameof(invite));
        }

        if (!string.Equals(invite.RecordType, ListRecordTypes.ListInvite, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be list-invite.", nameof(invite));
        }
    }
}
