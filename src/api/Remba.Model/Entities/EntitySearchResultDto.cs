namespace Remba.Model.Entities;

public class EntitySearchResultDto
{
    public string Provider { get; set; } = string.Empty;

    public string IdentifierType { get; set; } = string.Empty;

    public string IdentifierValue { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Year { get; set; }

    public string? Subtitle { get; set; }

    public string? Author { get; set; }

    public string? Director { get; set; }

    public string? Actors { get; set; }

    public string? ImageUrl { get; set; }

    public string? Link { get; set; }
}

public class EntityIdentifierRequest
{
    public string Type { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
