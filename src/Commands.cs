namespace codecrafters_redis;

public record Command;

public record Ping : Command;

public record Echo : Command
{
    public string Message { get; init; } = default!;
}

public record Get : Command
{
    public string Key { get; init; } = default!;
}

public record Set : Command
{
    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
    public int? Px { get; init; }
}