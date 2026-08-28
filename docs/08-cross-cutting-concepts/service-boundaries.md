# Service Boundaries

> Write this **before** the event catalog. It is the shortest document in the repository and
> the one that determines whether months 2–4 go smoothly.
>
> The column that does the work is the last one. Anyone can list what a service owns; stating
> what it is **forbidden to know** is what actually prevents a distributed monolith.

## How to fill this in

For each service, answer three questions:

1. **What data does it own?** — the data for which it is the single source of truth
2. **What does it need from others?** — and via which channel, gRPC or Kafka
3. **What must it never know?** — the rule that, if broken, means the boundary was wrong

A useful test: if two services would need to change together for one feature, the boundary
between them is probably in the wrong place.

---

| Service | Owns | Needs from others | Must never know |
|---|---|---|---|
| **order-service** | Orders, order items, saga state | Product price/name at checkout (own read model, fed by Kafka); stock reservation (gRPC → inventory) | How stock is stored. How payment is processed. It knows only whether each succeeded. |
| **inventory-service** | Stock levels, reservations | Product existence (Kafka) | Anything about orders. It reserves against a SKU; why is not its concern. |
| | | | |
| | | | |

_TODO — complete for every service. The two rows above show the level of specificity expected;
"needs product data" is not an answer, "needs price and name at checkout, via its own read model"
is._

## Data ownership rules

1. One service owns each piece of data. No shared tables, no shared schema.
2. Other services hold **projections** — copies built from published events. A projection is not
   shared ownership; the owner still decides what the truth is.
3. No service queries another service's database. Ever. `tests/arch/` enforces this.

## Known boundary tensions

_TODO — record the places you were unsure. These are the interesting parts, and they are what
an interviewer will probe._

| Tension | Current decision | Revisit if |
|---|---|---|
| | | |
