public class DataDocument
{
    public string Id { get; set; } = default!;
    public int UserId { get; set; }
    public required string Route { get; set; }
    public required Dictionary<string, object> Data { get; set; }
}


