# ConvoContentBuddy Technology Stack

## Core Languages & Frameworks
- **Language:** C# 14 (.NET 10.0), JavaScript (Interop), SQL
- **Framework:** ASP.NET Core 10, Blazor WebAssembly 10
- **Orchestration:** .NET Aspire 10 (Aspire.Hosting.AppHost, Aspire.Hosting.Redis, Aspire.Hosting.PostgreSQL, Aspire.Hosting.Qdrant)

## AI & Machine Learning
- **AI Orchestration:** Microsoft.SemanticKernel
- **AI Integration:** Microsoft.Extensions.AI
- **Models:** Gemini 2.5 Flash (via API), Gemini text-embedding-004

## Data & State Management
- **Vector Database:** Qdrant (via Qdrant.Client gRPC)
- **Relational Database:** PostgreSQL with pgvector (via Npgsql.EntityFrameworkCore.PostgreSQL)
- **Real-Time Sync & State:** Redis (via Microsoft.AspNetCore.SignalR.StackExchangeRedis)

## Real-Time Communication
- **WebSockets:** SignalR for real-time bidirectional communication between the API and Blazor client.
- **Audio Capture:** Browser-based Web Speech API (webkitSpeechRecognition)

## Resilience & Observability
- **Resilience:** Microsoft.Extensions.Http.Resilience (Polly) for Triple Modular Redundancy (TMR) and failover routing.
- **Observability:** OpenTelemetry (OTLP) for distributed tracing and metrics, integrated via Aspire.ServiceDefaults.

## UI & Styling
- **Styling:** Tailwind CSS

## Containerization & Infrastructure
- **Containerization:** Docker/Podman compliant images
- **Local Orchestration:** Docker Compose (managed intrinsically via .NET 10 Aspire)
- **Deployment:** azd (Azure Developer CLI) integration with Aspire
