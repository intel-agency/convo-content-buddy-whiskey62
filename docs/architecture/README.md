# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records for ConvoContentBuddy.

## What is an ADR?

An ADR is a document that captures an important architectural decision made along with its context and consequences.

## ADR Index

| Number | Title | Status | Date |
|--------|-------|--------|------|
| 0001 | Use .NET Aspire for Service Orchestration | Accepted | 2026-03-12 |
| 0002 | Implement Triple Modular Redundancy (TMR) | Accepted | 2026-03-12 |
| 0003 | Hybrid Vector-Graph Retrieval Strategy | Accepted | 2026-03-12 |
| 0004 | Zero-Interaction UI with Blazor WASM | Accepted | 2026-03-12 |
| 0005 | Multi-Tier Failover with Safe Mode | Accepted | 2026-03-12 |

## Creating a New ADR

1. Copy the template: `cp 0000-template.md NNNN-short-title.md`
2. Fill in the sections
3. Submit with your pull request
4. Update this index

## ADR Template

```markdown
# ADR-NNNN: [Short Title]

## Status
[Proposed | Accepted | Deprecated | Superseded]

## Context
[What is the issue that we're seeing that is motivating this decision?]

## Decision
[What is the change that we're proposing and/or doing?]

## Consequences
[What becomes easier or more difficult to do because of this change?]
```

## References

- [Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- [ADR GitHub Organization](https://adr.github.io/)
