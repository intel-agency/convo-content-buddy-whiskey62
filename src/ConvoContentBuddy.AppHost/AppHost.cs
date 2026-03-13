var builder = DistributedApplication.CreateBuilder(args);

var geminiApiKey = builder.AddParameter("GEMINI_API_KEY", secret: true);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithBindMount("../../docker/init-scripts/postgres", "/docker-entrypoint-initdb.d");

var db = postgres.AddDatabase("convocontentbuddy");

var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.ConvoContentBuddy_API_Brain>("api-brain")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("GEMINI_API_KEY", geminiApiKey)
    .WithEnvironment("Embedding__ModelName", "gemini-embedding-001")
    .WithEnvironment("Embedding__Dimensions", "1536");

builder.AddProject<Projects.ConvoContentBuddy_Data_Seeder>("data-seeder")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("GEMINI_API_KEY", geminiApiKey)
    .WithEnvironment("Embedding__ModelName", "gemini-embedding-001")
    .WithEnvironment("Embedding__Dimensions", "1536");

builder.AddProject<Projects.ConvoContentBuddy_UI_Web>("ui-web")
    .WithReference(api);

builder.Build().Run();
