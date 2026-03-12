# ConvoContentBuddy Architecture

## High-Level Architecture

ConvoContentBuddy is designed as a highly resilient, AI-powered background listening tool. The architecture spans four distinct layers, orchestrated by .NET 10 Aspire to ensure Triple Modular Redundancy (TMR) and graceful failovers.

### 1. Audio Input & Transcription Layer
- **Component:** Browser-based Web Speech API (`webkitSpeechRecognition`).
- **Responsibility:** Captures continuous streams of audio from the user's microphone and transcribes it into text buffers.
- **Integration:** Uses JavaScript interop (`speechInterop.js`) to communicate with the Blazor WASM client.

### 2. Ambient UI Layer
- **Component:** Blazor WebAssembly (WASM) Application (`ConvoContentBuddy.UI.Web`).
- **Responsibility:** Provides a zero-interaction dashboard that reacts autonomously to incoming SignalR events pushed by the server. Displays live transcripts and solution cards.
- **Integration:** Connects to the API layer via SignalR WebSockets.

### 3. Context & Intent Analysis Layer ("The Brain")
- **Component:** ASP.NET Core 10 Web API (`ConvoContentBuddy.API.Brain`).
- **Responsibility:** Acts as the orchestrator. Evaluates transcript buffers to identify intents, powered by Semantic Kernel and Gemini 2.5 Flash.
- **Resilience:** Deployed with 3 replicas (`withReplicas(3)`) using Aspire. Uses a Redis backplane to ensure that if one API instance fails, the SignalR WebSocket connection seamlessly re-establishes without dropping transcript state.

### 4. Resource Retrieval Layer
- **Components:**
  - **Vector Database:** Qdrant (gRPC) for high-performance semantic similarity search.
  - **Relational Database:** PostgreSQL (with pgvector) for managing relational graph edges (e.g., problem relationships) for "complexity crawling."
  - **Search Grounding:** Google Search Grounding to fetch time/space complexities.
- **Responsibility:** Executes a hybrid chain (Qdrant semantic search + PostgreSQL graph expansion + LLM verification) to fetch optimal code solutions in Python, Java, and C++.

## Key Design Decisions

1. **Aerospace-Grade Resilience (TMR):** The system employs Triple Modular Redundancy. If a primary AI or database layer fails, the system falls back to a degraded "Safe Mode" (e.g., local Qdrant vectors only) using Polly policies.
2. **Ambient User Experience:** The UI is zero-interaction. Solutions and code snippets appear organically as the conversation evolves, requiring no clicks from the user.
3. **High-Speed Semantic Matching:** Relies heavily on vector embeddings (Gemini text-embedding-004) and relational graphs to guarantee sub-500ms retrieval of coding problem contexts.
4. **.NET 10 Aspire Orchestration:** Chosen for single-command startup, centralized logging (OpenTelemetry), distributed tracing, and automatic restarts of failed components within 5 seconds.

## Project Structure

The solution follows a standard .NET Aspire modular structure:

```
ConvoContentBuddy.sln
├── ConvoContentBuddy.AppHost (Aspire Orchestrator)
├── ConvoContentBuddy.ServiceDefaults (OTLP, Health Checks, Resilience)
├── ConvoContentBuddy.API.Brain (ASP.NET Core Web API, Semantic Kernel Hub)
├── ConvoContentBuddy.UI.Web (Blazor WASM, SignalR Client, Speech Interop)
└── ConvoContentBuddy.Data.Seeder (Worker Service for LeetCode ingestion)
```
