# ADR-006 — Optimistic concurrency using the Postgres xmin system column

**Status:** Accepted
**Date:** 2026-08-28

## Context

Two people can open the same order. One confirms it, the other cancels it. Without a
concurrency strategy the second write silently overwrites the first and nobody learns that
a decision was lost.

The `Order` aggregate is a natural fit for optimistic concurrency: conflicts on one order
are rare, orders are short-lived, and a rejected write can simply be retried against a
fresh read. Pessimistic locking would hold a row lock across a user's thinking time, which
turns a rare conflict into a routine queue.

The awkward part is where the version lives. `Domain/` references nothing — not EF Core,
not `building-blocks/` — and a version number is a persistence concern that the business
never asks about. Adding `public int Version { get; set; }` to the aggregate would put a
database mechanism in the middle of the model.

## Decision

**Postgres's own `xmin` system column is the concurrency token.** Every row already carries
it: it holds the id of the transaction that last wrote that row, and Postgres maintains it
without being asked.

It is mapped as a **shadow property** — declared in `OrderConfiguration`, invisible to the
`Order` class:

```csharp
builder.Property<uint>("xmin")
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

A conflict surfaces as `DbUpdateConcurrencyException`, which the API edge translates to
gRPC `ABORTED` — the status a client is expected to retry.

## Consequences

Easier: no version column, no migration to add one, no code that has to remember to
increment it. The token cannot drift out of step with the row, because Postgres owns it.

Easier: the domain stays clean. `Order` has no idea any of this exists, and the domain unit
tests still run with no database.

Harder: **it is Postgres-specific.** Moving order-service to SQL Server would mean adding a
`rowversion` column and a migration. That is an acceptable trade for a service whose
database choice is already deliberate.

Harder: **the scaffolded migration has to be edited by hand.** `dotnet ef migrations add`
emits `xmin = table.Column<uint>(...)` and `CREATE TABLE` cannot declare a system column, so
the line is removed with a comment explaining why. Npgsql 10 dropped the
`UseXminAsConcurrencyToken()` helper that used to hide this; the mapping still works, the
scaffolding does not. Anyone regenerating the initial migration must repeat the edit.

Verified: two `DbContext` instances read the same order, the first confirms and saves, the
second cancels and is rejected with `DbUpdateConcurrencyException`, and the stored status is
still the first writer's.

## Alternatives considered

**A `version` integer on the aggregate**, incremented by the domain. Rejected: it puts a
persistence mechanism in the model, and every aggregate method has to remember to bump it.

**A `version` shadow property that EF increments.** Workable and portable, but it needs a
real column and a migration to deliver exactly what `xmin` already provides for free.

**Pessimistic locking** (`SELECT ... FOR UPDATE`). Rejected: it holds a lock for as long as
a person takes to decide, converting a rare conflict into a routine wait, and it invites
deadlocks the moment a second aggregate joins the transaction.

**Last write wins** — no token at all. Rejected: that is the bug, not a strategy.
