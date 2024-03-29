using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using Crudy.Util;
using Raven.DependencyInjection;
using Crudy.Identity;
using Crudy.Identity.Models;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Crudy.Documents;
using Microsoft.AspNetCore.RateLimiting;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();

#region Jwt Authentication
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddSwaggerGen(c =>
{
    c.ResolveConflictingActions(x => x.First());
    c.SchemaGeneratorOptions = new SchemaGeneratorOptions { SchemaIdSelector = type => type.FullName };
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CRUDY", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new string[] { }
                        }
                    });
});
#endregion

#region Rate Limiter

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("Fixed", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(10);
        opt.PermitLimit = 100;
    });
});

const string rateLimitPolicy = "Fixed";

#endregion

#region CORS

builder.Services.AddCors(policy =>
{
    policy.AddPolicy("ApiCORSPolicy",
        builder => builder
         .AllowAnyMethod()
         .AllowAnyHeader()
         .SetIsOriginAllowed((host) => true)
         .AllowCredentials()
         );
});

#endregion

builder.Services.AddRavenDbAsyncSession();
builder.Services.AddRavenDbDocStore();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CRUDY");
    });
}

app.UseCors("ApiCORSPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();


#region Identity

app.MapPost("/Login", (LoginModel model, AuthService authService, HttpContext context, CancellationToken cancellationToken) =>
{
    return authService.Login(model, cancellationToken);
})
.WithOpenApi();

app.MapPost("/Register", (RegisterModel model, AuthService authService, CancellationToken cancellationToken) =>
{
    return authService.Register(model, cancellationToken);
})
.WithOpenApi();

app.MapPost("/Change-password", (ChangePasswordModel model, AuthService authService, CancellationToken cancellationToken) =>
{
    return authService.ChangePassword(model, cancellationToken);
})
.WithOpenApi()
.RequireAuthorization();

#endregion

app.MapGet("/api/generate-token", async Task<Results<Ok<string>, NotFound>> (IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    string? userId = context.GetUserId();
    bool tokenAlreadyExist = await session.Query<TokenDocument>().Where(x => x.UserId == userId).AnyAsync(cancellationToken);

    if (tokenAlreadyExist)
        throw new BadRequestException("Your token already created");

    var ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress;
    var token = new TokenDocument()
    {
        UserId = userId,
        IPAddress = ipAddress.ToString(),
        Token = Guid.NewGuid().ToString("N"),
        ExpirationDate = null
    };
    await session.StoreAsync(token, cancellationToken);
    await session.SaveChangesAsync(cancellationToken);

    return TypedResults.Ok(token.Token);
})
.WithOpenApi()
.RequireAuthorization()
.RequireRateLimiting(rateLimitPolicy);

app.MapPost("/api/{token}/{route}", async (string token, string route, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    if (!token.IsValidGuid())
        throw new BadHttpRequestException("Token is not Valid");

    var tokenDoc = await session.Query<TokenDocument>().Where(x => x.Token == token).FirstOrDefaultAsync(cancellationToken);

    if (tokenDoc is null)
    {
        var ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress;
        if (ipAddress is null)
            throw new UnauthorizedAccessException();

        tokenDoc = new TokenDocument()
        {
            IPAddress = ipAddress.ToString(),
            Token = token,
        };
        await session.StoreAsync(tokenDoc, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    if (tokenDoc.ExpirationDate < DateTimeOffset.Now)
        throw new UnauthorizedAccessException("Token is expired");

    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync(cancellationToken);
    }

    int inputSize = input.Length * sizeof(Char);

    if (inputSize > 50000)
        throw new BadRequestException("Input size is too large");

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


    var document = new DataDocument
    {
        Id = id,
        TokenId = tokenDoc.Id,
        Route = route.ToLower().Trim(),
        Data = dic,
    };

    await session.StoreAsync(document, cancellationToken);
    await session.SaveChangesAsync(cancellationToken);
    return id;
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapGet("/api/{token}/{route}/{page}/{pageSize}", async (string token, string route, int page, int pageSize, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    if (!token.IsValidGuid())
        throw new BadHttpRequestException("Token is not Valid");

    var tokenDoc = await session.Query<TokenDocument>().Where(x => x.Token == token).FirstOrDefaultAsync(cancellationToken);

    if (tokenDoc is null)
        throw new UnauthorizedAccessException("Token is not valid");

    if (tokenDoc.ExpirationDate < DateTimeOffset.Now)
        throw new Exception("Token is expired");


    var query = session.Query<QueryDataDocument>(collectionName: "DataDocuments")
                       .Where(x => x.Route == route.ToLower().Trim() && x.TokenId == tokenDoc.Id)
                       .AsQueryable();

    var model = await query.Select(x => x.Data)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync(cancellationToken);

    return new
    {
        Data = model,
        TotalCount = await query.CountAsync(cancellationToken)
    };
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapGet("/api/{token}/{route}/{id}", async Task<Results<Ok<ExpandoObject>, NotFound>> (string token, string route, string id, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    if (!token.IsValidGuid())
        throw new BadHttpRequestException("Token is not Valid");

    var tokenDoc = await session.Query<TokenDocument>().Where(x => x.Token == token).FirstOrDefaultAsync(cancellationToken);

    if (tokenDoc is null)
        throw new Exception("Token is not valid");

    if (tokenDoc.ExpirationDate > DateTimeOffset.Now)
        throw new Exception("Token is expired");

    var query = session.Query<QueryDataDocument>(collectionName: "DataDocuments")
                       .Where(x => x.Route == route.ToLower().Trim() && x.Id == id && x.TokenId == tokenDoc.Id)
                       .AsQueryable();

    var item = await query.FirstOrDefaultAsync(cancellationToken);

    return item is null ? TypedResults.NotFound() : TypedResults.Ok(item.Data);
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapPut("/api/{token}/{route}/{id}", async Task<Results<Ok, NotFound>> (string token, string route, string id, HttpContext context, IAsyncDocumentSession session, CancellationToken cancellationToken) =>
{
    if (!token.IsValidGuid())
        throw new BadHttpRequestException("Token is not Valid");

    var tokenDoc = await session.Query<TokenDocument>().Where(x => x.Token == token).FirstOrDefaultAsync(cancellationToken);

    if (tokenDoc is null)
        throw new Exception("Token is not valid");

    if (tokenDoc.ExpirationDate > DateTimeOffset.Now)
        throw new Exception("Token is expired");

    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync(cancellationToken);
    }

    int inputSize = input.Length * sizeof(Char);

    if (inputSize > 50000)
        throw new BadRequestException("Input size is too large");

    if (!input.IsJsonValid())
        throw new BadRequestException("input body is not valid");

    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<DataDocument>()
                       .Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id && x.TokenId == tokenDoc.Id)
                       .AsQueryable();

    var item = await query.FirstOrDefaultAsync(cancellationToken);

    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);

    if (data is null)
        throw new BadRequestException($"input body is not valid!");

    if (item is null) return TypedResults.NotFound();

    data.ToList().ForEach(element =>
    {
        item.Data[element.Key] = element.Value;
    });

    await session.StoreAsync(item, cancellationToken);
    await session.SaveChangesAsync(cancellationToken);

    return TypedResults.Ok();
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapDelete("/api/{token}/{route}/{id}", async Task<Results<Ok, NotFound>> (string token, string id, string route, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    if (!token.IsValidGuid())
        throw new BadHttpRequestException("Token is not Valid");

    var tokenDoc = await session.Query<TokenDocument>().Where(x => x.Token == token).FirstOrDefaultAsync(cancellationToken);

    if (tokenDoc is null)
        throw new Exception("Token is not valid");

    if (tokenDoc.ExpirationDate > DateTimeOffset.Now)
        throw new Exception("Token is expired");

    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<DataDocument>()
                       .Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id && x.TokenId == tokenDoc.Id)
                       .AsQueryable();

    var item = await query.FirstOrDefaultAsync(cancellationToken);

    if (item is null) return TypedResults.NotFound();

    session.Delete(item);
    await session.SaveChangesAsync(cancellationToken);

    return TypedResults.Ok();
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.Run();


