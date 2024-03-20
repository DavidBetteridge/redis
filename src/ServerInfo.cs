namespace codecrafters_redis;

public class ServerInfo
{
    public string Role { get; set; } = "master";

    public string MasterReplid { get; set; } = default!;
    
    public int MasterReplOffset { get; set; }
}
