using ConvoContentBuddy.API.Brain.Endpoints;
using ConvoContentBuddy.API.Brain.Models;
using ConvoContentBuddy.API.Brain.Repositories;
using ConvoContentBuddy.API.Brain.Services;
using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("convocontentbuddy",
    configureDbContextOptions: options =>
        options.UseNpgsql(npgsql => npgsql.UseVector()));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "ConvoContentBuddy Brain API",
            Version = "v1",
            Description = "Real-time coding interview assistance API — semantic problem search powered by pgvector and Gemini embeddings"
        };
        return Task.CompletedTask;
    });
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<EmbeddingProfileOptions>(
    builder.Configuration.GetSection("EmbeddingProfile"));

builder.Services.PostConfigure<EmbeddingProfileOptions>(opts =>
{
    if (string.IsNullOrEmpty(opts.ApiKey) || opts.ApiKey.StartsWith("${"))
        opts.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? string.Empty;
});

builder.Services.AddScoped<IProblemRepository, ProblemRepository>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var opts = sp.GetRequiredService<IOptions<EmbeddingProfileOptions>>();
    return new GeminiEmbeddingGenerator(factory.CreateClient(), opts);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "Brain API v1"));
}

app.MapDefaultEndpoints();
app.MapProblemEndpoints();

app.Run();

/// <summary>Exposes the top-level Program type for <c>WebApplicationFactory</c> in test projects.</summary>
public partial class Program { }
