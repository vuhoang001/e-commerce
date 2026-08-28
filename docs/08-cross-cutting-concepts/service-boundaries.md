# Service Boundaries

> **Status:** draft — three open questions marked ⚠️ below need deciding before this is final.

The column that does the work is the last one. Anyone can list what a service owns; stating what it
is **forbidden to know** is what actually prevents a distributed monolith. Every rule in that column
is enforceable by `tests/arch/`, and most of them are.

A useful test throughout: **if two services must change together to ship one feature, the boundary
between them is in the wrong place.**

---

## The services

### api-gateway `[C#]`

| | |
|---|---|
| **Owns** | Nothing durable. Route configuration, rate-limit counters (Redis), and the trace origin. |
| **Needs** | Every service, over gRPC. The identity provider, for token validation. |
| **Must never know** | **Any business rule.** It must not decide whether an order can be cancelled — it asks order-service and relays the answer. It must not know a single database schema. If a change to business logic requires touching the gateway, the logic is in the wrong place. |

### order-service `[C#]`

| | |
|---|---|
| **Owns** | Orders · order items (snapshots, see PLAN.md §18) · saga state · idempotency keys |
| **Projects** | A product read model, built from `catalog.product.master.v1`. A projection is not ownership — catalog-service still decides what the truth is. |
| **Needs** | Stock reservation (gRPC → inventory) · payment outcome (Kafka ← payment) · product price and name (its own read model, never a live call) |
| **Must never know** | How stock is stored or reserved internally — only whether a reservation succeeded. How payment is processed — only whether it succeeded. How products are indexed for search. It never queries another service's database. |

### payment-service `[C#]`

| | |
|---|---|
| **Owns** | Payment attempts · transaction records · payment outcomes |
| **Needs** | An order id and an amount, from `ordering.order.v1` |
| **Must never know** | **What was bought.** It charges an amount against an order id. Line items, product names and quantities are none of its business. If payment-service ever needs a product name, something upstream has gone wrong. |

> This is the sharpest boundary in the system, and the best one to cite in an interview. Most
> people's instinct is to send the whole order to payment. Resisting that is the point.

### catalog-service `[Go]`

| | |
|---|---|
| **Owns** | The serving copy of product data · the search index (OpenSearch) |
| **Projects** | Product master data from `catalog.product.master.v1`. ⚠️ **The system of record is the upstream source system, not this service** — see open question 1. |
| **Needs** | The compacted master-data topic. Nothing else. |
| **Must never know** | Orders. Stock levels. Prices actually paid — it knows *list* price, never *transaction* price. Whether a product sells well: that is analytics, and it lives in ClickHouse. |

### inventory-service `[Go]`

| | |
|---|---|
| **Owns** | Stock level per SKU · reservations · reservation expiry |
| **Needs** | Valid SKUs, from the master-data topic |
| **Must never know** | Anything about orders — no customer, no price, no order total. It reserves quantity against a SKU and a reservation id. *Why* the reservation exists is not its concern. |

### recommendation-service `[Python]` — *conditional, see PLAN.md §17*

| | |
|---|---|
| **Owns** | Product embeddings · interaction features · the ANN index |
| **Needs** | Product data, clickstream and order events — all via Kafka |
| **Must never know** | Any PII beyond a pseudonymous user id. It must be structurally unable to identify a person. It is also **never on the checkout path** — a recommendation timing out must never delay a purchase. |

### stream-processor `[Java / Flink]`

| | |
|---|---|
| **Owns** | Its checkpoints and keyed state · its output tables in ClickHouse and Iceberg |
| **Needs** | Every event topic, plus the master-data topic for broadcast state |
| **Must never know** | **How to write back into the operational plane.** It is strictly read-only with respect to services. Output goes to ClickHouse, Iceberg, or an alert topic — never into a service's database, and never as a gRPC call to a service. |

> Violating that last rule is how an analytics job ends up silently mutating production state. Worth
> an explicit architecture test.

### data-platform `[Python]` — not a service, but it has boundaries

| | |
|---|---|
| **Owns** | Bronze, silver and gold tables · the `catalog.product.master.v1` topic |
| **Needs** | Upstream source systems |
| **Must never know** | Anything about service internals. It never writes to a service database. Its only output into the operational plane is a published, versioned Kafka topic. |

---

## Data ownership rules

1. **One owner per fact.** No shared tables, no shared schema, no shared database user.
2. **Everyone else holds projections.** A projection is a derived copy, rebuilt from published
   events. Holding a copy is not co-ownership; the owner still defines the truth.
3. **No service queries another service's database.** Ever. `tests/arch/` enforces this.
4. **Where authority over price lives:**
   | Kind of price | Authoritative owner |
   |---|---|
   | List price | catalog-service (projected from upstream) |
   | Agreed price | order-service, frozen in `OrderItem` |
   | Cost price and margin | Analytics only — never enters the operational plane |

---

## Known boundary tensions

The interesting part, and what an interviewer will probe.

| # | Tension | Current decision | Revisit if |
|---|---|---|---|
| 1 | **Two projections of the same product data** — catalog-service and order-service both hold one | Deliberate. order-service's copy exists so checkout survives catalog being down (PLAN.md §18). | The two ever disagree in a way that matters. Then one becomes authoritative and the other queries it. |
| 2 | **Reservation expiry vs saga timeout** — two independent timers about the same order | Inventory's expiry must be **longer** than the saga's timeout, or stock is released while the saga still believes it holds it | Either timeout changes. They must be reasoned about together, and the values documented in one place. |
| 3 | **catalog-service owns a projection, not a system of record** | Accepted. Upstream is the source of truth; catalog serves a copy. | Anyone asks for in-app product editing. That would make catalog authoritative and change the whole flow (see open question 1). |
| 4 | **stream-processor reads everything** | Accepted — it is read-only, so wide read access carries no coupling risk | It ever needs to write back. That would be a redesign, not a tweak. |
| 5 | **recommendation-service may be cut** | Section 17 proposes cutting it to fund backend depth | You decide to keep it. Its boundaries above stay valid either way. |

---

## ⚠️ Open questions — need deciding

| # | Question | Why it matters | Suggested answer |
|---|---|---|---|
| 1 | **Can products be edited inside the app, or only via upstream batch?** | If yes, catalog-service becomes a system of record with its own write path, and the batch pipeline needs conflict resolution. That is significantly more work. | **No in-app editing.** Upstream stays authoritative. Keeps the batch pipeline one-directional and the boundary clean. |
| 2 | **How long does a stock reservation live?** | It must exceed the saga timeout (tension 2), and it bounds how long a failed payment holds stock hostage. | **15 minutes**, with a saga timeout of 10. Write both in one place so they cannot drift. |
| 3 | **Does payment-service store payment methods at all?** | Storing card tokens brings real compliance weight, even simulated. | **No stored methods.** Each payment is self-contained. Removes a whole class of concern for zero learning cost. |

Once these three are settled, delete this section and fold the answers into the tables above.
