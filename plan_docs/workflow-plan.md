# Workflow Execution Plan: project-setup

## 1. Overview
- **Workflow Name:** `project-setup`
- **Workflow File:** `ai_instruction_modules/ai-workflow-assignments/dynamic-workflows/project-setup.md`
- **Project Name:** ConvoContentBuddy
- **Project Description:** An autonomous background listener for real-time programming interview assistance. It transcribes audio, identifies algorithmic problems, and retrieves optimal solutions using Gemini API, Qdrant, and PostgreSQL.
- **Total Assignments:** 6 main assignments (plus 1 pre-script event and 2 post-assignment events per task).
- **Summary:** This workflow initiates the GitHub repository, plans the application phases, scaffolds the .NET 10 Aspire project structure, generates repository summaries and agent instructions, and concludes with a comprehensive debriefing report.

## 2. Project Context Summary
- **Technology Stack:** .NET 10, ASP.NET Core 10, Blazor WASM, .NET Aspire 10, Semantic Kernel, Qdrant (gRPC), PostgreSQL (pgvector), Redis, SignalR, Tailwind CSS.
- **Architecture:** 
  - **UI Layer:** Blazor WASM (Ambient zero-interaction UI)
  - **Brain API:** ASP.NET Core 10 (Semantic Kernel Hub)
  - **Knowledge Base:** Qdrant & PostgreSQL
  - **Real-Time Sync:** SignalR + Redis
  - **Orchestration:** .NET Aspire 10 (`ConvoContentBuddy.AppHost`)
- **Special Constraints:** 
  - Aerospace-Grade Resilience (Triple Modular Redundancy - TMR)
  - Ambient User Experience (zero-interaction UI)
  - High-Speed Semantic Matching (sub-500ms retrieval)
- **Repository Details:** The project will be structured as a .NET Aspire modular solution with multiple microservices.

## 3. Assignment Execution Plan

| Assignment | Goal | Key Acceptance Criteria | Project-Specific Notes | Prerequisites | Dependencies | Risks / Challenges | Events |
|---|---|---|---|---|---|---|---|
| **`init-existing-repository`** | Initiate the repository by setting up GitHub Project, labels, milestones, and renaming workspace files. | PR/branch created, Git Project linked, columns created, labels imported, workspace files renamed. | Update `name` in `.devcontainer/devcontainer.json` and verify workspace file name matches the repository (`convo-content-buddy-whiskey62`). | GitHub auth with repo/project scopes. | None | GitHub API rate limits or permission issues. | `post-assignment-complete` |
| **`create-app-plan`** | Create a detailed application plan in an issue based on the app spec. | Plan documented in issue, `tech-stack.md` and `architecture.md` created, milestones linked. | Focus on .NET 10 Aspire orchestration, Blazor WASM, and Semantic Kernel integration phases. | `init-existing-repository` | App spec in `plan_docs/` | Ensuring all architectural layers (UI, Brain, KB, Sync) are properly phased without writing code. | `pre-assignment-begin` (gather-context), `on-assignment-failure` (recover-from-error), `post-assignment-complete` |
| **`create-project-structure`** | Create the actual project structure and scaffolding based on the plan. | Solution structure created, Docker/Aspire config, CI/CD, README, initial commit. | Create `.sln` with `AppHost`, `ServiceDefaults`, `API.Brain`, `UI.Web`, and `Data.Seeder` projects. | `create-app-plan` | App plan issue/docs | Correctly scaffolding .NET Aspire 10 with Qdrant, Postgres, and Redis hosting components. | `post-assignment-complete` |
| **`create-repository-summary`** | Create `.ai-repository-summary.md` to onboard Copilot/agents. | File created at root, follows formatting standards, includes build/test instructions. | Document Aspire startup commands (`dotnet run --project ConvoContentBuddy.AppHost`), .NET 10 build steps, and project layout. | `create-project-structure` | Project structure | Keeping it under 32K tokens while covering all 5 microservices. | `post-assignment-complete` |
| **`create-agents-md-file`** | Create `AGENTS.md` for AI coding agents with context and instructions. | File exists at root, contains setup/build/test commands, code style, architecture notes. | Include Aspire run commands, Semantic Kernel notes, and TMR failover testing instructions. | `create-project-structure` | Project structure, `.ai-repository-summary.md` | Ensuring commands are validated and work in the devcontainer environment. | `post-assignment-complete` |
| **`debrief-and-document`** | Perform a debriefing session and document learnings in a structured report. | Report created in `.md`, all sections complete, trace saved, committed to repo. | Capture learnings about .NET 10 Aspire setup and any issues with Qdrant/Postgres integration. | All prior assignments | All prior outputs | Accurately capturing the execution trace and metrics for the entire workflow. | `post-assignment-complete` |

## 4. Sequencing Diagram

```text
[Start]
  |
  v
(pre-script-begin) create-workflow-plan
  |
  v
init-existing-repository
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  v
create-app-plan
  |--> (pre-assignment-begin: gather-context)
  |--> (on-assignment-failure: recover-from-error)
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  v
create-project-structure
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  v
create-repository-summary
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  v
create-agents-md-file
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  v
debrief-and-document
  |--> (post-assignment-complete: validate-assignment-completion, report-progress)
  |
  v
[End]
```

## 5. Open Questions
- Are there any specific GitHub Project column requirements beyond the standard (Not Started, In Progress, In Review, Done)?
- Should the `create-project-structure` assignment actually instantiate the Qdrant and PostgreSQL containers via Aspire, or just scaffold the AppHost references? (Assuming full Aspire scaffolding as per the spec).
- Do we have the necessary API keys (Gemini) available in the repository secrets for the CI/CD pipeline and local devcontainer?
