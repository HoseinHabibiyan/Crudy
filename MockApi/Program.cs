using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using MockApi.Util;
using Raven.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRavenDbAsyncSession();
builder.Services.AddRavenDbDocStore();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.UseAuthentication();
//app.UseAuthorization();


app.MapPost("/{route}", async (string route, IAsyncDocumentSession session, HttpContext context) =>
{
    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync();
    }

    if (!input.IsJsonValid())
        throw new BadRequestException("Json is not valid");

    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);

    if (data is null)
        throw new BadRequestException($"input body is not valid");

    string id = Guid.NewGuid().ToString();

    var dic = new Dictionary<string, object>()
    {
        {"_id", id}
    };

    dic = dic.Union(data).ToDictionary();

    var document = new Document
    {
        Route = route.ToLower().Trim(),
        Data = dic,
    };

    await session.StoreAsync(document);
    await session.SaveChangesAsync();

    return id;
})
.WithOpenApi();

app.MapGet("/{route}", async (string route, IAsyncDocumentSession session) =>
{
    var model = await session.Query<Document>()
                             .Where(x => x.Route == route.ToLower().Trim())
                             .Select(x => x.Data).ToListAsync();
    return model;
})
.WithOpenApi();

app.MapGet("/{route}/{id}", async Task<Results<Ok<Dictionary<string, object>>, NotFound>> (string route, string id, IAsyncDocumentSession session) =>
{
    var item = await session.Query<Document>().Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id).FirstOrDefaultAsync();

    return item is null ? TypedResults.NotFound() : TypedResults.Ok(item.Data);
})
.WithOpenApi();

app.MapPut("/{route}/{id}", async Task<Results<Ok, NotFound>> (string route, string id, HttpContext context, IAsyncDocumentSession session) =>
{
    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync();
    }

    if (!input.IsJsonValid())
        throw new BadRequestException("input body is not valid");

    var item = await session.Query<Document>().Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id).FirstOrDefaultAsync();

    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);

    if (data is null)
        throw new BadRequestException($"input body is not valid!");

    if (item is null) return TypedResults.NotFound();

    data.ToList().ForEach(element =>
    {
        item.Data[element.Key] = element.Value;
    });

    await session.StoreAsync(item);
    await session.SaveChangesAsync();

    return TypedResults.Ok();
})
.WithOpenApi();

app.MapDelete("/{route}/{id}", async Task<Results<Ok, NotFound>> (string id, string route, IAsyncDocumentSession session) =>
{
    var item = await session.Query<Document>().Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id).FirstOrDefaultAsync();

    if (item is null) return TypedResults.NotFound();

    session.Delete(item);
    await session.SaveChangesAsync();

    return TypedResults.Ok();
})
.WithOpenApi();

app.Run();

class Document
{
    public string Id { get; set; } = default!;
    public int UserId { get; set; }
    public required string Route { get; set; }
    public required Dictionary<string, object> Data { get; set; }
}


