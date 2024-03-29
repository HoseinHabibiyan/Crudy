using System.Dynamic;

namespace Crudy.Documents;

public abstract class BaseDataDocument
{
    public string Id { get; set; } = default!;
    public required string TokenId { get; set; }
    public required string Route { get; set; }
}

public class DataDocument : BaseDataDocument
{
    public required Dictionary<string, object> Data { get; set; }
}

public class QueryDataDocument : BaseDataDocument
{
    public ExpandoObject Data { get; set; }
}