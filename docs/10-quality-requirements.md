# 10. Quality Requirements

> **What belongs here:** quality goals made measurable. A goal without a number is a wish.

| Quality | Scenario | Target |
|---|---|---|
| Latency | Product search, p99, under normal load | < 50 ms |
| Correctness | A TaskManager is killed mid-run | Revenue totals unchanged |
| Availability | catalog-service is down | Checkout still succeeds |
| Recoverability | order-service killed mid-publish | No event lost, none duplicated |

_TODO — extend, and record which ones have actually been verified._
