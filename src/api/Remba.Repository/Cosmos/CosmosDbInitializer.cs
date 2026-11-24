using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;

namespace Remba.Repository.Cosmos;

/// <summary>
/// Ensures the Remba Cosmos database and core containers exist.
/// Uses shared database throughput so containers do not each reserve 400 RU/s.
/// </summary>
public class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Ensuring Cosmos database '{DatabaseName}' exists at {Endpoint} (shared throughput {Throughput} RU/s)",
            _options.DatabaseName,
            _options.Endpoint,
            _options.DatabaseThroughput);

        try
        {
            var database = await EnsureSharedThroughputDatabaseAsync(cancellationToken)
                .ConfigureAwait(false);

            await EnsureContainerAsync(database, _options.Containers.Users, "/id", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.UserLists, "/ownerId", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.Entities, "/id", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.System, "/id", cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Cosmos database and containers are ready.");
        }
        catch (CosmosException ex) when (ex.StatusCode == global::System.Net.HttpStatusCode.BadRequest && ex.SubStatusCode == 1028)
        {
            throw new InvalidOperationException(
                "Cosmos account throughput limit exceeded while creating resources. " +
                "Delete the 'remba' database in Azure Data Explorer, set CosmosDb__RepairSharedThroughput=true, " +
                "and restart so it can be recreated with shared database throughput. See https://aka.ms/cosmos-tp-limit",
                ex);
        }
    }

    private async Task<Database> EnsureSharedThroughputDatabaseAsync(CancellationToken cancellationToken)
    {
        var database = _cosmosClient.GetDatabase(_options.DatabaseName);
        var exists = await DatabaseExistsAsync(database, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            var created = await CreateDatabaseWithSharedThroughputAsync(cancellationToken)
                .ConfigureAwait(false);
            return created.Database;
        }

        var sharedThroughput = await TryReadDatabaseThroughputAsync(database, cancellationToken)
            .ConfigureAwait(false);
        if (sharedThroughput is not null)
        {
            _logger.LogInformation(
                "Cosmos database '{DatabaseName}' already has shared throughput {Throughput} RU/s",
                _options.DatabaseName,
                sharedThroughput);
            return database;
        }

        var containerIds = await ListContainerIdsAsync(database, cancellationToken).ConfigureAwait(false);

        if (!_options.RepairSharedThroughput)
        {
            throw new InvalidOperationException(
                $"Cosmos database '{_options.DatabaseName}' has containers [{string.Join(", ", containerIds)}] " +
                "without shared database throughput, so each new container reserves 400 RU/s and hits the account cap. " +
                "Delete the database in Azure, or set CosmosDb__RepairSharedThroughput=true to recreate it automatically.");
        }

        _logger.LogWarning(
            "Cosmos database '{DatabaseName}' has no shared throughput (containers: [{Containers}]). " +
            "Deleting and recreating with shared {Throughput} RU/s because RepairSharedThroughput is enabled",
            _options.DatabaseName,
            string.Join(", ", containerIds),
            _options.DatabaseThroughput);

        await database.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var recreated = await CreateDatabaseWithSharedThroughputAsync(cancellationToken)
            .ConfigureAwait(false);
        return recreated.Database;
    }

    private static async Task<bool> DatabaseExistsAsync(Database database, CancellationToken cancellationToken)
    {
        try
        {
            await database.ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private Task<DatabaseResponse> CreateDatabaseWithSharedThroughputAsync(CancellationToken cancellationToken)
    {
        var throughput = _options.DatabaseThroughput > 0 ? _options.DatabaseThroughput : 400;
        return _cosmosClient.CreateDatabaseIfNotExistsAsync(
            _options.DatabaseName,
            throughput: throughput,
            cancellationToken: cancellationToken);
    }

    private static async Task<int?> TryReadDatabaseThroughputAsync(
        Database database,
        CancellationToken cancellationToken)
    {
        try
        {
            return await database.ReadThroughputAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<string>> ListContainerIdsAsync(
        Database database,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        using var iterator = database.GetContainerQueryIterator<ContainerProperties>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var container in page)
            {
                if (!string.IsNullOrWhiteSpace(container.Id))
                {
                    ids.Add(container.Id);
                }
            }
        }

        return ids;
    }

    private async Task EnsureContainerAsync(
        Database database,
        string containerName,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        var container = database.GetContainer(containerName);
        try
        {
            await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Cosmos container '{Container}' already exists (partition key {PartitionKey})",
                containerName,
                partitionKeyPath);
            return;
        }
        catch (CosmosException ex) when (ex.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            // Create below.
        }

        // No per-container throughput: containers share the database RU/s.
        var response = await database
            .CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = containerName,
                    PartitionKeyPath = partitionKeyPath
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Cosmos container '{Container}' ready (partition key {PartitionKey}, status {StatusCode})",
            containerName,
            partitionKeyPath,
            response.StatusCode);
    }
}
