# **New Application Specification**

## **App Title**

**ConvoContentBuddy**

## **Development Plan**

This document provides a comprehensive, multi-phase development plan for the **ConvoContentBuddy** application. It translates the high-level architecture into actionable epics, outlines the core technologies, and defines user stories for autonomous development agents.  
The project is guided by three core principles:

1. **High-Availability & Resilience:** The system must be self-healing and robust, capable of withstanding component failures without interrupting the user experience. This is achieved through containerization, automated restarts, and an N+2 multi-tiered failover strategy.  
2. **Real-time Performance:** The application must process and analyze live audio streams with minimal latency. Sub-second retrieval and analysis are critical to providing an "ambient" experience where information appears seamlessly as it's being discussed.  
3. **Accuracy & Context-Awareness:** The system must accurately identify the user's intent from conversational cues and provide relevant, high-quality information. This is achieved through a hybrid approach combining semantic vector search, relational graph traversal, and LLM-based verification.

### ---

**Phase 1: High-Availability Foundation & Orchestration**

* **Objective:** Establish a self-healing, containerized .NET Aspire environment capable of supporting Triple Modular Redundancy (TMR) and provide centralized observability.  
* **Tasks:**  
  * Initialize a .NET 10 solution with Aspire.AppHost and Aspire.ServiceDefaults.  
  * Configure AppHost to orchestrate all services: UI.Web (Blazor), API.Brain (ASP.NET Core), Vector.Store (Qdrant), Graph.Store (PostgreSQL), and Redis.  
  * Implement standard resilience policies (Exponential Backoff: 1s, 2s, 4s, 8s, 16s) and a Circuit Breaker (30s break duration) using Polly in ServiceDefaults.  
  * Configure TMR withReplicas(3) for the API.Brain service.  
  * Implement Liveness/Readiness probes in Program.cs of the API using Microsoft.Extensions.Diagnostics.HealthChecks.

### **Phase 2: Semantic Knowledge Ingestion**

* **Objective:** Build a persistent, high-performance knowledge base of LeetCode problems with both vector and graph relational mappings.  
* **Tasks:**  
  * Build a data seeder utility (.NET console tool or background worker) to parse LeetCode metadata (JSON/Markdown).  
  * Implement an IEmbeddingService using Gemini text-embedding-004.  
  * Seed a Qdrant collection (leetcode\_problems) with 1536-dimension vectors for cosine similarity search. Upsert problem vectors and payloads (ID, Title, Difficulty).  
  * Define a PostgreSQL schema (Problems and ProblemEdges tables) and seed relationships derived from "Similar Questions" metadata to allow "Complexity Crawling" during logic analysis.

### **Phase 3: The Hybrid Intelligence "Brain"**

* **Objective:** Orchestrate semantic search, graph traversal, and LLM verification into a single, high-speed analysis pipeline.  
* **Tasks:**  
  * Implement VectorSearchProvider using the Qdrant.Client gRPC library. (Input: Transcript chunk vector; Output: Top-3 candidates).  
  * Implement GraphTraversalProvider in the API to query PostgreSQL for all neighbors of the Top-1 vector match to provide "Follow-up" context.  
  * Build HybridRetrieverService to coordinate the full chain: Embed \-\> Vector Search \-\> Graph Fetch \-\> LLM Verify. Use Gemini 2.5 Flash for the "Judge" phase to confirm the final problem ID and prevent false positives.

### **Phase 4: Aerospace-Grade Redundancy (N+2 Failover)**

* **Objective:** Implement a prioritized failover queue to ensure the system remains responsive even when primary dependencies are rate-limited or unavailable.  
* **Tasks:**  
  * Implement a ModelFailoverManager with a multi-tiered Polly policy.  
  * **Tier 1:** Gemini 2.5 Flash \+ Search Grounding.  
  * **Tier 2 (Fallback):** Alternative Model/Region (e.g., Azure OpenAI or secondary Gemini Key).  
  * **Tier 3 (Safe Mode):** Deterministic fallback that bypasses LLM verification if the Circuit Breaker is "Open", returning the \#1 Vector match directly with a confidence: low flag.  
  * Integrate real-time status updates via SignalR to inform the UI of failover events.

### **Phase 5: Ambient Real-time Interface**

* **Objective:** Develop a zero-interaction, high-performance UI that reacts to live speech and displays relevant information automatically.  
* **Tasks:**  
  * Create a Blazor WASM UI with JS Interop (speechInterop.js) to manage the browser's webkitSpeechRecognition lifecycle. Pass transcription events back to Blazor via DotNetObjectReference.  
  * Implement a SignalR hub (BuddyHub) with a Redis backplane for real-time dashboard updates.  
  * Build an autonomous controller in the UI to buffer transcript chunks and trigger analysis without user interaction (e.g., every 100 new characters).  
  * Build dynamic problem card rendering for code snippets (with syntax highlighting), logic approach, and graph context.

## ---

**Description**

### **Overview**

