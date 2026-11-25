using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class ListShareRepository : IListShareRepository
{
    private readonly Container _container;
    private readonly ILogger<ListShareRepository> _logger;

    public ListShareRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<ListShareRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.UserLists);
    }

    public async Task<ListShareDbo?> GetAsync(
        string partitionOwnerId,
        string shareId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(partitionOwnerId) || StringUtilities.IsNullOrWhiteSpace(shareId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<ListShareDbo>(
                    shareId,
                    new PartitionKey(partitionOwnerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == ListRecordTypes.ListShare ? response.Resource : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ListShareDbo?> GetForMemberAsync(
        string memberUserId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(memberUserId) || StringUtilities.IsNullOrWhiteSpace(listId))
        {
            return null;
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.ownerId = @ownerId
              AND c.recordType = @recordType
              AND c.listId = @listId
              AND c.memberUserId = @memberUserId
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", memberUserId)
            .WithParameter("@recordType", ListRecordTypes.ListShare)
            .WithParameter("@listId", listId)
            .WithParameter("@memberUserId", memberUserId);

        using var iterator = _container.GetItemQueryIterator<ListShareDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(memberUserId) });

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

    public async Task<IReadOnlyList<ListShareDbo>> GetByListAsync(
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
            ORDER BY c.added ASC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", listOwnerId)
            .WithParameter("@recordType", ListRecordTypes.ListShare)
            .WithParameter("@listId", listId);

        var results = new List<ListShareDbo>();
        using var iterator = _container.GetItemQueryIterator<ListShareDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(listOwnerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<IReadOnlyList<ListShareDbo>> GetByMemberAsync(
        string memberUserId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(memberUserId))
        {
            return [];
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.ownerId = @ownerId
              AND c.recordType = @recordType
              AND c.memberUserId = @memberUserId
            ORDER BY c.added DESC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", memberUserId)
            .WithParameter("@recordType", ListRecordTypes.ListShare)
            .WithParameter("@memberUserId", memberUserId);

        var results = new List<ListShareDbo>();
        using var iterator = _container.GetItemQueryIterator<ListShareDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(memberUserId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task UpsertAsync(ListShareDbo share, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(share);

        await _container
            .UpsertItemAsync(share, new PartitionKey(share.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string partitionOwnerId,
        string shareId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _container
                .DeleteItemAsync<ListShareDbo>(
                    shareId,
                    new PartitionKey(partitionOwnerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "List share {ShareId} in partition {PartitionOwnerId} was already missing during delete",
                shareId,
                partitionOwnerId);
        }
    }

    private static void ValidateForWrite(ListShareDbo share)
    {
        if (StringUtilities.IsNullOrWhiteSpace(share.Id))
        {
            throw new ArgumentException("Share id is required.", nameof(share));
        }

        if (StringUtilities.IsNullOrWhiteSpace(share.OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(share));
        }

        if (StringUtilities.IsNullOrWhiteSpace(share.ListId))
        {
            throw new ArgumentException("List id is required.", nameof(share));
        }

        if (StringUtilities.IsNullOrWhiteSpace(share.MemberUserId))
        {
            throw new ArgumentException("Member user id is required.", nameof(share));
        }

        if (!string.Equals(share.RecordType, ListRecordTypes.ListShare, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be list-share.", nameof(share));
        }
    }
}
