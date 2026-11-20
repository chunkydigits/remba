using System.ComponentModel.DataAnnotations;

namespace Remba.Model.System;

public class EntityTypeDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Visibility { get; set; } = EntityTypeVisibility.Private;

    public bool SupportsLookup { get; set; }

    public DateTime Added { get; set; }

    public DateTime? Edited { get; set; }
}

public class CreateEntityTypeRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;
}
