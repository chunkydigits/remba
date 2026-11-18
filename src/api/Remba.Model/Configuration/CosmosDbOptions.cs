namespace Remba.Model.Configuration;

public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "remba";

    public CosmosContainersOptions Containers { get; set; } = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Key);
}

public class CosmosContainersOptions
{
    public string Users { get; set; } = "users";

    public string UserLists { get; set; } = "user-lists";

    public string Entities { get; set; } = "entities";
}
