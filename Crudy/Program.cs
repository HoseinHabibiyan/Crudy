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
    policy.AddPolicy("CORSPolicy",
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

app.UseCors("CORSPolicy");

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



app.MapPost("/{route}", async (string route, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync();
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

    var ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress;

    var document = new DataDocument
    {
        Id = id,
        Route = route.ToLower().Trim(),
        Data = dic,
        IPAddress = ipAddress?.ToString() ?? string.Empty,
        UserId = context.Request.HttpContext.GetUserId(),
    };

    await session.StoreAsync(document, cancellationToken);
    await session.SaveChangesAsync(cancellationToken);
    return id;
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapGet("/{route}/{page}/{pageSize}", async (string route, int page, int pageSize, IAsyncDocumentSession session, HttpContext context, CancellationToken cancellationToken) =>
{
    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<QueryDataDocument>(collectionName: "DataDocuments")
                       .Where(x => x.Route == route.ToLower().Trim())
                       .AsQueryable();

    if (userId is not null)
    {
        query = query.Where(x => x.UserId == userId);
    }
    else
    {
        string? ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        query = query.Where(x => x.IPAddress == ipAddress);
    }

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

app.MapGet("/{route}/{id}", async Task<Results<Ok<ExpandoObject>, NotFound>> (string route, string id, IAsyncDocumentSession session, HttpContext context) =>
{
    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<QueryDataDocument>(collectionName: "DataDocuments")
                       .Where(x => x.Route == route.ToLower().Trim() && x.Id == id)
                       .AsQueryable();

    if (userId is not null)
    {
        query = query.Where(x => x.UserId == userId);
    }
    else
    {
        string? ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        query = query.Where(x => x.IPAddress == ipAddress);
    }

    var item = await query.FirstOrDefaultAsync();

    return item is null ? TypedResults.NotFound() : TypedResults.Ok(item.Data);
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapPut("/{route}/{id}", async Task<Results<Ok, NotFound>> (string route, string id, HttpContext context, IAsyncDocumentSession session) =>
{
    string input = default!;

    using (var bodyReader = new StreamReader(context.Request.Body))
    {
        input = await bodyReader.ReadToEndAsync();
    }

    int inputSize = input.Length * sizeof(Char);

    if (inputSize > 50000)
        throw new BadRequestException("Input size is too large");

    if (!input.IsJsonValid())
        throw new BadRequestException("input body is not valid");

    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<DataDocument>()
                       .Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id)
                       .AsQueryable();

    if (userId is not null)
    {
        query = query.Where(x => x.UserId == userId);
    }
    else
    {
        string? ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        query = query.Where(x => x.IPAddress == ipAddress);
    }

    var item = await query.FirstOrDefaultAsync();

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
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.MapDelete("/{route}/{id}", async Task<Results<Ok, NotFound>> (string id, string route, IAsyncDocumentSession session, HttpContext context) =>
{
    string? userId = context.Request.HttpContext.GetUserId();

    var query = session.Query<DataDocument>()
                       .Where(x => x.Route == route.ToLower().Trim() && x.Data["_id"].ToString() == id)
                       .AsQueryable();

    if (userId is not null)
    {
        query = query.Where(x => x.UserId == userId);
    }
    else
    {
        string? ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        query = query.Where(x => x.IPAddress == ipAddress);
    }

    var item = await query.FirstOrDefaultAsync();

    if (item is null) return TypedResults.NotFound();

    session.Delete(item);
    await session.SaveChangesAsync();

    return TypedResults.Ok();
})
.WithOpenApi()
.RequireRateLimiting(rateLimitPolicy);

app.Run();


