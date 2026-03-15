using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Seeder;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("convocontentbuddy",
    configureDbContextOptions: options =>
        options.UseNpgsql(npgsql => npgsql.UseVector()));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
