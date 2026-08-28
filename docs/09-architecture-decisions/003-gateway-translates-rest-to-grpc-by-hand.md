# ADR-003 — The gateway translates REST to gRPC by hand

**Status:** Accepted
**Date:** 2026-08-28

## Context

The browser cannot speak gRPC, so something has to translate. The obvious candidate is
the gateway, and the gateway is C# with YARP — but **YARP does not translate REST to
gRPC**. It is an HTTP-to-HTTP reverse proxy that knows nothing about Protobuf. Choosing
C# over Go for the gateway silently gave up `grpc-gateway`, which generates that
translation from `google.api.http` annotations. PLAN.md section 3b sets out the problem
and flags this decision as one to record.

## Decision

**The gateway holds gRPC clients and exposes REST through hand-written Minimal API
endpoints** — option B in PLAN.md section 3b.

The public JSON shape is a separate set of records (`Contracts/OrderDto.cs`), not the
protobuf types serialised directly.

**YARP is not referenced yet.** Nothing needs passing through until catalog-service
arrives in month 2 with its own `grpc-gateway`.

## Consequences

Easier: the gateway is a real backend-for-frontend rather than a dumb proxy. In month 3
`GET /api/products/{id}` can fan out to search, recommendation and inventory in parallel
and merge the results, instead of the browser making three calls.

Easier: contract breakage is a compile error. Change a field in `proto/` and the gateway
stops building. With `grpc-gateway` the same mistake surfaces at runtime.

Easier: the public contract stops leaking internals. Money reaches a client as
`{"amount": 18.99, "currency": "USD"}` rather than units-plus-nanos, a tax rate as
`10` percent rather than `1000` basis points, and status as `"confirmed"` rather than
`ORDER_STATUS_CONFIRMED`. Those three representations exist for wire precision and are
no use to a browser.

Harder: every public endpoint is written by hand, along with its mapping and its status
translation. This is tolerable only because the number of genuinely public endpoints is
far smaller than the number of RPCs; if that stops being true, revisit.

Harder: the mapping is a second place to update when the contract grows. `OrderStatus`
mapping throws on an unhandled value rather than falling back, so the omission fails at
the gateway rather than reaching a client as a raw protobuf name.

Accepted cost of C# over Go for the gateway: roughly 110 MB of image against 15 MB,
200–400 ms of cold start against under 50 ms, 60 MB idle against 10 MB. Those numbers
start mattering at dozens of instances. At this project's size, sharing one logging,
tracing and auth stack with order-service and payment-service is worth more.

## Alternatives considered

**gRPC JSON transcoding in each service** (`Microsoft.AspNetCore.Grpc.JsonTranscoding`),
with YARP merely routing. Rejected: it is .NET-only, and catalog-service and
inventory-service are Go. It would mean two parallel translation mechanisms, which is
worse than one hand-written layer.

**Envoy in front for gRPC-JSON transcoding, YARP behind for auth.** Rejected: two proxy
layers is complexity this project has not earned.

**Serialising the protobuf types straight to JSON**, skipping the DTOs. Rejected: it
publishes units-plus-nanos and basis points as the public API, and welds the external
contract to the internal one so neither can change without the other.
