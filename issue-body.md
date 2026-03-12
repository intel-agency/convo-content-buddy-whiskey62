# ConvoContentBuddy – Complete Implementation

## Overview
ConvoContentBuddy is an autonomous background listener designed to assist users in real-time during technical interviews or coding sessions. By seamlessly transcribing audio and analyzing intents, it identifies the specific algorithmic problem (e.g., LeetCode) being discussed and automatically retrieves optimal solutions, complexities, and logic via the Gemini API—all without requiring manual user input.

This plan is based on the [New Application Spec: ConvoContentBuddy](docs/ai-new-app-template.md) and supporting documents.

## Goals
- Provide a zero-interaction, ambient UI that updates organically without mouse/keyboard input.
- Achieve high-speed semantic matching (sub-500ms retrieval) using vector embeddings and relational graphs.
- Ensure aerospace-grade resilience (TMR) with graceful failovers and a degraded "Safe Mode".
- Seamlessly transcribe continuous audio from the user's microphone.

## Technology Stack

- Language: C# 14 (.NET 10.0), JavaScript (Interop), SQL
- UI Framework: Blazor WebAssembly 10
- AI/Runtime: Microsoft.SemanticKernel, Microsoft.Extensions.AI, Gemini 2.5 Flash, Gemini text-embedding-004
- Architecture: Microservices orchestrated by .NET Aspire 10
- Databases/Storage: Qdrant (gRPC), PostgreSQL with pgvector, Redis
- Logging/Observability: OpenTelemetry (OTLP)
- Containerization/Infra: Docker, Docker Compose (via Aspire), azd (Azure Developer CLI)

## Application Features
- **Zero-Interaction UI:** The dashboard updates organically without mouse/keyboard input.
- **Live Transcript Feed:** Real-time visualization of recognized speech.
- **Hybrid Vector-Graph Search:** Sub-second algorithmic matching using Cosine similarity.
- **Active Problem Card & Solution Panel:** Code snippets with syntax highlighting that automatically adapt to the conversation's context.

## System Architecture
### Core Services
1. **ConvoContentBuddy.AppHost** — Aspire Orchestrator for single-command startup and container management.
2. **ConvoContentBuddy.API.Brain** — ASP.NET Core Web API acting as the orchestrator. Evaluates transcript buffers to identify intents, powered by Semantic Kernel and Gemini 2.5 Flash.
3. **ConvoContentBuddy.UI.Web** — Blazor WASM client providing a zero-interaction dashboard that reacts autonomously to incoming SignalR events.
4. **ConvoContentBuddy.Data.Seeder** — Worker Service for LeetCode ingestion and knowledge base seeding.

### Key Features (system-level)
- Triple Modular Redundancy (TMR) for the API layer with a Redis backplane.
- Hybrid chain executing a Qdrant semantic search, PostgreSQL graph expansion, and Google Search Grounding.
- Fallback strategy (Safe Mode) in the event of LLM provider outages.

## Project Structure

```
ConvoContentBuddy/
├─ src/
│  ├─ ConvoContentBuddy.AppHost/
│  ├─ ConvoContentBuddy.ServiceDefaults/
│  ├─ ConvoContentBuddy.API.Brain/
│  ├─ ConvoContentBuddy.UI.Web/
│  └─ ConvoContentBuddy.Data.Seeder/
├─ tests/
├─ docs/
├─ scripts/
├─ docker/
├─ assets/
└─ global.json
```

---

## Implementation Plan

### Phase 1: High-Availability Foundation & Orchestration
- [ ] 1.1. Initialize .NET 10 solution and repository bootstrap
- [ ] 1.2. Setup Aspire.AppHost orchestrating Blazor, ASP.NET Core API (withReplicas(3)), Qdrant, PostgreSQL, and Redis
- [ ] 1.3. Configure centralized logging and distributed tracing (OpenTelemetry)
- [ ] 1.4. Implement Polly and Aspire health checks for automatic restarts

### Phase 2: Semantic Knowledge Ingestion
- [ ] 2.1. Build data seeder utility (ConvoContentBuddy.Data.Seeder)
- [ ] 2.2. Implement IEmbeddingService using Gemini text-embedding-004
- [ ] 2.3. Upsert vectors to Qdrant
- [ ] 2.4. Seed graph edges in PostgreSQL

