# **New Application: ConvoContentBuddy**

## **App Title**

ConvoContentBuddy

## **Development Plan**

This document provides a comprehensive, multi-phase development plan for the ConvoContentBuddy application. It translates the high-level architecture into actionable epics and tasks, upgraded to leverage .NET 10 capabilities for an autonomous, self-healing environment.

### **1\. Motivation & Guiding Principles**

ConvoContentBuddy is an autonomous background listener designed to assist users in real-time during technical interviews or coding sessions. By seamlessly transcribing audio and analyzing intents, it identifies the specific algorithmic problem (e.g., LeetCode) being discussed and automatically retrieves optimal solutions, complexities, and logic via the Gemini API—all without requiring manual user input.

**Guiding Principles:**

* **Aerospace-Grade Resilience (TMR):** The system must employ Triple Modular Redundancy and graceful failovers. If a primary AI or database layer fails, the system must continue to operate in a degraded "Safe Mode."  
* **Ambient User Experience:** The UI must be zero-interaction. Solutions and code snippets should "appear" organically as the conversation evolves, requiring no clicks from the user.  
* **High-Speed Semantic Matching:** The system relies heavily on vector embeddings and relational graphs to guarantee sub-500ms retrieval of coding problem contexts.

### **2\. Architectural Decisions**

To achieve high availability and a seamless ambient UI, we have opted for a **.NET 10 Aspire-orchestrated architecture** containing specialized microservices:

* **UI Layer (Blazor WASM):** Chosen for its ability to run rich, C\#-based real-time UI components directly in the browser while easily interoperating with the JavaScript webkitSpeechRecognition API.  
* **The "Brain" API (ASP.NET Core 10):** Acts as the orchestrator. It uses Microsoft.SemanticKernel combined with .NET 10's Microsoft.Extensions.AI to manage prompt execution, search grounding, and failover routing.  
* **Knowledge Base (Qdrant & PostgreSQL):** Qdrant was selected for its high-performance gRPC vector similarity search, while PostgreSQL (with pgvector) manages the relational graph edges (e.g., problem relationships) for "complexity crawling."  
* **Real-Time Sync (SignalR \+ Redis):** To support TMR (3 replicated API instances), a Redis backplane is necessary so that if one API instance fails, the SignalR WebSocket connection seamlessly re-establishes without dropping transcript state.

### **3\. Phased Development Plan & Epics**

#### **Phase 1: High-Availability Foundation & Orchestration**

**Objective:** Establish a self-healing, containerized .NET Aspire 10 environment.

* **Epic: Core Infrastructure Setup**  
  * *Story 1.1:* As an operator, I want a single-command startup for all services (API, UI, DBs) using Docker Compose and Aspire, so that local development matches production infrastructure.  
  * *Story 1.2:* As a developer, I want centralized logging and distributed tracing (OpenTelemetry) to trace the "Hybrid Chain" in real-time.  
  * *Story 1.3:* As a system, I want to automatically restart any failed component within 5 seconds using Polly and Aspire health checks.  
  * **Tasks:** Initialize .NET 10 solution, setup Aspire.AppHost orchestrating Blazor, ASP.NET Core API (withReplicas(3)), Qdrant, PostgreSQL, and Redis.

#### **Phase 2: Semantic Knowledge Ingestion**

**Objective:** Build a persistent, high-performance knowledge base of coding problems.

* **Epic: Data Engineering & Seeding**  
  * *Story 2.1:* As an admin, I want to ingest the entire LeetCode catalog and store semantic "fingerprints" for sub-second retrieval.  
  * *Story 2.2:* As a system, I want to map relationships between problems using a relational graph in PostgreSQL.  
  * **Tasks:** Build data seeder utility, implement IEmbeddingService using Gemini text-embedding-004, upsert vectors to Qdrant, seed graph edges in Postgres.

#### **Phase 3: The Hybrid Intelligence "Brain"**

**Objective:** Orchestrate semantic search, graph traversal, and LLM verification.

