# e-commerce

An online shop, built as a set of small independent services rather than one large program.
Every order placed, item searched, and stock level changed becomes an event the rest of the
system reacts to — including a pipeline that turns those events into live numbers as they happen.

> **Status:** month 1 — the order domain. Two of the seven services exist, with orders
> persisted in Postgres behind an aggregate; the rest is still on paper. See [Running it](#running-it).

## What it will do

- Browse and search a product catalogue
- Add items to a basket and place an order
- Take that order through **reserve stock → take payment → confirm**, and unwind it cleanly
  if any step fails
- Show a live dashboard — revenue by the minute, suspicious activity, what people are
  looking at right now
- Suggest related products based on what a shopper has viewed

## How it's put together

Seven services, each owning its own data and talking to the others over well-defined contracts.
Different languages are used where each one genuinely fits:

| Service | Language | What it handles |
|---|---|---|
| `api-gateway` | C# | The single front door — routing, sign-in, rate limits |
| `order-service` | C# | Order lifecycle and the logic that keeps it consistent |
| `payment-service` | C# | Taking payment as part of the order flow |
| `catalog-service` | Go | Product data and fast search |
| `inventory-service` | Go | Stock levels when many buyers want the same item |
| `recommendation-service` | Python | Similarity and suggestions |
| `stream-processor` | Java | Turning the event stream into live figures |

Services ask each other questions over **gRPC** and announce completed facts over **Kafka**.
Both are defined in `proto/` first, so no service can change its interface without the change
being visible and checked.

## Why build it this way

Plenty of sample shops already show basic create-read-update-delete over a few services.
This one focuses on the parts those samples usually leave out:

- Reading changes straight out of the database log, so no event is ever lost between
  saving an order and publishing it
- Message schemas that are versioned and checked automatically — a change that would break
  another service fails the build instead of production
- Continuous processing over the event stream: counting revenue in time windows, spotting
  unusual behaviour, grouping a visitor's activity into sessions
- One request traceable end to end, across four languages and across the message broker,
  as a single unbroken timeline

## The plan

The full build plan — six months, phase by phase, each with a concrete "done when" test —
is in **[PLAN.md](./PLAN.md)**. It covers the repository layout, why each language was chosen,
when to use gRPC versus Kafka, the local development workflow, continuous integration across
four toolchains, known pitfalls, and an explicit list of what is deliberately *not* being built.

Architecture documentation follows the [arc42](https://arc42.org/) template. Tool versions are
pinned with [mise](https://mise.jdx.dev/), and architectural rules are enforced by automated
tests rather than left to discipline.

## Roadmap

| Phase | Focus | Status |
|---|---|---|
| 0.5 | Skeleton — one request travelling the full path | ◐ |
| 1 | Core order logic | ◐ |
| 2 | Go services + the event backbone | ☐ |
| 3 | Multi-step order flow + recommendations | ☐ |
| 4 | Stream processing | ☐ |
| 5 | Live dashboard + end-to-end tracing | ☐ |
| 6 | Failure testing, deployment, documentation | ☐ |

## Running it

`make up` is the intended entry point, but `compose.services.yml` is not written yet. Until
then, start the infrastructure and run the services from source:

```sh
make setup                 # once, after cloning — pins every tool version
make infra                 # Postgres
make db-update             # apply migrations
make run S=order-service   # terminal 1 — gRPC on :5001
make run S=api-gateway     # terminal 2 — REST on :8080
```

Place an order:

```sh
make call S=order-service M=rpc.order.v1.OrderService/PlaceOrder D='{
  "customer_id":"11111111-1111-1111-1111-111111111111",
  "shipping_address":{"line1":"221B Baker Street","city":"London","postal_code":"NW1 6XE","country":"GB"},
  "lines":[{"product_id":"product-7","product_name":"Cafetiere, 1 litre","sku":"KIT-CAF-1L",
            "unit_price":{"currency_code":"USD","units":"18","nanos":990000000},
            "tax_rate_basis_points":1000,"quantity":2}]}'
```

then read it back through the gateway with the id it returns:

```sh
curl localhost:8080/api/orders/<id>
```

```json
{ "orderId": "…", "status": "pending",
  "items": [ { "sku": "KIT-CAF-1L", "productName": "Cafetiere, 1 litre",
               "unitPrice": { "amount": 18.99, "currency": "USD" },
               "taxRatePercent": 10, "quantity": 2 } ],
  "total": { "amount": 41.78, "currency": "USD" } }
```

The total is computed by the `Order` aggregate from its lines, not stored, so it cannot
disagree with the items. `make call` talks to a gRPC service directly, bypassing the
gateway; `make test` runs the domain tests and `make arch` the boundary rules.

## Project conventions

- **Language: English** — documentation, code comments, commit messages, and identifiers.
- **Contracts before code** — `proto/` is the single source of truth, checked on every pull request.
- **Boundaries are enforced, not documented** — architecture tests run in CI.
- **Tool versions are pinned** — `mise install` sets up all four languages correctly.

## License

MIT
