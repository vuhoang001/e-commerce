# ADR-005 — Money is a decimal amount plus a currency, rounded away from zero at two places

**Status:** Accepted
**Date:** 2026-08-28

## Context

Money now exists in three representations, and they have to agree.

| Where | Shape | Why that shape |
|---|---|---|
| `proto/common/v1/money.proto` | `currency_code` + `units` + `nanos` | Exact across languages, no floating point on the wire |
| `Domain/Orders/Money.cs` | `decimal` + ISO 4217 code | .NET's exact decimal type, natural to compute with |
| `api-gateway` `MoneyResponse` | `decimal` + currency | What a client renders |

Rounding has to happen somewhere. A tax rate divides: 10% of 37.98 is 3.798, which is not
a payable amount in any currency with two decimal places. Left undefined, each layer would
round differently and the totals would disagree — the classic bug where an invoice's lines
do not add up to its total.

The first hard-coded seed in order-service illustrated it: it claimed a total of 41.97 for
two items at 18.99 with 10% tax, which is neither 37.98 nor 41.78. Nothing computed it, so
nothing caught it.

## Decision

**In the domain, money is an exact `decimal` plus an ISO 4217 code**, and there is no
constructor that takes an amount without a currency.

**Arithmetic across currencies throws.** `Money.Of(1, "USD") + Money.Of(1, "EUR")` is a
`DomainException`, not an implicit conversion at some guessed rate.

**Tax is rounded per line**, to two decimal places, `MidpointRounding.AwayFromZero`. The
order total is the sum of already-rounded lines.

**Money cannot be negative.** A refund is its own concept when it arrives, not a negative
order line.

## Consequences

Easier: a line total is checkable by hand. A customer adding up the lines on their invoice
gets the number at the bottom, because that is literally how it is computed.

Easier: the total cannot drift from its lines. `Order.Total` is computed on read and has no
setter, so there is no way to store a total that disagrees with the items.

Harder: **two decimal places is wrong for some currencies.** It is right for USD, EUR and
GBP; JPY has no minor unit and KWD has three. This service is single-currency until it is
not, and a second currency requires a currency-exponent table before `Money.Round()` can be
trusted. That is a known debt, recorded here rather than discovered later.

Harder: rounding per line means many small roundings rather than one. For an order with
fifty lines the accumulated difference from rounding the grand total instead can reach a
few pence. That is the correct trade: the invoice has to be internally consistent, and
"the lines do not add up" is a support call, whereas a penny against a theoretical
un-rounded total is not.

## Alternatives considered

**`double`.** Rejected outright: 0.1 + 0.2 != 0.3 in binary floating point, and an order is
a permanent financial record.

**Minor units as an `int64`** — cents, as payment providers use. Rejected for the domain:
converting to a displayable amount needs each currency's exponent, so the representation
cannot be interpreted without an external table. The wire format keeps `units` + `nanos`
for the same reason, which is self-describing.

**Round only the grand total, keeping lines exact.** Rejected: the lines shown to the
customer would not sum to the total shown to the customer.

**Banker's rounding** (`MidpointRounding.ToEven`), .NET's default. Rejected for prices:
away-from-zero is the ordinary commercial convention and the one a customer checking the
arithmetic by hand will expect. Banker's rounding is the better choice for statistical
aggregates, which is not what this is.