* **Epic: Hybrid Retrieval Pipeline**  
  * *Story 3.1:* As a user, I want the system to identify the problem being discussed even if phrasing differs from official descriptions.  
  * *Story 3.2:* As a system, I want to verify vector matches using Gemini 2.5 Flash before pushing to the UI to prevent false positives.  
  * **Tasks:** Implement VectorSearchProvider (Qdrant gRPC), GraphTraversalProvider, and HybridRetrieverService using Semantic Kernel.

#### **Phase 4: Aerospace-Grade Redundancy (N+2 Failover)**

**Objective:** Ensure the system never stops responding.

* **Epic: Resilience Routing**  
  * *Story 4.1:* As a system, I want to switch to a secondary AI model or region if the primary hits a rate limit.  
  * *Story 4.2:* As a user, I want the system to enter a local "Safe Mode" if all cloud services are unreachable, returning the raw vector match with a "low confidence" flag.  
  * **Tasks:** Implement ModelFailoverManager with Polly policies (Tier 1: Gemini \+ Search, Tier 2: Fallback, Tier 3: Deterministic Safe Mode).

#### **Phase 5: Ambient Real-time Interface**

**Objective:** Develop a zero-interaction UI.

* **Epic: Front-End Dashboard**  
  * *Story 5.1:* As a user, I want the app to listen to my mic and show a live transcript automatically.  
  * *Story 5.2:* As a user, I want solution cards to appear on my dashboard as I speak.  
  * **Tasks:** Write speechInterop.js wrapper, implement BuddyHub (SignalR), and build autonomous client-side controller logic (buffer and debounced POSTs).

## **Description**

ConvoContentBuddy is a highly resilient, AI-powered background listening tool designed to provide real-time programming interview assistance. It processes live speech audio, isolates semantic intent, retrieves exact algorithmic problems from a local vector/graph database, and leverages Gemini 2.5 Flash with Search Grounding to present optimal code solutions in a zero-interaction, ambient UI.

## **Overview**

The architecture spans four distinct layers:

1. **Audio Input & Transcription Layer:** Browser-based Web Speech API capturing continuous streams.  
2. **Context & Intent Analysis Layer ("The Brain"):** Evaluates transcript buffers to identify intents, powered by Semantic Kernel and Gemini 2.5 Flash.  
3. **Resource Retrieval Layer:** A hybrid chain executing a Qdrant semantic search, PostgreSQL graph expansion, and Google Search Grounding to fetch time/space complexities.  
4. **Ambient UI Layer:** A Blazor WASM application that reacts autonomously to incoming SignalR events pushed by the server.

## **Document Links**

### Project Documents

* [ConvoContentBuddy: Comprehensive Agent Development Roadmap](./ConvoContentBuddy_%20Comprehensive%20Agent%20Development%20Roadmap.md)
* [ConvoContentBuddy: Implementation TODO List](./ConvoContentBuddy_%20Implementation%20TODO%20List.md)
* [ConvoContentBuddy: Technical Design Document](./ConvoContentBuddy_%20Technical%20Design%20Document.md)

### External Documentation

