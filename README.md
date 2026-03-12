# ConvoContentBuddy

**Autonomous Background Listener for Real-time Programming Interview Assistance**

ConvoContentBuddy is an AI-powered background listening tool designed to assist users during technical interviews or coding sessions. By seamlessly transcribing audio and analyzing intents, it identifies algorithmic problems and automatically retrieves optimal solutions—all without requiring manual user input.

## 🎯 Key Features

- **Zero-Interaction UI** - Dashboard updates organically without mouse/keyboard input
- **Live Transcript Feed** - Real-time visualization of recognized speech
- **Hybrid Vector-Graph Search** - Sub-second algorithmic matching using Cosine similarity
- **Active Problem Card & Solution Panel** - Code snippets with syntax highlighting that automatically adapt to conversation context
- **Aerospace-Grade Resilience** - Triple Modular Redundancy (TMR) with graceful failovers

## 🏗️ Architecture

ConvoContentBuddy follows a microservices architecture orchestrated by .NET Aspire 10:

```
┌─────────────────────────────────────────────────────────────────┐
│                    .NET Aspire Orchestrator                     │
│                     (AppHost - Single Startup)                  │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────────┐    ┌──────────────┐
│  Blazor WASM │◄──►│   API.Brain      │◄──►│ Data.Seeder  │
│   (UI.Web)   │    │  (3 Replicas)    │    │   Worker     │
└──────────────┘    └──────────────────┘    └──────────────┘
        │                     │                     │
        │                     │                     │
        │              ┌──────┴──────┐              │
        │              │             │              │
        ▼              ▼             ▼              ▼
┌──────────────────────────────────────────────────────────┐
│                    Infrastructure Layer                  │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────────────┐ │
│  │ Qdrant │  │Postgres│  │ Redis  │  │ Gemini 2.5     │ │
│  │(Vector)│  │(Graph) │  │(Cache) │  │ Flash API      │ │
│  └────────┘  └────────┘  └────────┘  └────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/)
- Gemini API Key ([Get one here](https://makersuite.google.com/app/apikey))

### 1. Clone and Setup

```bash
git clone <repository-url>
cd convo-content-buddy-whiskey62
```

### 2. Configure Environment

```bash
# Set your Gemini API key
export GEMINI_API_KEY=your_api_key_here
```

### 3. Run with Aspire (Recommended)

```bash
cd src/ConvoContentBuddy.AppHost
dotnet run
```

This will:
- Start PostgreSQL, Qdrant, and Redis containers
- Launch the API.Brain service with 3 replicas
- Start the Blazor WASM UI
- Open the Aspire dashboard at `http://localhost:15000`

### 4. Run with Docker Compose (Alternative)

```bash
docker-compose up -d
```

Access the application:
- **UI**: http://localhost:8080
- **API**: http://localhost:5000
- **Qdrant Dashboard**: http://localhost:6333

### 5. Seed Initial Data

```bash
# Run the data seeder to ingest LeetCode problems
docker-compose --profile seeding run data-seeder
```

## 📁 Project Structure

```
ConvoContentBuddy/
├── src/
│   ├── ConvoContentBuddy.AppHost/           # Aspire Orchestrator
│   ├── ConvoContentBuddy.ServiceDefaults/   # OTLP, Health Checks, Resilience
│   ├── ConvoContentBuddy.API.Brain/         # ASP.NET Core Web API, Semantic Kernel Hub
│   ├── ConvoContentBuddy.UI.Web/           # Blazor WASM, SignalR Client, Speech Interop
│   └── ConvoContentBuddy.Data.Seeder/      # Worker Service for LeetCode ingestion
├── tests/
│   └── ConvoContentBuddy.Tests/            # Unit & Integration Tests
├── docker/                                  # Docker configurations
│   ├── api-brain.Dockerfile
│   ├── data-seeder.Dockerfile
│   ├── ui-web.Dockerfile
│   └── nginx.conf
├── docs/                                    # Documentation
│   ├── architecture/                        # Architecture Decision Records (ADRs)
│   ├── api/                                # API Documentation
│   └── user-guide/                         # User Guides
├── docker-compose.yml                       # Local development orchestration
└── ConvoContentBuddy.slnx                  # Solution file
```

