# 9. Architecture Decisions

One file per decision, numbered, never deleted. A superseded ADR stays and is marked as such —
the history of what you rejected is as valuable as what you chose.

## Format

```markdown
# ADR-00X — Title

**Status:** Proposed | Accepted | Superseded by ADR-00Y
**Date:** YYYY-MM-DD

## Context
What forces are at play? What makes this a real decision rather than an obvious one?

## Decision
What was chosen, stated plainly.

## Consequences
What becomes easier. What becomes harder. Be honest about the second.

## Alternatives considered
What else was on the table, and the specific reason each was rejected.
```

## Index

| ADR | Title | Status |
|---|---|---|
| 001 | Why polyglot | 🔴 to write |
| [002](002-contract-layout-and-generation.md) | Contract layout and code generation scope | ✅ Accepted |
| [003](003-gateway-translates-rest-to-grpc-by-hand.md) | The gateway translates REST to gRPC by hand | ✅ Accepted |
| [004](004-gateway-folders-by-feature.md) | The gateway groups code by feature, order-service by layer | ✅ Accepted |
