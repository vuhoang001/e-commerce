# 4. Solution Strategy

> **What belongs here:** the handful of decisions that shape everything else, in one page.
> Detail goes in ADRs (chapter 09); this is the summary a reader needs first.

_TODO. Likely candidates:_

- _Services split by bounded context, communicating over gRPC and Kafka_
- _Contract-first: `proto/` is the single source of truth_
- _Two data pipelines — streaming for facts, batch for master data_
- _Polyglot, with a stated technical reason per language_
