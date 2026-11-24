namespace Remba.Model.Configuration;

public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "remba";

    /// <summary>
    /// Shared RU/s for the database. Containers inherit this instead of each taking 400 RU/s.
    /// Keep at or below the account total throughput limit (often 1000 on small accounts).
    /// </summary>
    public int DatabaseThroughput { get; set; } = 400;

    public CosmosContainersOptions Containers { get; set; } = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Key);
}

public class CosmosContainersOptions
{
    public string Users { get; set; } = "users";

    public string UserLists { get; set; } = "user-lists";

    public string Entities { get; set; } = "entities";

    public string System { get; set; } = "system";
}
