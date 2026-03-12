# **ConvoContentBuddy: Comprehensive Agent Development Roadmap**

This document serves as the master implementation directive for ConvoContentBuddy. It translates the high-level architecture into actionable epics and tasks for autonomous development agents.

## **Epic 1: High-Availability Foundation & Orchestration**

**Goal**: Establish a self-healing, containerized .NET Aspire environment capable of supporting Triple Modular Redundancy (TMR).

### **User Stories**

* **US 1.1**: As an operator, I want a single-command startup for all services (API, UI, DBs) using Docker Compose and Aspire.  
* **US 1.2**: As a developer, I want centralized logging and distributed tracing to debug the "Hybrid Chain" in real-time.  
* **US 1.3**: As a system, I want to automatically restart any failed component within 5 seconds to maintain uptime.

### **Agent Implementation Tasks**

* \[ \] **Task 1.1: Project Initialization**  
  * Create a .NET 10 solution with Aspire.AppHost and Aspire.ServiceDefaults.  
  * Configure AppHost to orchestrate UI.Web (Blazor), API.Brain (ASP.NET Core), Vector.Store (Qdrant), Graph.Store (PostgreSQL), and Redis (SignalR Backplane).  
* \[ \] **Task 1.2: Resilience Policy Implementation**  
  * In ServiceDefaults, implement a standard HttpResiliencePipeline.  
  * Configure Exponential Backoff (1s, 2s, 4s, 8s, 16s) and a Circuit Breaker with a 30s break duration.  
* \[ \] **Task 1.3: TMR Configuration**  
  * Set withReplicas(3) for the API.Brain service in the AppHost.  
  * Implement Liveness/Readiness probes in Program.cs of the API using Microsoft.Extensions.Diagnostics.HealthChecks.

## **Epic 2: Semantic Knowledge Ingestion**

**Goal**: Build a persistent, high-performance knowledge base of LeetCode problems with vector and graph relational mappings.

### **User Stories**

* **US 2.1**: As an admin, I want to ingest the entire LeetCode catalog and store their semantic "fingerprints" for sub-second retrieval.  
* **US 2.2**: As a system, I want to map relationships between problems (e.g., "Two Sum" leads to "Three Sum") using a relational graph.

### **Agent Implementation Tasks**

* \[ \] **Task 2.1: Data Seeder Utility**  
  * Build a .NET console tool or background worker to parse LeetCode metadata (JSON/Markdown).  
  * Implement IEmbeddingService using Gemini text-embedding-004.  
* \[ \] **Task 2.2: Vector Seeding (Qdrant)**  
  * Create a collection leetcode\_problems with 1536-dim Cosine similarity.  
  * Upsert problem vectors and payloads (ID, Title, Difficulty).  
* \[ \] **Task 2.3: Graph Seeding (PostgreSQL)**  
  * Define schema: Problems table and ProblemEdges table (adjacency list).  
  * Seed edges derived from "Similar Questions" metadata to allow "Complexity Crawling" during logic analysis.

## **Epic 3: The Hybrid Intelligence "Brain"**

**Goal**: Orchestrate the semantic search, graph traversal, and LLM verification into a single, high-speed analysis pipeline.

### **User Stories**

* **US 3.1**: As a user, I want the system to identify the problem being discussed even if the phrasing is different from the official description.  
* **US 3.2**: As a system, I want to verify vector matches using an LLM before pushing them to the UI to prevent false positives.

### **Agent Implementation Tasks**

* \[ \] **Task 3.1: Vector Retrieval Service**  
  * Implement VectorSearchProvider using the Qdrant.Client gRPC library.  
  * Input: Transcript chunk vector; Output: Top-3 candidates with similarity scores.  
* \[ \] **Task 3.2: Graph Expansion Logic**  
  * Implement GraphTraversalProvider in the API.  
  * Query PostgreSQL for all neighbors of the Top-1 vector match to provide "Follow-up" context.  
* \[ \] **Task 3.3: Hybrid Chain Integration**  
  * Build HybridRetrieverService to coordinate: Embed \-\> Vector Search \-\> Graph Fetch \-\> LLM Verify.  
  * Use Gemini 2.5 Flash for the "Judge" phase to confirm the final problem ID.

## **Epic 4: Aerospace-Grade Redundancy (N+2 Failover)**

**Goal**: Implement the prioritized failover queue to ensure the system never stops responding.

### **User Stories**

* **US 4.1**: As a user, I want the system to keep working (even with reduced features) if the primary AI model hits a rate limit.  
* **US 4.2**: As a system, I want to switch to a local "Safe Mode" if all cloud services are unreachable.

### **Agent Implementation Tasks**

* \[ \] **Task 4.1: ModelFailoverManager**  
  * Implement a FailoverPolicy using Polly.  
  * **Tier 1**: Gemini 2.5 Flash \+ Search Grounding.  
  * **Tier 2 (Fallback)**: Alternative Model/Region (e.g., Azure OpenAI or secondary Gemini Key).  
* \[ \] **Task 4.2: Deterministic "Safe Mode" (Tier 3\)**  
  * Implement logic that bypasses LLM verification if the Circuit Breaker is "Open."  
  * Return the \#1 Vector match directly with a confidence: low flag.  
* \[ \] **Task 4.3: Real-time Failover Logging**  
  * Push "System Status" updates to the UI via SignalR whenever a failover occurs (e.g., "Switching to Standby Engine...").

## **Epic 5: Ambient Real-time Interface**

**Goal**: Develop a zero-interaction, high-performance UI that reacts to live speech.

### **User Stories**

* **US 5.1**: As a user, I want the app to listen to my microphone and show me a live transcript.  
* **US 5.2**: As a user, I want solution cards to "appear" automatically on my dashboard without me clicking anything.

### **Agent Implementation Tasks**

* \[ \] **Task 5.1: Blazor Speech Interop**  
  * Write speechInterop.js to manage the webkitSpeechRecognition lifecycle.  
  * Pass transcription events back to Blazor via DotNetObjectReference.  
* \[ \] **Task 5.2: SignalR Real-time Dashboard**  
  * Implement BuddyHub in the API with Redis Backplane.  
  * Build a Blazor component that listens for OnProblemDetected events and renders code snippets with syntax highlighting.  
* \[ \] **Task 5.3: Autonomous Controller Logic**  
  * Implement a timer-based or threshold-based "Push" in the UI (e.g., every 100 new characters, trigger an analysis).

## **Verification & Deployment Checklist**

* \[ \] **V1**: All services pass health checks in the Aspire Dashboard.  
* \[ \] **V2**: Killing 2 of 3 API instances does not disrupt the live SignalR stream (Redis verification).  
* \[ \] **V3**: Disconnecting the internet triggers "Safe Mode" UI alerts.  
* \[ \] **V4**: End-to-end "Two Sum" test: System identifies problem and shows Python/C\# solutions within 2 seconds of the prompt being spoken.

**Current Status**: 🏗️ Phase 1 (Foundation)