### Phase 3: The Hybrid Intelligence "Brain"
- [ ] 3.1. Implement VectorSearchProvider (Qdrant gRPC)
- [ ] 3.2. Implement GraphTraversalProvider
- [ ] 3.3. Implement HybridRetrieverService using Semantic Kernel
- [ ] 3.4. Verify vector matches using Gemini 2.5 Flash before pushing to the UI

### Phase 4: Aerospace-Grade Redundancy (N+2 Failover)
- [ ] 4.1. Implement ModelFailoverManager with Polly policies
- [ ] 4.2. Configure Tier 1: Gemini + Search
- [ ] 4.3. Configure Tier 2: Fallback
- [ ] 4.4. Configure Tier 3: Deterministic Safe Mode (local Qdrant vectors only)

### Phase 5: Ambient Real-time Interface
- [ ] 5.1. Write speechInterop.js wrapper for Web Speech API
- [ ] 5.2. Implement BuddyHub (SignalR) for real-time communication
- [ ] 5.3. Build autonomous client-side controller logic (buffer and debounced POSTs)
- [ ] 5.4. Develop UI components for live transcript and solution cards

---

## Mandatory Requirements Implementation

### Testing & Quality Assurance
- [ ] Unit tests — coverage target: 80%+
- [ ] Integration tests (V1-V4 scenarios)
- [ ] E2E tests (V4 E2E Latency)
- [ ] Performance/load tests (V2 TMR Verification)
- [ ] Automated tests in CI

### Documentation & UX
- [ ] Comprehensive README outlining local startup sequence via Aspire
- [ ] User manual and feature docs
- [ ] XML/API docs (public APIs)
- [ ] Architecture Decision Records (ADRs) for Semantic Kernel plugin definitions and failover policies
- [ ] Swagger/OpenAPI enabled for API.Brain

### Build & Distribution
- [ ] Build scripts
- [ ] Containerization support (Docker/Podman compliant images)
- [ ] Automated deployment manifests generated via azd
- [ ] Release pipeline

### Infrastructure & DevOps
- [ ] CI/CD workflows (build/test/scan/publish)
- [ ] Static analysis and security scanning
- [ ] Performance benchmarking/monitoring (OpenTelemetry)

---

## Acceptance Criteria
- [ ] Core architecture implemented and components communicate as designed
- [ ] Key features/functionality work end-to-end
- [ ] Observability/logging in place with actionable signals
- [ ] Security model and controls validated
- [ ] Test coverage target met and CI green
- [ ] Containerization/packaging works for target environment(s)
- [ ] Documentation complete and accurate
- [ ] Performance: Semantic vector matching completes in under 500ms. End-to-end processing completes in under 2 seconds.
- [ ] Resilience: The application survives the loss of any single container without dropping the user's session.
- [ ] Accuracy: The Hybrid Retriever successfully identifies the correct coding problem from a conversational description at least 95% of the time.

## Risk Mitigation Strategies

| Risk | Mitigation |
|------|------------|
| LLM Provider Outage | Implement ModelFailoverManager with a Deterministic Safe Mode falling back to local Qdrant vectors. |
| API Instance Failure | Use Triple Modular Redundancy (TMR) with a Redis backplane to seamlessly re-establish SignalR connections. |
| High Latency | Utilize Qdrant gRPC and PostgreSQL pgvector for sub-second algorithmic matching. |

## Timeline Estimate
- Phase 1: 1-2 weeks
- Phase 2: 1-2 weeks
- Phase 3: 2-3 weeks
- Phase 4: 1-2 weeks
- Phase 5: 2-3 weeks
- Total: 7-12 weeks

## Success Metrics
- Sub-500ms semantic vector matching latency.
- Sub-2s end-to-end processing latency.
- 95%+ accuracy in identifying coding problems.
- 100% uptime during single-container failures.

## Repository Branch
Target branch for implementation: `dynamic-workflow-project-setup`

## Implementation Notes
Key assumptions: The user's browser supports the Web Speech API. The system relies on .NET 10 Aspire for orchestration and local development parity with production.
