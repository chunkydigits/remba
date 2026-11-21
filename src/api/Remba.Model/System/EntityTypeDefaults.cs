using Remba.Model.Entities;

namespace Remba.Model.System;

public static class EntityTypeDefaults
{
    public static IReadOnlyList<EntityTypeDbo> CreateAll(DateTime utcNow) =>
    [
        Create(utcNow, EntityTypes.Film, "Film", supportsLookup: true),
        Create(utcNow, EntityTypes.Tv, "TV", supportsLookup: true),
        Create(utcNow, EntityTypes.Book, "Books", supportsLookup: true),
        Create(utcNow, EntityTypes.Recipe, "Recipes", supportsLookup: false),
        Create(utcNow, EntityTypes.Country, "Countries", supportsLookup: false)
    ];

    private static EntityTypeDbo Create(DateTime utcNow, string key, string name, bool supportsLookup) =>
        new()
        {
            Id = BuildSystemId(key),
            RecordType = SystemRecordTypes.EntityType,
            Key = key,
            Name = name,
            Visibility = EntityTypeVisibility.System,
            OwnerId = null,
            SupportsLookup = supportsLookup,
            Added = utcNow,
            Edited = null
        };

    public static string BuildSystemId(string key) => $"entity-type:{key}";
}