* [Semantic Kernel .NET Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
* [.NET 10 Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
* [Qdrant gRPC C# Client Documentation](https://qdrant.tech/documentation/concepts/vectors/)

## **Requirements**

* System must seamlessly transcribe continuous audio from the user's microphone.  
* System must identify algorithms/LeetCode problems based on conversational descriptions.  
* System must retrieve code in Python, Java, and C++.  
* System architecture must enforce Triple Modular Redundancy (TMR) for the API layer.  
* System must execute a fallback strategy (Safe Mode) in the event of LLM provider outages.

## **Features**

* **Zero-Interaction UI:** The dashboard updates organically without mouse/keyboard input.  
* **Live Transcript Feed:** Real-time visualization of recognized speech.  
* **Hybrid Vector-Graph Search:** Sub-second algorithmic matching using Cosine similarity.  
* **Active Problem Card & Solution Panel:** Code snippets with syntax highlighting that automatically adapt to the conversation's context.

## **Test Cases**

* **V1 (Health):** All services (API, Web, Redis, Postgres, Qdrant) must pass health checks in the Aspire Dashboard on startup.  
* **V2 (TMR Verification):** Killing 2 of 3 running API.Brain instances must not disrupt the active SignalR stream connected to the Blazor client (verified via Redis Backplane state preservation).  
* **V3 (Failover):** Disconnecting the outbound internet connection must trigger a "Safe Mode" UI alert, falling back to local Qdrant vectors only.  
* **V4 (E2E Latency):** Speaking the prompt for "Two Sum" must result in the system identifying the problem and pushing Python/C\# solution cards to the UI within 2.0 seconds.

## **Logging**

* **OpenTelemetry (OTLP):** Distributed tracing and metrics will be configured centrally within Aspire.ServiceDefaults.  
* .NET 10 Authentication/Authorization Metrics: Exposed via System.Diagnostics.Metrics.  
* Semantic Kernel activities will be logged to trace prompt execution, token usage, and plugin invocation times.

## **Containerization: Docker**

* Yes. All microservices and databases will be containerized using Docker/Podman compliant images.

## **Containerization: Docker Compose**

* Managed intrinsically via **.NET 10 Aspire**. The AppHost project will act as the orchestrator, dynamically generating manifests and wiring connection strings for local development and producing standard deployments for production.

## **Swagger/OpenAPI**

* Yes. Enabled for the API.Brain ASP.NET Core project using the updated .NET 10 OpenAPI enhancements to document the webhook and REST fallback endpoints.

## **Documentation**

* Inline XML documentation for all public C\# methods and interfaces.  
* A comprehensive README.md outlining the local startup sequence via Aspire.  
* Architecture Decision Records (ADRs) for Semantic Kernel plugin definitions and failover policies.

## **Acceptance Criteria**

* **Performance:** Semantic vector matching must complete in under 500ms. End-to-end processing (Transcript \-\> AI Judge \-\> UI Card) must complete in under 2 seconds.  
* **Resilience:** The application must survive the loss of any single container without dropping the user's session.  
* **Accuracy:** The Hybrid Retriever must successfully identify the correct coding problem from a conversational description at least 95% of the time in benchmark testing.

## **Language**

C\#, JavaScript (Interop), SQL.

## **Language Version**

.NET v10.0 (C\# 14\)

## **Include global.json?**

Yes.

{  
  "sdk": {  
    "version": "10.0.0",  
    "rollForward": "latestFeature"  
  }  
}

## **Frameworks, Tools, Packages**

* **Core:** .NET 10, ASP.NET Core 10, Blazor WebAssembly 10  
* **Orchestration:** .NET Aspire 10 (Aspire.Hosting.AppHost, Aspire.Hosting.Redis, Aspire.Hosting.PostgreSQL, Aspire.Hosting.Qdrant)  
* **AI/LLM:** Microsoft.SemanticKernel, Microsoft.Extensions.AI  
* **Data & State:** Qdrant.Client (gRPC), Npgsql.EntityFrameworkCore.PostgreSQL (with pgvector), Microsoft.AspNetCore.SignalR.StackExchangeRedis  
* **Resilience:** Microsoft.Extensions.Http.Resilience (Polly)  
* **Styling:** Tailwind CSS

## **Project Structure/Package System**

The solution follows a standard .NET Aspire modular structure:

* ConvoContentBuddy.sln  
  * ConvoContentBuddy.AppHost (Aspire Orchestrator)  
  * ConvoContentBuddy.ServiceDefaults (OTLP, Health Checks, Resilience)  
  * ConvoContentBuddy.API.Brain (ASP.NET Core Web API, Semantic Kernel Hub)  
  * ConvoContentBuddy.UI.Web (Blazor WASM, SignalR Client, Speech Interop)  
  * ConvoContentBuddy.Data.Seeder (Worker Service for LeetCode ingestion)

## **GitHub**

* **Repo:** https://www.google.com/search?q=https://github.com/intel-agency/ConvoContentBuddy  
* **Branch:** main (production), develop (integration)

## **Deliverables**

1. Fully functioning, buildable .NET 10 solution containing the AppHost, API, and Web projects.  
2. Automated deployment manifests generated via azd (Azure Developer CLI) integration with Aspire.  
3. Seeder utility scripts for scraping/ingesting data into Qdrant/Postgres.  
4. Comprehensive test suite verifying the V1-V4 scenarios.