using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Remba.Model.Entities;

namespace Remba.Model.Lists;

public class EntryDto
{
    public string Id { get; set; } = string.Empty;

    public string ListId { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? IdentifierType { get; set; }

    public string? IdentifierValue { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Link { get; set; }

    public string? Author { get; set; }

    public string? Director { get; set; }

    public string? Actors { get; set; }

    public bool? Read { get; set; }

    public int Order { get; set; }

    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.Ordinal);

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class CreateEntryRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public EntityIdentifierRequest? Identifier { get; set; }

    [MaxLength(2000)]
    public string? Link { get; set; }

    [MaxLength(400)]
    public string? Author { get; set; }

    [MaxLength(400)]
    public string? Director { get; set; }

    [MaxLength(800)]
    public string? Actors { get; set; }

    public bool? Read { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public Dictionary<string, JsonElement>? Attributes { get; set; }
}

public class UpdateEntryRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Link { get; set; }

    [MaxLength(400)]
    public string? Author { get; set; }

    [MaxLength(400)]
    public string? Director { get; set; }

    [MaxLength(800)]
    public string? Actors { get; set; }

    public bool? Read { get; set; }

    public Dictionary<string, JsonElement>? Attributes { get; set; }
}

public class EntryQuery
{
    public string? Q { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public string? Author { get; set; }

    public string? Director { get; set; }

    public string? Actors { get; set; }

    /// <summary>
    /// true / false / null (all)
    /// </summary>
    public bool? Read { get; set; }
}

public class ReorderEntriesRequest
{
    [Required]
    [MinLength(1)]
    public List<string> EntryIds { get; set; } = [];
}

public static class EntrySortFields
{
    public const string Order = "order";

    public const string Name = "name";

    public const string Author = "author";

    public const string Director = "director";

    public const string Actors = "actors";

    public const string Added = "added";

    public const string Read = "read";
}
