namespace Crudy.Documents;

public class DataDocument
{
    public string Id { get; set; } = default!;
    public string? UserId { get; set; }
    public required string IPAddress { get; set; }
    public required string Route { get; set; }
    public required Dictionary<string, object> Data { get; set; }
}


