# ADR-0001: Use .NET Aspire for Service Orchestration

## Status
Accepted

## Context

ConvoContentBuddy requires orchestration of multiple microservices:
- ASP.NET Core Web API (with 3 replicas for TMR)
- Blazor WebAssembly client
- Worker service for data seeding
- External dependencies: Qdrant, PostgreSQL, Redis

We need a solution that provides:
- Single-command startup for local development
- Automatic service discovery and configuration
- Built-in health checks and observability
- Container lifecycle management
- Production-ready deployment manifests

## Decision

We will use **.NET Aspire 10** as our orchestration platform.

### Rationale

1. **Native .NET Integration**: Seamless integration with .NET 10 ecosystem
2. **Built-in Service Discovery**: Automatic configuration of service endpoints
3. **OpenTelemetry Integration**: Out-of-the-box distributed tracing and metrics
4. **Developer Experience**: Single `dotnet run` to start entire system
5. **Azure Integration**: Native support for Azure deployment via azd
6. **Health Checks**: Built-in health check endpoints and dashboard
7. **Container Management**: Automatic Docker container lifecycle management

### Alternatives Considered

1. **Docker Compose Only**
   - ✅ Simple and widely understood
   - ❌ No service discovery
   - ❌ Manual health check configuration
   - ❌ No automatic Azure deployment

2. **Kubernetes**
   - ✅ Production-grade orchestration
   - ❌ Overkill for current scale
   - ❌ Complex local development setup
   - ❌ Steep learning curve

3. **Dapr**
   - ✅ Service invocation and pub/sub
   - ❌ Additional complexity
   - ❌ Not needed for current requirements

## Consequences

### Positive
- **Rapid Development**: Single command starts entire system
- **Observability**: Automatic OpenTelemetry integration
- **Cloud Parity**: Local development mirrors production
- **TMR Support**: Easy configuration of multiple replicas
- **Azure Ready**: One-command deployment with `azd up`

### Negative
- **Learning Curve**: Team needs to learn Aspire patterns
- **Preview Software**: Aspire is relatively new
- **Vendor Lock-in**: Tight coupling to Microsoft ecosystem

### Neutral
- Requires .NET 10 SDK
- Container runtime (Docker/Podman) required

## Implementation

### AppHost Configuration
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add infrastructure
var postgres = builder.AddPostgreSQL("postgres");
var qdrant = builder.AddQdrant("qdrant");
var redis = builder.AddRedis("redis");

// Add services with TMR
var api = builder.AddProject<Projects.ConvoContentBuddy_API_Brain>("api-brain")
    .WithReference(postgres)
    .WithReference(qdrant)
    .WithReference(redis)
    .WithReplicas(3);

builder.AddProject<Projects.ConvoContentBuddy_UI_Web>("ui-web")
    .WithReference(api);

builder.AddProject<Projects.ConvoContentBuddy_Data_Seeder>("data-seeder")
    .WithReference(postgres)
    .WithReference(qdrant);
```

### ServiceDefaults Configuration
```csharp
builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("ConvoContentBuddy.*"))
    .WithMetrics(metrics => metrics.AddMeter("ConvoContentBuddy.*"));
```

## References

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [.NET Aspire GitHub](https://github.com/dotnet/aspire)
- [Aspire Service Defaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults)

---

**Date**: 2026-03-12  
**Decision Makers**: Development Team  
**Supersedes**: N/A
