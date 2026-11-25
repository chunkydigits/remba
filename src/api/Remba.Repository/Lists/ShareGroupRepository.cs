using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class ShareGroupRepository : IShareGroupRepository
{
    private readonly Container _container;
    private readonly ILogger<ShareGroupRepository> _logger;

    public ShareGroupRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<ShareGroupRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.UserLists);
    }

    public async Task<ShareGroupDbo?> GetByIdAsync(
        string ownerId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId) || StringUtilities.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<ShareGroupDbo>(
                    groupId,
                    new PartitionKey(ownerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == ListRecordTypes.ShareGroup ? response.Resource : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ShareGroupDbo>> GetByOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId))
        {
            return [];
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.ownerId = @ownerId
              AND c.recordType = @recordType
            ORDER BY c.name ASC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", ownerId)
            .WithParameter("@recordType", ListRecordTypes.ShareGroup);

        var results = new List<ShareGroupDbo>();
        using var iterator = _container.GetItemQueryIterator<ShareGroupDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<ShareGroupDbo> CreateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(group);

        var response = await _container
            .CreateItemAsync(group, new PartitionKey(group.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<ShareGroupDbo> UpdateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(group);

        var response = await _container
            .ReplaceItemAsync(
                group,
                group.Id,
                new PartitionKey(group.OwnerId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task DeleteAsync(string ownerId, string groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container
                .DeleteItemAsync<ShareGroupDbo>(
                    groupId,
                    new PartitionKey(ownerId),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Share group {GroupId} for owner {OwnerId} was already missing during delete",
                groupId,
                ownerId);
        }
    }

    private static void ValidateForWrite(ShareGroupDbo group)
    {
        if (StringUtilities.IsNullOrWhiteSpace(group.Id))
        {
            throw new ArgumentException("Group id is required.", nameof(group));
        }

        if (StringUtilities.IsNullOrWhiteSpace(group.OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(group));
        }

        if (StringUtilities.IsNullOrWhiteSpace(group.Name))
        {
            throw new ArgumentException("Group name is required.", nameof(group));
        }

        if (!string.Equals(group.RecordType, ListRecordTypes.ShareGroup, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be share-group.", nameof(group));
        }
    }
}
