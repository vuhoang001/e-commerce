# ADR-002 — Contract layout and code generation scope

**Status:** Accepted
**Date:** 2026-08-28

## Context

`proto/` is the single source of truth for every contract in a repository that will
eventually hold four languages. Three choices had to be made before the first `.proto`
file existed, and all three are expensive to reverse once services depend on them.

1. **How many buf modules, and therefore what a package is called.** `buf lint`'s
   `PACKAGE_DIRECTORY_MATCH` rule ties a file's package to its path relative to the
   module root, so the module layout decides the package name, which decides the
   generated namespace in every language.
2. **Which languages to generate.** PLAN.md section 4 specifies a `buf.gen.yaml` with
   plugins for C#, Go, Python and Java. Only one C# service exists in month 0.5; the
   Go services arrive in month 2, Python in month 3, Java in month 4.
3. **How to represent money and tax.** The `proto-contract` skill forbids a bare
   `double` for money but does not say what to use instead.

## Decision

**One module, rooted at `proto/`.** Packages therefore mirror the full path:
`common.v1`, `rpc.order.v1`, `events.order.v1`.

**Generate C# only for now.** The Go, Python and Java plugin blocks are present in
`proto/buf.gen.yaml` but commented out, each labelled with the month it is enabled.

**Money is `common.v1.Money`** — `currency_code` / `units` / `nanos`, field-for-field
identical to `google.type.Money`. **Tax rates are integer basis points**, not a rate.

## Consequences

Easier: the `rpc.` and `events.` segments survive into generated code, so an integration
event and a request type land in different namespaces (`Ecommerce.Rpc.Order.V1` versus
`Ecommerce.Events.Order.V1`) and cannot be confused. The `ddd-dotnet` skill warns against
exactly that conflation; the namespace now enforces it rather than asking politely.

Easier: no generated code exists that nothing compiles against. Dead stubs still have to
pass review, still appear in diffs, and still break the build when a plugin version moves.

Harder: package names are longer than the conventional `myorg.order.v1`, and the `rpc.`
segment is unusual to anyone who has not read this ADR.

Harder: enabling a language later is an extra step that is easy to forget. The mitigation
is that its service cannot compile without it, so the failure is immediate and obvious.

Money as `units` + `nanos` costs a conversion at every C# boundary (`decimal` does not map
directly) but is exact, self-describing about its currency, and holds nine decimal places —
enough for a unit price that a tax or discount rate has divided.

## Alternatives considered

**A module per top-level directory** (`proto/rpc`, `proto/events`, `proto/common`), giving
the shorter package `order.v1`. Rejected: `rpc/order/v1` and `events/order/v1` would then
*both* be package `order.v1`, which is legal protobuf and thoroughly misleading — the
distinction the `proto-contract` skill is built around would vanish from the generated code.

**Generate all four languages now**, as PLAN.md section 4 shows. Rejected for the dead-code
reason above. This ADR is the record that the deviation was deliberate; PLAN.md section 4
remains correct as the end state.

**Money as minor units** (an `int64` of cents, as payment providers use). Rejected: converting
to a decimal amount requires knowing each currency's exponent — JPY has none, USD has two —
which means an external table the contract does not carry.

**Tax rate as `double`**. Rejected for the same reason money is not a double: a rate multiplies
money, so its rounding error becomes the order total's rounding error.