ConvoContentBuddy is an autonomous, real-time semantic assistant designed to listen to technical conversations (e.g., coding interviews) and proactively display relevant information. It operates as a background listener, using a hybrid intelligence system to understand the context of a discussion, identify the specific algorithmic problem being addressed, and retrieve optimal solutions and explanations via Google Search Grounding. The goal is to provide this information ambiently on a dashboard, often before the user finishes asking the prompt.

### **Document Links**

1. ConvoContentBuddy: Comprehensive Agent Development Roadmap.md  
2. ConvoContentBuddy: Technical Design Document.md  
3. ConvoContentBuddy: Implementation TODO List.md

### **Requirements**

* The system must run as a containerized application orchestrated by **.NET Aspire**.  
* It must feature a highly available API with Triple Modular Redundancy (TMR) scaling.  
* It requires a multi-layered data store: a vector database (**Qdrant**) for semantic search and a graph database (**PostgreSQL \+ pgvector**) for relational context.  
* The system must integrate with a large language model (**Gemini 2.5 Flash** and **text-embedding-004**) for text embedding, search grounding, and problem verification.  
* A real-time communication channel (**SignalR**) with a **Redis** backplane is necessary for multi-instance UI updates.  
* The frontend must utilize browser-native speech recognition APIs (webkitSpeechRecognition).

### **Features**

* \[X\] **High-Availability & TMR:** withReplicas(3) configuration for the core API to ensure constant uptime.  
* \[X\] **Resilience & Failover:** N+2 redundancy with a 3-tier fallback model (Primary LLM \-\> Secondary LLM \-\> Deterministic Safe Mode).  
* \[X\] **Hybrid Search Pipeline:** Combines vector similarity search with graph-based contextual expansion.  
* \[X\] **LLM-Powered Verification:** Uses a "Judge" LLM to filter false positives before pushing state to the UI.  
* \[X\] **Real-time Ambient UI:** A Blazor dashboard that listens for speech via JS Interop and displays detected problems via SignalR autonomously.  
* \[X\] **Centralized Observability:** Distributed tracing, logging, and metrics configured via OpenTelemetry in ServiceDefaults.  
* \[X\] **Containerization: Docker Compose:** The .NET Aspire AppHost must generate the Docker Compose file for seamless startup.

## **Acceptance Criteria**

1. **V1 (Health):** All services pass their liveness and readiness health checks in the Aspire Dashboard.  
2. **V2 (TMR Verification):** Killing 2 of the 3 API.Brain instances must not disrupt the live SignalR stream to the client (demonstrating effective Redis backplane state persistence).  
3. **V3 (Failover Testing):** Simulating an API outage (e.g., disconnecting the internet or hitting a rate limit) triggers a fallback to Tier 2 or "Safe Mode" (Tier 3), pushing corresponding status alerts to the UI.  
4. **V4 (End-to-End Speed):** An end-to-end test where a user speaks the phrase *"Given an array of integers, return indices of the two numbers such that they add up to a specific target"* must result in the system identifying "LeetCode 1: Two Sum" and displaying Python/C\# solutions on the dashboard within **2 seconds**.

## **Language**

C\#

## **Language Version**

.NET v10.0

* \[X\] Include global.json with SDK version configured for .NET 10.0 and rollForward: "latestFeature".

## **Frameworks, Tools, Packages**

* **Orchestration:** .NET Aspire (Aspire.AppHost, Aspire.ServiceDefaults)  
* **Backend Framework:** ASP.NET Core (.NET 10\)  
* **Frontend Framework:** Blazor WebAssembly (WASM)  
* **Real-time Communication:** SignalR with Redis Backplane  
* **Resilience & Fault Tolerance:** Polly (Retry, Circuit Breaker, Fallback Policies)  
* **Vector Database:** Qdrant (via Qdrant.Client gRPC library)  
* **Relational/Graph Database:** PostgreSQL with pgvector extension  
* **AI/LLM Integration:** Microsoft.SemanticKernel with connectors for Gemini (text-embedding-004, gemini-2.5-flash-preview-09-2025)  
* **Observability:** OpenTelemetry (Logging, Tracing, Metrics)  
* **UI Styling:** Tailwind CSS

## **Project Structure / Package System**

A .NET 10 solution containing the following structure:

* ConvoContentBuddy.AppHost: The .NET Aspire orchestration project.  
* ConvoContentBuddy.ServiceDefaults: A shared project for resilience, HTTP pipelines, health checks, and OpenTelemetry configuration.  
* ConvoContentBuddy.API.Brain: The main ASP.NET Core API containing the hybrid retrieval logic, SignalR hub (BuddyHub), and failover management.  
* ConvoContentBuddy.UI.Web: The Blazor WASM frontend application (Ambient UI).  
* ConvoContentBuddy.DataSeeder: A .NET console utility for parsing and ingesting LeetCode JSON/Markdown data.  
* ConvoContentBuddy.Core: A shared class library for DTOs, Event Models, and interfaces.

## **Deliverables**

* A fully functional, containerized .NET 10 application launchable via Aspire or Docker Compose.  
* Source code managed with standard clean architecture principles.  
* A deployed environment demonstrating all acceptance criteria, particularly the sub-2-second end-to-end latency test.  
* Robust failover implementation handling API rate limits gracefully.