# **ConvoContentBuddy: Implementation TODO List**

This document tracks the progress of the implementation for the Hybrid Semantic Assistant. It is structured into logical phases following the Architecture Guide.

## **Phase 1: Orchestration & Infrastructure**

*Goal: Establish the .NET Aspire environment and shared resilience plumbing.*

* \[ \] **1.1 Setup .NET Aspire AppHost**  
  * Initialize the AppHost project.  
  * Configure service discovery for internal components.  
* \[ \] **1.2 Implementation of ServiceDefaults**  
  * Configure OpenTelemetry (OTLP) for tracing and metrics.  
  * Implement health check endpoints (/health).  
  * Define global Polly resilience pipelines (Retry, Circuit Breaker).  
* \[ \] **1.3 Infrastructure Containers**  
  * Add Qdrant (Vector Store) to AppHost with persistence.  
  * Add PostgreSQL \+ pgvector (Graph Store) to AppHost with persistence.  
  * Add Redis (SignalR Backplane) to AppHost.

## **Phase 2: Data Engineering & Knowledge Base**

*Goal: Populate the semantic stores with LeetCode problem data.*

* \[ \] **2.1 LeetCode Scraper/Importer**  
  * Develop a utility to parse LeetCode problem data (ID, Title, Description, Solutions).  
* \[ \] **2.2 Vector Ingestion Pipeline**  
  * Integrate Gemini text-embedding-004 to generate vectors for descriptions.  
  * Seed the Qdrant leetcode\_problems collection.  
* \[ \] **2.3 Graph Seeding**  
  * Populate PostgreSQL with problem metadata.  
  * Establish relationships (edges) based on "Related Topics" or difficulty levels.

## **Phase 3: The API "Brain" Logic**

*Goal: Implement the core hybrid retrieval and failover management.*

* \[ \] **3.1 Semantic Kernel Integration**  
  * Configure Microsoft.SemanticKernel with Gemini connectors.  
* \[ \] **3.2 Hybrid Retriever Service**  
  * Implement Vector Search logic (Qdrant gRPC).  
  * Implement Graph Expansion logic (PostgreSQL).  
  * Implement LLM Verification logic (Gemini 2.5 Flash).  
* \[ \] **3.3 SignalR Hub & Redis Backplane**  
  * Create BuddyHub for real-time pushing.  
  * Configure Redis backplane for multi-instance sync.  
* \[ \] **3.4 N+2 Failover Manager**  
  * Implement Tier 1 (Gemini 2.5 Flash \+ Search).  
  * Implement Tier 2 (Alternate Model/Region).  
  * Implement Tier 3 (Deterministic Safe Mode).

## **Phase 4: Frontend Development**

*Goal: Build the ambient listener and real-time dashboard.*

* \[ \] **4.1 Blazor WASM Shell**  
  * Setup UI layout with Tailwind CSS.  
* \[ \] **4.2 JS Interop for Speech API**  
  * Build speechInterop.js wrapper for webkitSpeechRecognition.  
* \[ \] **4.3 Autonomous Controller**  
  * Implement client-side buffering and debounced POSTing to the API.  
* \[ \] **4.4 Real-time Problem Dashboard**  
  * SignalR client integration.  
  * Dynamic problem card rendering (solutions, approach, graph context).

## **Phase 5: Testing & Reliability**

*Goal: Verify self-healing and aerospace-grade resilience.*

* \[ \] **5.1 TMR Verification**  
  * Test withReplicas(3) scaling in Aspire.  
  * Verify SignalR state persistence across instance restarts via Redis.  
* \[ \] **5.2 Failover Testing**  
  * Simulate Gemini API outages and verify switch to Tier 2/Tier 3\.  
* \[ \] **5.3 End-to-End Load Testing**  
  * Verify sub-500ms latency for semantic matching.

## **Phase 6: Future Roadmap**

* \[ \] **6.1 DB Clustering (Patroni/Qdrant Raft)**  
* \[ \] **6.2 Multi-Region Deployment**  
* \[ \] **6.3 "Glider Mode" (IndexedDB \+ ONNX Runtime Web)**

**Current Status:** 🏗️ Phase 1 (Initial Setup)