## 🛠️ Technology Stack

| Category | Technology |
|----------|------------|
| **Language & Runtime** | C# 14, .NET 10 |
| **UI Framework** | Blazor WebAssembly 10 |
| **API Framework** | ASP.NET Core 10 |
| **Orchestration** | .NET Aspire 10 |
| **AI/ML** | Microsoft Semantic Kernel, Gemini 2.5 Flash |
| **Vector Database** | Qdrant (gRPC) |
| **Relational Database** | PostgreSQL with pgvector |
| **Caching & Backplane** | Redis |
| **Real-time Communication** | SignalR (WebSockets) |
| **Observability** | OpenTelemetry (OTLP) |
| **Containerization** | Docker/Podman |

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter "Category=Integration"
```

## 📊 Monitoring & Observability

ConvoContentBuddy includes comprehensive observability out of the box:

- **Distributed Tracing**: OpenTelemetry traces across all services
- **Metrics**: Performance counters and custom metrics
- **Health Checks**: Kubernetes-ready health endpoints
- **Logging**: Structured logging with Serilog

Access the Aspire Dashboard for real-time monitoring at `http://localhost:15000` when running with Aspire.

## 🔧 Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `GEMINI_API_KEY` | Google Gemini API key | Yes |
| `ConnectionStrings__PostgreSQL` | PostgreSQL connection string | No (default provided) |
| `Qdrant__Host` | Qdrant host | No (default: localhost) |
| `Redis__ConnectionString` | Redis connection string | No (default: localhost:6379) |

### Configuration Files

- `appsettings.json` - Production configuration
- `appsettings.Development.json` - Development overrides

## 📚 Documentation

- [Architecture Overview](docs/architecture/README.md)
- [API Documentation](docs/api/README.md)
- [User Guide](docs/user-guide/README.md)
- [Deployment Guide](docs/deployment/README.md)
- [Repository Summary](.ai-repository-summary.md)

## 🚢 Deployment

### Azure Deployment (azd)

```bash
# Install Azure Developer CLI
# brew install azure/azd/azd  # macOS
# winget install Microsoft.Azd  # Windows

# Login to Azure
azd auth login

# Provision and deploy
azd up
```

### Docker Deployment

```bash
# Build images
docker-compose build

# Push to registry
docker-compose push

# Deploy to production
docker-compose -f docker-compose.prod.yml up -d
```

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎯 Roadmap

### Phase 1: High-Availability Foundation & Orchestration ✅
- [x] Initialize .NET 10 solution
- [x] Setup Aspire orchestration with TMR
- [x] Configure OpenTelemetry and health checks

### Phase 2: Semantic Knowledge Ingestion
- [ ] Build data seeder utility
- [ ] Implement embedding service
- [ ] Upsert vectors to Qdrant

### Phase 3: The Hybrid Intelligence "Brain"
- [ ] Implement vector search provider
- [ ] Implement graph traversal provider
- [ ] Build hybrid retriever service

### Phase 4: Aerospace-Grade Redundancy
- [ ] Implement failover manager
- [ ] Configure multi-tier fallbacks
- [ ] Enable deterministic safe mode

### Phase 5: Ambient Real-time Interface
- [ ] Implement speech interop
- [ ] Build SignalR hub
- [ ] Develop autonomous UI components

## 📞 Support

- **Documentation**: [docs/](docs/)
- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)

## 🙏 Acknowledgments

- [.NET Aspire Team](https://github.com/dotnet/aspire)
- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Qdrant Vector Database](https://qdrant.tech/)
- [Google Gemini](https://ai.google.dev/)

---

**Built with ❤️ using .NET 10 and AI**
