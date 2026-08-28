# Event Catalog

> **The most important document in this repository.** Every Kafka topic is registered here
> *before* it exists in code. If it is not in this table, it must not be produced.
>
> Get this right and `proto/` writes itself. Get it wrong and you pay for it in month 3.

## How to fill this in

Work backwards from **questions the system must answer**, not forwards from services you
plan to build. For each row, force yourself to answer all six columns — if you cannot name
a consumer, you probably do not need the event yet.

The **partition key** column is the one people skip and later regret. The key decides
ordering: all events sharing a key are processed in order, events with different keys are not.
Choose the thing whose ordering actually matters, then check it does not create a hot partition.

## Naming

| Kind | Pattern | Example |
|---|---|---|
| Topic | `<context>.<aggregate>.v<n>` | `ordering.order.v1` |
| Event type | Past tense, always | `OrderPlaced`, never `PlaceOrder` |
| Master data topic | `<context>.<entity>.master.v<n>` | `catalog.product.master.v1` |

## Retention

| Retention | Use for | Because |
|---|---|---|
| **Time-based** (default) | Events — things that happened | Replaying history is meaningful |
| **Compacted** | Master data — things that are | You want the current value per key, not every past version |

---

## Catalog

| Topic | Event | Producer | Consumers | Partition key | Retention |
|---|---|---|---|---|---|
| `ordering.order.v1` | `OrderPlaced` | order-service | search-service (reindex), stream-processor (revenue), inventory-service | `order_id` — every event for one order must stay ordered. **Not** `customer_id`: high-volume customers create hot partitions. | 30 days |
| `catalog.product.master.v1` | `ProductUpserted` | data-platform (batch) | catalog-service, search-service, order-service (read model), stream-processor (broadcast state) | `product_id` — one current row per product | **Compacted** |
| | | | | | |
| | | | | | |

_TODO — the two rows above are worked examples showing the reasoning expected in each cell.
Add the rest yourself. Expect roughly 8–12 events for the whole system; if you are heading
past 20, some of them are probably commands wearing an event's name._

## Events deliberately NOT created

Recording what you rejected is as useful as what you kept.

| Considered | Why not |
|---|---|
| `OrderViewed` | Nothing acts on it. Analytics can come from clickstream. |
| | |
