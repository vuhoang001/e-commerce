# ecommerce-polyglot — Kế hoạch xây dựng

> Hệ thống e-commerce microservices **đa ngôn ngữ**: C# (nghiệp vụ) · Go (throughput) · Python (AI/data) · Java (streaming).
> Giao tiếp **contract-first** qua gRPC (sync) và Kafka (async).
>
> - **Thời lượng:** 6 tháng
> - **Mục tiêu kép:** kiến trúc microservices tử tế + portfolio cho vị trí Data Engineer (Streaming)
> - **Ngày lập:** 2026-08-28
> - *Thay thế bản kế hoạch trước tại `~/flowmart/PLAN.md`*

---

## Mục lục

1. [Đánh giá cấu trúc bạn đề xuất](#1-đánh-giá-cấu-trúc-bạn-đề-xuất)
2. [Cấu trúc hoàn chỉnh](#2-cấu-trúc-hoàn-chỉnh)
3. [Phân chia service — ngôn ngữ nào, vì sao](#3-phân-chia-service--ngôn-ngữ-nào-vì-sao)
3b. [Gateway C# + YARP — làm được gì, không làm được gì](#3b-gateway-c--yarp--làm-được-gì-không-làm-được-gì)
4. [Contract-first workflow](#4-contract-first-workflow)
5. [Quy tắc giao tiếp: gRPC hay Kafka](#5-quy-tắc-giao-tiếp-grpc-hay-kafka)
6. [Trải nghiệm dev local](#6-trải-nghiệm-dev-local)
7. [Observability xuyên ngôn ngữ](#7-observability-xuyên-ngôn-ngữ)
8. [Lộ trình 6 tháng](#8-lộ-trình-6-tháng)
9. [CI/CD cho monorepo đa ngôn ngữ](#9-cicd-cho-monorepo-đa-ngôn-ngữ)
10. [Cạm bẫy đã biết](#10-cạm-bẫy-đã-biết)
11. [Phạm vi loại trừ](#11-phạm-vi-loại-trừ)
12. [Bản đồ phỏng vấn](#12-bản-đồ-phỏng-vấn)
13. [Theo dõi tiến độ](#13-theo-dõi-tiến-độ)
14. [Đối chiếu với BookWorm](#14-đối-chiếu-với-bookworm)
15. [Agent skills — ghim hướng dẫn AI vào repo](#15-agent-skills--ghim-hướng-dẫn-ai-vào-repo)

---

## 1. Đánh giá cấu trúc bạn đề xuất

### Đúng rồi, giữ nguyên

| Điểm | Vì sao đúng |
|---|---|
| `proto/` ở **gốc repo** | Contract là tài sản chung, không thuộc service nào. Đặt ở gốc là quyết định đúng — nó buộc mọi thay đổi contract phải hiển thị rõ trong PR. |
| Mỗi service có `Dockerfile` riêng | Bắt buộc với polyglot. Mỗi ngôn ngữ có base image và build stage khác nhau. |
| `building-blocks/` | Tên hay (mượn từ eShopOnContainers). Đây là nơi chống lặp code hạ tầng. |
| Cấu trúc *nội bộ* khác nhau theo ngôn ngữ | Đúng. `Domain/Application/Infrastructure` cho C#, `cmd/internal` cho Go — **đừng ép chung một khuôn**, mỗi cộng đồng có convention riêng. |

### Sáu chỗ cần sửa

| # | Vấn đề | Sửa thế nào |
|---|---|---|
| 1 | **`src/services/order-service/src/`** — lồng `src` hai lần | Bỏ `src/` ngoài cùng. Top level thành `services/`, `proto/`, `building-blocks/`. Đường dẫn ngắn đi một tầng. |
| 2 | **Go: có cả `cmd/` lẫn `main.go` ở gốc** | Mâu thuẫn convention. Chọn `cmd/server/main.go`, gốc service không có `main.go`. |
| 3 | **Chưa có chỗ chứa code sinh ra từ proto** | `buf generate` phải đổ vào đâu đó. → `building-blocks/gen/{csharp,go,python,java}/` |
| 4 | **Chỉ có gRPC, không có event schema** | E-commerce **bắt buộc** có async. Thêm `proto/events/` cho Kafka message. |
| 5 | **Thiếu `deploy/`, `docs/`, `tests/`, `tools/`** | Không có thư mục ADR thì không ai biết bạn đã cân nhắc gì. Không có `tests/e2e/` thì không chứng minh được hệ thống chạy thật. |
| 6 | **Một `docker-compose.yml` cho tất cả** | Sẽ đau ở tháng thứ 2. Tách: `compose.infra.yml` (Kafka, Postgres, Redis...) chạy 1 lần rồi để đó, `compose.services.yml` restart liên tục. |

### Một câu hỏi kiến trúc còn thiếu

Bạn có **3 service và một web client**. Client sẽ gọi thẳng 3 service, hay qua một cửa?
→ Cần thêm **`api-gateway`** (C# / .NET + YARP). Không có nó thì trình duyệt phải nói gRPC (không làm được trực tiếp), phải xử lý CORS 3 lần, và auth phải làm 3 lần.

> ⚠️ **Lưu ý kỹ thuật quan trọng — xem [mục 3b](#3b-gateway-c--yarp-yarp-làm-được-gì-và-không-làm-được-gì).** YARP **không** dịch REST ↔ gRPC. Nó là reverse proxy HTTP. Việc dịch phải làm bằng cách khác, và điều đó thay đổi vai trò của gateway.

---

## 2. Cấu trúc hoàn chỉnh

```
ecommerce-polyglot/
├── .agents/
│   └── skills/                     # ★ hướng dẫn cho AI coding agent — xem mục 15
│       ├── kafka-conventions/      #   tự viết
│       ├── proto-contract/         #   tự viết
│       ├── flink-job/              #   tự viết
│       ├── go-service/             #   tự viết
│       ├── ddd-dotnet/             #   tự viết
│       ├── aspire/                 #   vendor từ Microsoft
│       └── csharp-tunit/           #   vendor
├── AGENTS.md                       # trỏ mọi agent về CLAUDE.md
├── CLAUDE.md                       # quy tắc chung: build/test/lint bằng lệnh nào
├── mise.toml                       # ★ PIN phiên bản .NET / Go / Python / Java / buf
├── Makefile                        # điểm vào duy nhất: make up / make proto / make test
├── compose.infra.yml               # Kafka, Postgres, Redis, OpenSearch, MinIO, OTel
├── compose.services.yml            # 6 service + gateway
├── .env.example
├── .editorconfig
│
├── proto/                          # ★ NGUỒN SỰ THẬT DUY NHẤT cho mọi contract
│   ├── buf.yaml                    # lint + breaking-change rules
│   ├── buf.gen.yaml                # sinh code cho 4 ngôn ngữ
│   ├── rpc/                        # ── gRPC: request/response ──
│   │   ├── order/v1/order_service.proto
│   │   ├── search/v1/search_service.proto
│   │   ├── recommendation/v1/recommendation_service.proto
│   │   └── inventory/v1/inventory_service.proto
│   ├── events/                     # ── Kafka: sự kiện đã xảy ra ──
│   │   ├── order/v1/order_placed.proto
│   │   ├── order/v1/order_cancelled.proto
│   │   ├── catalog/v1/product_updated.proto
│   │   └── user/v1/click_recorded.proto
│   └── common/v1/
│       ├── money.proto
│       └── pagination.proto
│
├── services/
│   ├── order-service/              # [C# / .NET 10]  DDD, transaction, saga
│   │   ├── src/
│   │   │   ├── Domain/             # aggregate, value object — 0 dependency
│   │   │   ├── Application/        # command/query handler, MediatR
│   │   │   ├── Infrastructure/     # EF Core, Kafka producer, outbox
│   │   │   └── Api/                # gRPC service impl + host
│   │   ├── tests/
│   │   │   ├── Domain.UnitTests/
│   │   │   └── Api.IntegrationTests/
│   │   ├── migrations/
│   │   └── Dockerfile
│   │
│   ├── payment-service/            # [C# / .NET 10]  saga participant
│   │   └── ... (như trên, gọn hơn)
│   │
│   ├── search-service/             # [Go]  low latency, high QPS
│   │   ├── cmd/server/main.go
│   │   ├── internal/
│   │   │   ├── handler/            # gRPC handler
│   │   │   ├── indexer/            # consume Kafka → OpenSearch
│   │   │   ├── search/             # query builder, ranking
│   │   │   └── config/
│   │   ├── go.mod
│   │   └── Dockerfile
│   │
│   ├── inventory-service/          # [Go]  reserve/release stock, tối ưu concurrency
│   │   ├── cmd/server/main.go
│   │   ├── internal/
│   │   └── Dockerfile
│   │
│   ├── recommendation-service/     # [Python 3.12]  AI / embeddings
│   │   ├── app/
│   │   │   ├── grpc_server.py
│   │   │   ├── models/             # embedding, ANN index
│   │   │   ├── pipelines/          # batch retrain
│   │   │   └── consumers/          # Kafka → cập nhật feature
│   │   ├── pyproject.toml
│   │   └── Dockerfile
│   │
│   ├── stream-processor/           # [Java 21 + Flink 2.x]  ← thêm ở tháng 4
│   │   ├── jobs/
│   │   │   ├── revenue-rollup/
│   │   │   ├── fraud-detection/
│   │   │   └── sessionization/
│   │   ├── pom.xml
│   │   └── Dockerfile
│   │
│   └── api-gateway/                # [C# / .NET 10 + YARP]  BFF, auth, rate limit
│       ├── src/
│       │   ├── Endpoints/          # Minimal API — dịch REST → gRPC client
│       │   ├── Clients/            # gRPC client sinh từ proto
│       │   ├── Middleware/         # auth, rate limit, correlation id
│       │   └── Program.cs          # YARP routes + OTel
│       ├── appsettings.json        # YARP ReverseProxy config
│       ├── tests/
│       └── Dockerfile
│
├── building-blocks/
│   ├── gen/                        # ★ code sinh từ proto — KHÔNG sửa tay
│   │   ├── csharp/  go/  python/  java/
│   │
│   ├── chassis-dotnet/             # ★ microservices chassis (C#)
│   │   ├── Cqrs/                   #   MediatR + pipeline behaviors
│   │   ├── Endpoints/              #   Minimal API convention, versioning
│   │   ├── EventBus/               #   Kafka producer/consumer, outbox, DLQ
│   │   ├── Persistence/            #   EF Core base, UnitOfWork, soft delete
│   │   ├── Caching/                #   Redis, cache-aside
│   │   ├── Security/               #   JWT, authorization policy
│   │   ├── Observability/          #   OTel tracing, metrics, structured logging
│   │   ├── Exceptions/             #   ProblemDetails, exception filter
│   │   ├── Validation/             #   FluentValidation convention
│   │   └── Resilience/             #   Polly: retry, circuit breaker, timeout
│   │
│   ├── chassis-go/                 # cùng 10 nhóm, viết theo cách Go
│   │   ├── kafka/  otel/  httpx/  config/  logx/  resilience/
│   │
│   ├── chassis-python/
│   │   └── kafka/  otel/  config/
│   │
│   └── scripts/
│       ├── gen-proto.sh
│       ├── init-databases.sh
│       └── seed-data.py
│
├── deploy/
│   ├── k8s/
│   ├── connectors/                 # Debezium config
│   └── otel/                       # OpenTelemetry Collector config
│
├── tests/
│   ├── e2e/                        # k6 hoặc Playwright — chạy qua gateway
│   └── arch/                       # ★ architecture test — ép ranh giới, chạy trong CI
│       ├── dotnet/                 #   NetArchTest / ArchUnitNET
│       ├── go/                     #   go-arch-lint
│       └── python/                 #   import-linter
│                                   # (contract test nằm TRONG từng service, xem dưới)
│
├── docs/                           # ★ khung arc42 — chuẩn tài liệu kiến trúc
│   ├── 01-introduction-and-goals.md
│   ├── 02-architecture-constraints.md
│   ├── 03-context-and-scope.md
│   ├── 04-solution-strategy.md
│   ├── 05-building-block-view.md
│   ├── 06-runtime-view.md          #   ← saga, luồng event vẽ ở đây
│   ├── 07-deployment-view.md
│   ├── 08-cross-cutting-concepts/  #   ← các tài liệu chuyên đề thành phụ lục
│   │   ├── event-catalog.md
│   │   ├── partition-strategy.md
│   │   ├── schema-evolution.md
│   │   ├── observability.md
│   │   └── service-boundaries.md
│   ├── 09-architecture-decisions/  #   ← ADR nằm ở đây (arc42 chương 9)
│   ├── 10-quality-requirements.md
│   ├── 11-risks-and-technical-debt.md
│   ├── 12-glossary.md
│   ├── failure-modes.md
│   └── runbook.md
│
└── tools/
    └── devcontainer/
```

**Thay đổi lớn nhất so với bản của bạn:** bỏ `src/` ngoài cùng, tách `proto/rpc` với `proto/events`, thêm `building-blocks/gen/`, thêm `api-gateway`.

### Ba quyết định cấu trúc mượn từ BookWorm

| Quyết định | Vì sao |
|---|---|
| **`mise.toml` ở gốc** | Bạn có **4 toolchain**. Không pin phiên bản thì trong vòng một tháng repo chỉ chạy được trên máy bạn. Đây là file quan trọng thứ hai sau `Makefile`. |
| **`docs/` theo arc42** | Thay 5 file `.md` rời rạc bằng khung 12 chương chuẩn công nghiệp. Các tài liệu chuyên đề (event catalog, partition strategy) trở thành **phụ lục của chương 08**, không mất đi. ADR về đúng chỗ của nó — **chương 09**. |
| **Contract test nằm TRONG service** | `services/order-service/tests/ContractTests/` chứ không phải `tests/contract/` ở ngoài. Contract test thuộc về service sở hữu contract — cùng nguyên tắc với "schema thuộc về producer". |

### `mise.toml` — viết ngay tuần đầu

```toml
[tools]
dotnet  = "10.0"
go      = "1.24"
python  = "3.12"
java    = "temurin-21"
buf     = "latest"
k6      = "latest"

[env]
_.file = ".env"

[tasks.up]
run = "docker compose -f compose.infra.yml -f compose.services.yml up -d --build"
```

> `mise install` → cả 4 toolchain đúng phiên bản. Không có dòng "cài .NET 10 trước" trong README nữa.

---

## 3. Phân chia service — ngôn ngữ nào, vì sao

Polyglot chỉ đáng giá khi mỗi lựa chọn có **lý do kỹ thuật thật**. Bảng này là câu trả lời khi phỏng vấn hỏi *"sao không viết hết bằng một ngôn ngữ?"*

| Service | Ngôn ngữ | Lý do thật | Nếu chọn sai thì sao |
|---|---|---|---|
| **order-service** | C# / .NET | Nghiệp vụ phức tạp nhất: aggregate, invariant, transaction, saga. Type system + EF Core + tooling refactor mạnh nhất. | Viết bằng Go: thiếu generic-rich modeling, ORM yếu, code DDD trở nên rườm rà |
| **payment-service** | C# | Cùng bounded context ngôn ngữ với order, chia sẻ `building-blocks/dotnet` | — |
| **search-service** | Go | I/O-bound, QPS cao, latency p99 quan trọng. Goroutine + binary 15MB + khởi động <100ms → autoscale rẻ. | Viết bằng C#: cold start chậm hơn, RAM gấp 3–4× cho cùng throughput |
| **inventory-service** | Go | Contention cao (nhiều người giành cùng một SKU). Cần kiểm soát concurrency ở mức thấp. | — |
| **recommendation-service** | Python | Hệ sinh thái ML: `sentence-transformers`, `faiss`, `pandas`, `pgvector`. Không ngôn ngữ nào thay được. | Viết bằng C#: ML.NET tồn tại nhưng hệ sinh thái mỏng hơn hàng chục lần |
| **stream-processor** | Java | Flink là Java-native. DataStream API + Flink SQL. | PyFlink: chậm hơn, thiếu tính năng; Flink.NET: không ai dùng, không ai phỏng vấn |
| **api-gateway** | C# / .NET + YARP | Cùng toolchain với order/payment → dùng chung `building-blocks/dotnet`. YARP do team ASP.NET làm, tích hợp thẳng Aspire, OTel, auth. BFF aggregate viết bằng LINQ dễ chịu hơn Go. | Viết bằng Go: image nhỏ hơn (~15MB vs ~110MB), cold start nhanh hơn — nhưng phải nuôi thêm một hệ `building-blocks` |

> **Nói thẳng:** polyglot có chi phí thật — 4 toolchain, 4 cách log, 4 job CI, khó share code. Trong công ty thật, đây thường là quyết định **sai** trừ khi có lý do rõ ràng.
> Với portfolio thì hợp lý, vì mục tiêu chính là **chứng minh năng lực đa nền tảng**. Hãy viết điều này ra trong `docs/09-architecture-decisions/001-why-polyglot.md` — thừa nhận trade-off làm bạn đáng tin hơn là giả vờ không có.

---

## 3b. Gateway C# + YARP — làm được gì, không làm được gì

> Đọc mục này **trước khi** viết dòng code gateway đầu tiên. Đổi Go → C# không phải là thay tên ngôn ngữ; nó thay đổi cách gateway hoạt động.

### Hiểu nhầm phổ biến

**YARP không dịch REST ↔ gRPC.** YARP là reverse proxy **HTTP → HTTP**. Nó route, load-balance, transform header, retry — nhưng nó không biết gì về Protobuf. Cho một request `GET /v1/orders/123` đi qua YARP, thứ đến đích vẫn là một request HTTP, không phải một lời gọi gRPC.

Trong khi đó `grpc-gateway` (Go) **có** sinh ra proxy REST↔gRPC từ annotation `google.api.http` trong proto. Đây là năng lực bạn vừa đánh đổi đi.

### Ba cách lấy lại năng lực đó trong .NET

| Cách | Cơ chế | Vấn đề |
|---|---|---|
| **A. gRPC JSON transcoding trong từng service** | `Microsoft.AspNetCore.Grpc.JsonTranscoding` đọc annotation `google.api.http`, tự expose REST song song với gRPC. YARP chỉ route. | **Chỉ chạy trên .NET.** `search-service` và `inventory-service` viết bằng Go → không dùng được. Phải cài thêm `grpc-gateway` cho hai service Go → lại quay về hai cơ chế song song. |
| **B. Gateway giữ gRPC client, expose Minimal API** ★ | Gateway tham chiếu stub sinh từ proto, gọi service bằng gRPC client, expose REST bằng Minimal API viết tay. | Phải viết tay endpoint. Nhưng số endpoint public thực tế ít hơn số RPC nhiều. |
| **C. Đặt Envoy trước, YARP sau** | Envoy làm gRPC-JSON transcoder, YARP làm auth/BFF | Hai lớp proxy — độ phức tạp không đáng cho dự án này |

### Khuyến nghị: cách B

```csharp
// services/api-gateway/src/Endpoints/OrderEndpoints.cs
app.MapGet("/api/orders/{id:guid}", async (
        Guid id,
        OrderService.OrderServiceClient orders,   // stub sinh từ proto/rpc/order/v1
        CancellationToken ct) =>
{
    var reply = await orders.GetOrderAsync(
        new GetOrderRequest { OrderId = id.ToString() }, cancellationToken: ct);
    return Results.Ok(reply.ToDto());
})
.RequireAuthorization()
.RequireRateLimiting("per-user");
```

**Vì sao cách B tốt hơn bạn tưởng:**

1. **Gateway trở thành BFF thật, không phải proxy ngu.** Endpoint `GET /api/orders/{id}` có thể gọi song song `order`, `inventory`, `recommendation` rồi gộp — đúng thứ frontend cần, thay vì bắt trình duyệt gọi 3 lần.
2. **Contract vẫn được kiểm soát.** Stub gRPC vẫn sinh từ `proto/`, `buf breaking` vẫn chặn thay đổi phá vỡ. Chỉ có lớp REST là viết tay.
3. **Type-safe đầu-cuối.** Đổi field trong proto → gateway không compile. Với `grpc-gateway` thì lỗi rơi vào runtime.

**YARP dùng vào việc gì trong cách B?**

Không phải cho các endpoint gRPC — mà cho những thứ chỉ cần đi thẳng qua:

```jsonc
// appsettings.json
"ReverseProxy": {
  "Routes": {
    "search-passthrough": {                  // search-service tự expose REST qua grpc-gateway
      "ClusterId": "search",
      "Match": { "Path": "/api/search/{**catch-all}" },
      "AuthorizationPolicy": "authenticated",
      "RateLimiterPolicy": "per-user"
    }
  },
  "Clusters": {
    "search": { "Destinations": { "d1": { "Address": "http://search-service:8080" } } }
  }
}
```

→ **Mô hình lai:** YARP cho passthrough đơn giản, Minimal API + gRPC client cho những chỗ cần aggregate hoặc cần dịch. Đây là cách dùng YARP đúng bản chất của nó.

### Được và mất khi đổi Go → C#

| | Được | Mất |
|---|---|---|
| Toolchain | Bớt 1 hệ `building-blocks` — gateway chia sẻ logging/otel/auth với order & payment | — |
| Aspire | Gateway vào được `AppHost`, service discovery tự động, không cần hard-code URL | — |
| BFF aggregate | LINQ + `async`/`await` + record type — dễ hơn Go rõ rệt | — |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` trưởng thành hơn hệ sinh thái Go | — |
| REST↔gRPC | — | Mất `grpc-gateway` sinh code tự động → phải viết tay Minimal API |
| Runtime | — | Image ~110MB (vs ~15MB), cold start ~200–400ms (vs <50ms), RAM idle ~60MB (vs ~10MB) |
| Ngôn ngữ trong repo | Còn **4** (C#, Go, Python, Java) thay vì 4 — Go vẫn còn cho search/inventory | — |

> **Đáng ghi vào ADR:** con số runtime ở trên chỉ quan trọng khi gateway phải scale lên hàng chục instance. Ở quy mô dự án này nó không quan trọng, và việc thống nhất toolchain với order/payment có giá trị thực tế lớn hơn. Nhưng hãy nói rõ bạn **biết** trade-off đó, đừng lờ đi.

### Việc cần thêm vào lộ trình

- **Tháng 0.5:** `api-gateway` (C#) với 1 endpoint Minimal API gọi gRPC client → order-service
- **Tháng 2:** thêm YARP route passthrough cho `search-service` (Go, có `grpc-gateway` riêng)
- **Tháng 3:** biến gateway thành BFF thật — endpoint `GET /api/products/{id}` gọi song song `search` + `recommendation` + `inventory`
- **Tháng 5:** gateway là **điểm bắt đầu của distributed trace** — nơi sinh `traceparent` đầu tiên

---

## 4. Contract-first workflow

Đây là phần khiến monorepo đa ngôn ngữ **sống được**.

### Công cụ: [`buf`](https://buf.build)

```yaml
# proto/buf.yaml
version: v2
lint:
  use: [STANDARD]
breaking:
  use: [FILE]
```

```yaml
# proto/buf.gen.yaml
version: v2
plugins:
  - remote: buf.build/protocolbuffers/csharp
    out: ../building-blocks/gen/csharp
  - remote: buf.build/grpc/csharp
    out: ../building-blocks/gen/csharp
  - remote: buf.build/protocolbuffers/go
    out: ../building-blocks/gen/go
  - remote: buf.build/grpc/go
    out: ../building-blocks/gen/go
  - remote: buf.build/protocolbuffers/python
    out: ../building-blocks/gen/python
  - remote: buf.build/protocolbuffers/java
    out: ../building-blocks/gen/java
```

### Quy tắc bất di bất dịch

1. **Không ai được sửa file trong `building-blocks/gen/`.** Thêm `.gitattributes`: `building-blocks/gen/** linguist-generated=true`
2. **`buf breaking` chạy trong CI** trên mọi PR, so với nhánh `main`. Contract vỡ = PR đỏ.
3. **Version trong đường dẫn** (`order/v1/`). Muốn breaking change? Tạo `v2`, chạy song song, deprecate dần.
4. **`proto/rpc/` và `proto/events/` khác bản chất:**
   - `rpc/` = *"tôi hỏi bạn cái này"* → tên là động từ (`GetOrder`, `ReserveStock`)
   - `events/` = *"việc này đã xảy ra rồi"* → tên là quá khứ (`OrderPlaced`, `StockReserved`)
5. Sinh code bằng `make proto`, **commit kết quả vào git**. Lý do: dev mới clone về là build được ngay, không cần cài `buf`.

---

## 5. Quy tắc giao tiếp: gRPC hay Kafka

Đây là quyết định bị làm sai nhiều nhất trong microservices. Quy tắc một dòng:

> **Cần câu trả lời để đi tiếp → gRPC. Chỉ thông báo việc đã xảy ra → Kafka.**

| Tình huống | Cách | Vì sao |
|---|---|---|
| Gateway → mọi service | **gRPC** | Client đang chờ response |
| Order → Inventory: giữ hàng | **gRPC** | Phải biết còn hàng hay không mới tạo được đơn |
| Order → Payment: trừ tiền | **Kafka** (saga) | Có thể mất vài giây; không được block người dùng |
| Order → Search: cập nhật index | **Kafka** | Search chậm 2 giây cũng không sao |
| Order → Recommendation: ghi nhận hành vi | **Kafka** | Fire-and-forget |
| Clickstream → mọi nơi | **Kafka** | Volume cao, nhiều consumer |
| Mọi thứ → Stream processor | **Kafka** | Bản chất của streaming |

### Sơ đồ

```
                    ┌──────────────┐
   Browser ────────▶│ api-gateway  │ [C# + YARP]  BFF, auth, rate limit
                    └──────┬───────┘
          ┌────────────────┼────────────────┬──────────────────┐
          │ gRPC           │ gRPC           │ gRPC             │ gRPC
          ▼                ▼                ▼                  ▼
   ┌─────────────┐  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐
   │   order     │  │   search    │  │  inventory   │  │ recommendation   │
   │    [C#]     │  │    [Go]     │  │    [Go]      │  │    [Python]      │
   │  Postgres   │  │ OpenSearch  │  │   Postgres   │  │    pgvector      │
   └──────┬──────┘  └──────▲──────┘  └──────┬───────┘  └────────▲─────────┘
          │ outbox         │ index          │                   │ features
          ▼                │                ▼                   │
   ╔══════════════════════════════════════════════════════════════════════╗
   ║           KAFKA  ·  ordering.events · catalog.events · clickstream   ║
   ╚═══════════════════════════════╤══════════════════════════════════════╝
                                   │
                        ┌──────────▼───────────┐
                        │  stream-processor    │ [Java / Flink]
                        │  window · state · CEP│
                        └──────────┬───────────┘
                                   ▼
                        ClickHouse  ·  Iceberg/MinIO
```

---

## 6. Trải nghiệm dev local

Mục tiêu: **`git clone` → `make up` → hệ thống chạy.** Không có bước thứ ba.

### Makefile là API của repo

```makefile
.PHONY: up down proto test lint

up:            ## Khởi động toàn bộ hệ thống
	docker compose -f compose.infra.yml -f compose.services.yml up -d --build

infra:         ## Chỉ hạ tầng — dùng khi debug 1 service từ IDE
	docker compose -f compose.infra.yml up -d

down:
	docker compose -f compose.infra.yml -f compose.services.yml down -v

proto:         ## Sinh lại toàn bộ stub từ proto/
	cd proto && buf lint && buf generate

proto-check:   ## Kiểm tra breaking change so với main
	cd proto && buf breaking --against '.git#branch=main'

test:          ## Chạy test của cả 4 ngôn ngữ
	dotnet test services/order-service
	dotnet test services/api-gateway
	cd services/search-service && go test ./...
	cd services/inventory-service && go test ./...
	cd services/recommendation-service && pytest
	cd services/stream-processor && mvn test

arch:          ## Architecture test — ép ranh giới service/tầng
	dotnet test tests/arch/dotnet
	cd tests/arch/go && go-arch-lint check
	cd tests/arch/python && lint-imports

lint:          ## Lint toàn repo
	cd proto && buf lint
	cd services/search-service && golangci-lint run
	cd services/inventory-service && golangci-lint run
	cd services/recommendation-service && ruff check .
```

### Tách compose làm hai

| File | Nội dung | Vòng đời |
|---|---|---|
| `compose.infra.yml` | Kafka, Schema Registry, Postgres ×3, Redis, OpenSearch, MinIO, OTel Collector, Grafana | Chạy 1 lần buổi sáng, để nguyên cả ngày |
| `compose.services.yml` | 7 service của bạn (3 C#, 2 Go, 1 Python, 1 Java) | Restart liên tục khi code |

> Nếu để chung một file, mỗi lần sửa code Go bạn lại phải chờ Kafka khởi động lại. Sau tuần thứ hai bạn sẽ ghét dự án của chính mình.

### Bắt buộc có

- [ ] **Healthcheck** cho mọi container + `depends_on: condition: service_healthy`
- [ ] Seed data script — không có dữ liệu thì không demo được gì
- [ ] `.env.example` đầy đủ, `make up` không cần sửa gì cũng chạy

---

## 7. Observability xuyên ngôn ngữ

**Đây là phần khoe được nhiều nhất mà tốn ít công nhất.** Một trace đi từ **C# gateway → Go → C# → Kafka → Python** và hiện thành **một đường liền mạch** trong Grafana Tempo là hình ảnh thuyết phục hơn bất kỳ đoạn code nào.

| Thành phần | Cách làm |
|---|---|
| Instrumentation | OpenTelemetry SDK cho .NET, Go, Python, Java — **không dùng thư viện tracing riêng của từng ngôn ngữ** |
| Propagation | W3C `traceparent` header cho gRPC; **Kafka message header** cho async |
| Collector | 1 OTel Collector trong `compose.infra.yml`, mọi service export vào đó |
| Backend | Tempo (trace) + Prometheus (metric) + Loki (log) + Grafana |
| Correlation | Mọi log **phải** có `trace_id` — đây là điều kiện để nối log với trace |
| **Trace origin** | **`api-gateway` là nơi sinh `traceparent` đầu tiên.** Mọi span khác là con cháu của span gateway. Vì gateway giờ là .NET, bạn dùng `ActivitySource` + `AddAspNetCoreInstrumentation()` — cùng cơ chế với order-service, không phải học thêm SDK thứ hai. |

> **Điểm khó — và cũng là điểm đáng khoe:** truyền trace context qua Kafka không tự động. Bạn phải tự inject vào header ở producer và extract ở consumer, ở **cả 4 ngôn ngữ**. Làm được thì viết `docs/09-architecture-decisions/00X-distributed-tracing.md`.

---

## 8. Lộ trình 6 tháng

### Tháng 0.5 — Walking skeleton *(2 tuần)*

> **Mục tiêu:** một request đi từ browser → gateway → 1 service → DB, và thấy được trace.

- [ ] Khung repo theo cấu trúc mục 2
- [ ] **`mise.toml`** pin .NET / Go / Python / Java / buf — *làm ngay ngày đầu, không để sau*
- [ ] Khởi tạo `docs/` theo khung arc42 (12 file rỗng có tiêu đề — điền dần)
- [ ] **`.agents/skills/` + `CLAUDE.md` + `AGENTS.md`** — xem [mục 15](#15-agent-skills--ghim-hướng-dẫn-ai-vào-repo).
      Viết `proto-contract` và `ddd-dotnet` ngay tuần đầu; các skill còn lại thêm khi ngôn ngữ tương ứng xuất hiện.
- [ ] `proto/rpc/order/v1/order_service.proto` — 1 RPC duy nhất `GetOrder`
- [ ] `make proto` sinh stub cho C# + Go
- [ ] `order-service` (C#) trả về dữ liệu hard-code
- [ ] `api-gateway` (C# + YARP) expose REST `/api/orders/{id}` → gọi gRPC client tới order-service
- [ ] `compose.infra.yml` + `compose.services.yml` + `Makefile`
- [ ] OTel Collector + Grafana Tempo — **thấy trace 2 chặng**
- [ ] CI: build cả 2 service + `buf lint`

**Tiêu chí xong:** `make up && curl localhost:8080/api/orders/1` → có kết quả, và trace hiện đủ 2 span.

---

### Tháng 1 — Core domain (C#)

- [ ] **order-service** đầy đủ:
  - [ ] `Domain/` — `Order` aggregate, `OrderItem`, `Address` value object, domain event
  - [ ] `Application/` — MediatR + pipeline `Logging → Validation → Transaction`
  - [ ] `Infrastructure/` — EF Core, Postgres, migration
  - [ ] Idempotency: `IdentifiedCommand` + `RequestManager`
- [ ] **payment-service** (C#) — tối giản, chỉ đủ làm saga participant
- [ ] Unit test domain **không mock DB**
- [ ] Integration test bằng **Testcontainers**

#### ★ Architecture tests — bắt đầu từ tháng 1, không để cuối

> Đây là thứ biến *"tôi theo DDD"* thành *"vi phạm DDD thì build đỏ"*. Với repo polyglot, nó còn quan trọng hơn — ranh giới giữa 4 ngôn ngữ là chỗ dễ vỡ nhất và khó phát hiện nhất.

| Ngôn ngữ | Công cụ | Luật ép ngay từ tháng 1 |
|---|---|---|
| C# | `NetArchTest` / `ArchUnitNET` | `Domain/` không được tham chiếu `Infrastructure/` hay `building-blocks/`<br>Aggregate phải kế thừa `Entity`<br>Domain event phải là `record`, immutable<br>Command handler phải nằm trong `Application/` |
| Go | `go-arch-lint` | `internal/handler` không được import `internal/repository` trực tiếp<br>Không package nào import `gen/csharp` |
| Python | `import-linter` | `app/models` không được import `app/consumers` |
| **Mọi ngôn ngữ** | test tự viết | **Không service nào import `gen/` của ngôn ngữ khác**<br>**Không service nào tham chiếu package của service khác** |

- [ ] `tests/arch/dotnet` — tối thiểu 8 luật, chạy trong CI
- [ ] `tests/arch/go` + `tests/arch/python` — thêm khi service tương ứng ra đời (tháng 2, 3)
- [ ] Luật quan trọng nhất: **cyclic dependency test** — phát hiện distributed monolith từ sớm

---

### Tháng 2 — Go services + event backbone ★

> Tháng biến project từ "vài API" thành hệ thống event-driven.

- [ ] **search-service** (Go)
  - [ ] gRPC handler + OpenSearch client
  - [ ] Kafka consumer → cập nhật index
  - [ ] Đo p99 latency, đặt mục tiêu < 50ms
- [ ] **inventory-service** (Go) — reserve/release stock, xử lý concurrency
- [ ] **Outbox pattern** trong order-service
- [ ] **Debezium** đọc Postgres WAL → Kafka *(hoặc publisher thường ở giai đoạn 1)*
- [ ] `proto/events/` + Protobuf serialization qua Schema Registry
- [ ] **Partition key design** → `docs/08-cross-cutting-concepts/partition-strategy.md`
- [ ] DLQ + retry topic có backoff
- [ ] `building-blocks/go/` — otel middleware, kafka wrapper dùng chung

**Tiêu chí xong:** kill order-service giữa lúc publish → restart → không mất, không duplicate.

---

### Tháng 3 — Saga & Python service

- [ ] **Saga đơn hàng** qua Kafka:

  ```
  Created → StockReserved → Paid → Confirmed
      │           │            │
      └─ StockRejected ────────┴─ PaymentFailed ──→ Cancelled
  ```

- [ ] Chọn **orchestration** (state machine trong order-service), không choreography
      → `docs/09-architecture-decisions/00X-saga-orchestration.md`
- [ ] Stuck-saga detection + timeout
- [ ] **recommendation-service** (Python)
  - [ ] gRPC server (grpcio)
  - [ ] Embedding sản phẩm bằng `sentence-transformers` → pgvector
  - [ ] `GetSimilarProducts` + `GetRecommendationsForUser`
  - [ ] Kafka consumer cập nhật hành vi người dùng
- [ ] **E2E test** qua gateway: đặt hàng → kiểm tra trạng thái cuối

---

### Tháng 4 — Tầng Flink (Java) ★

> Tháng quyết định giá trị portfolio với vị trí Data Engineer.

Ba job, mỗi job dạy một khái niệm khác nhau:

| # | Job | Khái niệm |
|---|---|---|
| 1 | `revenue-rollup` | Tumbling window, watermark, event time |
| 2 | `fraud-detection` | Keyed state, timer, CEP |
| 3 | `sessionization` | Session window, late data, side output |

Phải làm được **và giải thích được**:

- [ ] Watermark strategy — bounded out-of-orderness đặt bao nhiêu, vì sao
- [ ] Checkpointing — interval, aligned vs unaligned, RocksDB state backend
- [ ] **Exactly-once** — Kafka source + transactional sink (2PC)
      → *chứng minh:* kill TaskManager giữa chừng, count không đổi
- [ ] **Savepoint & rescale** — dừng job, tăng parallelism, restore
- [ ] Ít nhất 1 job viết bằng **Flink SQL**

> Mỗi job kèm `README.md` giải thích quyết định thiết kế. Đây là kịch bản trả lời phỏng vấn.

---

### Tháng 5 — Serving layer & hoàn thiện

- [ ] **ClickHouse** — Flink ghi vào, phục vụ dashboard
- [ ] **Iceberg trên MinIO** — lakehouse layer, so sánh hot vs cold query
- [ ] Dashboard real-time (Grafana hoặc một web app nhỏ)
- [ ] **Trace xuyên suốt hoàn chỉnh**: browser → Go → C# → Kafka → Java → ClickHouse
- [ ] Metric: consumer lag, checkpoint duration, backpressure, gRPC p99 mỗi service
- [ ] Data quality: null check, schema drift, late-arrival rate

---

### Tháng 6 — Hardening & trình bày

- [ ] **Chaos test** → `docs/failure-modes.md`
  - kill broker · kill TaskManager · network partition · service chết giữa saga
- [ ] **Load test** — k6 qua gateway, 10k rps, đo p99 từng chặng
- [ ] Backfill/replay — reset consumer group, replay 30 ngày
- [ ] Deploy lên **kind** (K8s local) — Helm chart hoặc kustomize
- [ ] **12–15 ADR** trong `docs/09-architecture-decisions/`
- [ ] `docs/runbook.md` — "service X chết thì làm gì"
- [ ] Hoàn thiện 12 chương arc42 — đặc biệt **10-quality-requirements** và **11-risks-and-technical-debt**

#### ★ Đóng gói — 2 ngày công, nhưng quyết định repo có được đọc hay không

> Người xem GitHub quyết định trong **30 giây** có mở code của bạn hay không. Toàn bộ 6 tháng làm việc phụ thuộc vào 30 giây đó.

- [ ] **Sơ đồ kiến trúc dạng ảnh** trong README — không phải ASCII art, mà PNG/SVG vẽ bằng Excalidraw hoặc Structurizr
- [ ] **Screenshot** dashboard real-time + Grafana trace 4 ngôn ngữ + Flink UI
- [ ] **GIF demo** — `make up` → đặt hàng → thấy số liệu chạy trên dashboard
- [ ] **Badge**: CI status, coverage, license, .NET/Go/Python version
- [ ] **GitHub Pages** publish `docs/` (MkDocs hoặc Docusaurus — MkDocs Material đủ và nhanh hơn)
- [ ] README rút gọn: mục tiêu → sơ đồ → bảng service/ngôn ngữ → "chạy trong 2 lệnh" → link docs site
- [ ] **CI bảo mật**: CodeQL + Trivy + Dependabot *(gần như chỉ copy YAML, signal tốt cho vị trí platform)*
- [ ] Test lại `git clone && mise install && make up` trên **máy sạch**

---

## 9. CI/CD cho monorepo đa ngôn ngữ

Vấn đề cốt lõi: **đừng build lại tất cả khi chỉ sửa một service.**

```yaml
# .github/workflows/ci.yml  (rút gọn)
jobs:
  changes:
    outputs:
      order:   ${{ steps.filter.outputs.order }}
      gateway: ${{ steps.filter.outputs.gateway }}
      search:  ${{ steps.filter.outputs.search }}
      proto:   ${{ steps.filter.outputs.proto }}
    steps:
      - uses: dorny/paths-filter@v3
        id: filter
        with:
          filters: |
            # Gateway giờ là C# → phụ thuộc gen/csharp VÀ building-blocks/dotnet,
            # giống hệt order-service. Sửa proto = build lại cả hai.
            order:   ['services/order-service/**',  'building-blocks/gen/csharp/**', 'building-blocks/dotnet/**']
            gateway: ['services/api-gateway/**',    'building-blocks/gen/csharp/**', 'building-blocks/dotnet/**']
            search:  ['services/search-service/**', 'building-blocks/gen/go/**',     'building-blocks/go/**']
            proto:   ['proto/**']

  proto-check:
    if: needs.changes.outputs.proto == 'true'
    steps:
      - run: buf lint
      - run: buf breaking --against '.git#branch=main'   # ← chặn breaking change

  order-service:
    if: needs.changes.outputs.order == 'true'
    steps: [dotnet build, dotnet test]

  api-gateway:
    if: needs.changes.outputs.gateway == 'true'
    steps: [dotnet build, dotnet test]

  # ... tương tự cho từng service
```

> **Hệ quả của việc đổi gateway sang C#:** trước đây sửa `proto/` chỉ rebuild 1 project .NET, giờ rebuild **2** (order + gateway) vì cả hai cùng dùng `building-blocks/gen/csharp/`.
> CI chậm hơn một chút, nhưng đổi lại **breaking change được phát hiện ở cả hai đầu của contract cùng lúc** — client (gateway) và server (order) vỡ chung một lần, không phải phát hiện muộn ở runtime.

**Ba job bắt buộc phải có:**

| Job | Chặn cái gì |
|---|---|
| `buf breaking` | Sửa proto làm vỡ consumer đang chạy |
| `gen-is-current` | Sửa `.proto` nhưng quên chạy `make proto` — CI sinh lại và so sánh, khác nhau thì đỏ |
| `e2e` | Từng service pass nhưng ghép lại thì hỏng |

---

## 10. Cạm bẫy đã biết

| # | Cạm bẫy | Cách tránh |
|---|---|---|
| 1 | **Chia service quá sớm** | 7 service ở tuần 1 là quá nhiều. Bắt đầu **2** (order + gateway) — giờ cả hai đều C#, nên tháng 0.5 chỉ cần **một** toolchain. Thêm Go ở tháng 2, Python tháng 3, Java tháng 4. |
| 2 | **Shared database** | Mỗi service một database schema riêng, **không service nào query bảng của service khác**. Vi phạm điều này thì bạn có distributed monolith. |
| 3 | **`building-blocks` phình to** | Chỉ chứa code *hạ tầng* (otel, kafka, logging). **Không bao giờ** chứa domain logic — nếu 2 service cần chung domain logic thì ranh giới service đang sai. |
| 4 | **Log 4 kiểu khác nhau** | Thống nhất **structured JSON** + cùng field name (`trace_id`, `service`, `level`) ngay từ tuần 1. Sửa sau rất mệt. |
| 5 | **Bỏ qua versioning proto** | Đặt `v1/` trong đường dẫn ngay từ đầu, dù chưa cần. |
| 6 | **Python service chậm** | gRPC Python là bottleneck thật. Dùng `grpcio` với thread pool đủ lớn, cân nhắc `uvloop`. Đo trước khi lo. |
| 7 | **Không ai chạy được repo** | Test lại `git clone && make up` trên máy sạch mỗi tháng một lần. |

---

## 11. Phạm vi loại trừ

| Không làm | Lý do |
|---|---|
| Thanh toán thật (Stripe) | Giả lập đủ; không dạy gì về distributed system |
| Admin CMS, quản lý user, i18n | Tốn thời gian, không thể hiện năng lực |
| Mobile app | Không liên quan mục tiêu |
| Service mesh (Istio/Linkerd) | Độ phức tạp lớn, giá trị học tập thấp so với công sức |
| Recommendation model phức tạp | Embedding + cosine similarity là đủ ấn tượng |
| Tự viết service discovery | Docker DNS / K8s Service là đủ |

---

## 12. Bản đồ phỏng vấn

### Vị trí Data Engineer (Streaming)

| Câu hỏi | Trả lời bằng |
|---|---|
| "Exactly-once trong Flink hoạt động thế nào?" | Job 1 + chaos test kill TaskManager |
| "Chọn partition key thế nào?" | `docs/08-cross-cutting-concepts/partition-strategy.md` |
| "Late data xử lý ra sao?" | Job 3 — session window + side output |
| "Schema thay đổi thì sao?" | `buf breaking` trong CI + Schema Registry |
| "Consumer lag tăng, bạn làm gì?" | Grafana dashboard + rescale bằng savepoint |
| "CDC khác gì outbox polling?" | Tháng 2 — đã làm và đo cả hai |
| "Backpressure là gì?" | Flink UI + metric đã dựng |

### Vị trí Backend / Platform

| Câu hỏi | Trả lời bằng |
|---|---|
| "Chia ranh giới service thế nào?" | `docs/08-cross-cutting-concepts/service-boundaries.md` |
| "Sao dùng nhiều ngôn ngữ?" | `docs/09-architecture-decisions/001-why-polyglot.md` — kèm thừa nhận trade-off |
| "gRPC hay REST hay message queue?" | Mục 5 — có quy tắc rõ ràng, không tuỳ hứng |
| "Distributed transaction thế nào?" | Saga orchestration tháng 3 |
| "Debug lỗi xuyên 4 service ra sao?" | Distributed tracing tháng 5 |
| "Deploy độc lập từng service thế nào?" | CI path-filter + versioned proto |

---

## 13. Theo dõi tiến độ

| Giai đoạn | Deliverable then chốt | Trạng thái |
|---|---|---|
| T0.5 — Skeleton | `make up` + trace 2 chặng | ☐ |
| T1 — Core domain | Order aggregate + Testcontainers test | ☐ |
| T2 — Go + events ★ | Outbox → Kafka, không mất/duplicate | ☐ |
| T3 — Saga + Python | E2E đặt hàng thành công qua gateway | ☐ |
| T4 — Flink ★ | 3 job + chứng minh exactly-once | ☐ |
| T5 — Serving | Trace liền mạch 4 ngôn ngữ | ☐ |
| T6 — Hardening | `failure-modes.md` + 15 ADR | ☐ |
| T6 — Đóng gói | Docs site + screenshot + badge + sơ đồ | ☐ |

---

## 14. Đối chiếu với BookWorm

[`foxminchan/BookWorm`](https://github.com/foxminchan/BookWorm) là repo tham chiếu .NET/Aspire được đánh giá cao (⭐ 504). Mục này ghi lại **vì sao dự án này chọn khác** — để 3 tháng nữa bạn không quên lý do.

### Quy mô thật của BookWorm

| | |
|---|---|
| Thời gian | 7/2024 → nay, **hơn 2 năm** |
| Commit | **597** — của đúng **một người**; 7 "contributor" còn lại là bot |
| Quy mô | 3.377 file · 10 service · 2 app Next.js |
| Ngôn ngữ | **~99% C#** |

> ⚠️ Đừng so sánh tiến độ của bạn với repo này. Bạn có 6 tháng, anh ấy có 2 năm. So sánh **cách làm**, không so sánh **khối lượng**.

### Học gì — đã đưa vào kế hoạch

| Học | Đã nằm ở đâu trong tài liệu này |
|---|---|
| `mise.toml` pin phiên bản toolchain | Mục 2 + tháng 0.5 |
| Tài liệu theo **arc42** 12 chương | Mục 2 (`docs/`) + tháng 0.5, 6 |
| **Architecture tests** ép ranh giới | Tháng 1 (mở rộng cho Go + Python ở tháng 2, 3) |
| **Chassis** có cấu trúc rõ, 10 module | Mục 2 (`building-blocks/chassis-*`) |
| **Contract test nằm trong từng service** | Mục 2 — không để ở `tests/contract/` ngoài |
| **Đóng gói**: docs site, screenshot, badge, sơ đồ ảnh | Tháng 6 |
| CI bảo mật: CodeQL, Trivy, Dependabot | Tháng 6 |
| **Agent skills ghim trong repo** (`.agents/skills/`) | Mục 15 + tháng 0.5 |

### Tránh gì — và vì sao

| Anh ấy làm | Ta không làm | Lý do |
|---|---|---|
| **10 service** | 7, và cứng rắn với mục 11 | 2 năm vs 6 tháng. Thêm service = thêm bề mặt phải bảo trì, không phải thêm điểm cộng. |
| **Nhồi mọi pattern** — event sourcing + inbox + feature flags + API versioning + CQRS + VSA + saga | Chỉ pattern có lý do trong bối cảnh này, mỗi cái một ADR | Người phỏng vấn giỏi sẽ hỏi *"vì sao event sourcing ở đây?"*. *"Để chứng minh tôi làm được"* là câu trả lời yếu. |
| **Tầng AI/agent**: MCP, A2A, AG-UI, multi-agent | Bỏ hoàn toàn | Trendy nhưng lệch khỏi vị trí Data Engineer (Streaming). |
| **2 app Next.js + WCAG 2.1 AA** | 1 dashboard đơn giản | ~2 tháng công để chứng minh kỹ năng không ai hỏi bạn. |
| **Vertical Slice Architecture** | Giữ layered `Domain/Application/Infrastructure` | VSA hợp CRUD-heavy. Domain của ta có saga + aggregate invariant phức tạp → layered rõ ràng hơn. *Đây là lựa chọn, không phải chân lý — ghi vào ADR.* |

### Điều BookWorm **không** dạy được — và đó là chỗ ta khác biệt

BookWorm dùng Kafka **như một hàng đợi tin nhắn**. Không có:

- ❌ Partition strategy có chủ đích · ❌ Consumer lag monitoring · ❌ Replay từ offset
- ❌ Schema Registry / schema evolution · ❌ CDC / Debezium
- ❌ Stream processing (Flink, windowing, watermark, keyed state) · ❌ Exactly-once semantics
- ❌ Serving layer / lakehouse

Toàn bộ **tháng 2 và tháng 4** của kế hoạch này là vùng trắng trong repo của anh ấy. Với vị trí Data Engineer (Streaming), BookWorm gần như **không dùng được làm mẫu kỹ thuật** — nhưng dùng được làm **mẫu về cách làm và cách trình bày**.

> **Nguyên tắc rút ra: học *cách* anh ấy làm, đừng học *cái* anh ấy làm.**

---

## 15. Agent skills — ghim hướng dẫn AI vào repo

> **Vì sao mục này quan trọng với bạn hơn với BookWorm:** repo của anh ấy ~99% C#, một bộ convention. Repo của bạn có **4 ngôn ngữ, 4 bộ convention**. Không ghim hướng dẫn vào repo, AI sẽ viết Go theo kiểu C#, viết Flink job theo tư duy batch, và đặt tên Kafka topic tuỳ hứng mỗi lần.

### Agent skill là gì

Một thư mục chứa `SKILL.md` — YAML frontmatter + hướng dẫn — mà AI coding agent (Claude Code, Copilot, Cursor) tự nạp khi gặp task liên quan. Đây **không phải code chạy**, và không liên quan gì đến tính năng AI trong sản phẩm (mà ta đã loại khỏi phạm vi ở mục 11).

```
.agents/skills/kafka-conventions/
├── SKILL.md              # ngắn — nạp khi trigger
└── references/           # dài — chỉ nạp khi thật sự cần
    ├── topic-naming.md
    └── partition-keys.md
```

**Hai kỹ thuật quyết định skill có dùng được hay không:**

| Kỹ thuật | Vì sao cần |
|---|---|
| `USE FOR` / `DO NOT USE FOR` trong `description` | Agent đọc description để **chọn** skill. Có 7 skill mà không ghi rõ khi nào *không* dùng → agent chọn nhầm liên tục. |
| **Progressive disclosure** — `SKILL.md` ngắn, `references/` tải theo nhu cầu | Nhồi hết vào `SKILL.md` thì tốn context mỗi lần trigger, kể cả khi chỉ cần 1/10 nội dung. |

### Skill cần **tự viết** — xếp theo thứ tự làm

| # | Skill | Viết khi | Nội dung bắt buộc |
|---|---|---|---|
| 1 | **`proto-contract`** | Tuần 1 | Quy trình sửa `.proto`: `buf lint` → `buf breaking` → `make proto` → commit `gen/`. **Cấm sửa tay file trong `gen/`.** Version trong path (`v1/`). `rpc/` đặt tên động từ, `events/` đặt tên quá khứ. |
| 2 | **`ddd-dotnet`** | Tuần 1 | `Domain/` không tham chiếu `Infrastructure/` hay `building-blocks/`. Aggregate kế thừa `Entity`. Domain event là `record` immutable. Command handler nằm ở `Application/`. Đối chiếu với `tests/arch/` — skill và arch test phải nói **cùng một luật**. |
| 3 | **`kafka-conventions`** | Tháng 2 | Đặt tên topic, chọn partition key, bắt buộc DLQ + retry topic, inject trace context vào header, commit offset **sau** khi xử lý, consumer phải idempotent. |
| 4 | **`go-service`** | Tháng 2 | `cmd/server/main.go`, không `main.go` ở gốc. `internal/handler` không import thẳng `internal/repository`. Error wrapping, context propagation, structured log cùng field name với .NET. |
| 5 | **`flink-job`** | Tháng 4 | Mọi job **phải** khai báo watermark strategy và giải thích con số. Cấu hình checkpoint rõ ràng. Late data: side output hay drop — phải chọn có ý thức. Mỗi job một `README.md` giải thích thiết kế. |
| 6 | **`arc42-docs`** | Tháng 1 | Tài liệu mới thuộc chương nào. ADR vào `09-`, chuyên đề vào `08-`. Format ADR: Context / Decision / Consequences / Alternatives considered. |

### Skill nên **vendor** (không tự viết)

| Skill | Nguồn | Dùng cho |
|---|---|---|
| `aspire`, `aspireify`, `aspire-orchestration` | Microsoft | Nếu bạn dùng Aspire cho tầng .NET |
| `csharp-tunit` hoặc `csharp-xunit` | cộng đồng | Convention viết test C# |
| `vercel-react-best-practices` | Vercel | **Chỉ khi** làm dashboard bằng Next.js — nếu dùng Blazor thì bỏ |
| `catalog-documentation-creator` | EventCatalog | Nếu sau này publish event catalog dạng site |

> **Vendor là copy vào repo, không phải cài global.** Mục đích chính là **mọi người (và mọi máy) đều có AI hành xử giống nhau** — kể cả bạn của 3 tháng sau.

### Mẫu `SKILL.md` — dùng luôn cho `kafka-conventions`

```markdown
---
name: kafka-conventions
description: >-
  Quy tắc bắt buộc khi làm việc với Kafka trong repo này — đặt tên topic,
  chọn partition key, DLQ, idempotent consumer, trace context.
  USE FOR: tạo topic mới, viết producer/consumer, thêm integration event,
  sửa file trong proto/events/, cấu hình consumer group, xử lý retry.
  DO NOT USE FOR: Flink job (dùng flink-job), thay đổi schema proto
  (dùng proto-contract), gRPC request/response (dùng proto-contract).
metadata:
  version: "1.0"
---

# Kafka Conventions

## Đặt tên topic

`<context>.<aggregate>.<v1>` — ví dụ `ordering.order.v1`, `catalog.product.v1`.
Không dùng số nhiều. Không viết hoa. Version nằm ở cuối, không ở giữa.

## Partition key — luật cứng

| Topic | Key | Vì sao |
|---|---|---|
| `ordering.order.v1` | `OrderId` | Mọi event của một đơn phải giữ đúng thứ tự |
| `user.click.v1` | `SessionId` | Sessionization cần cùng session vào cùng partition |

**Không bao giờ** dùng `CustomerId` làm key cho `ordering.*` — khách VIP tạo hot partition.

## Bắt buộc với mọi consumer

1. Idempotent — xử lý lại cùng message không được đổi kết quả
2. Commit offset **sau** khi xử lý xong, không phải trước
3. Có DLQ: `<topic>.dlq`, và retry topic có exponential backoff
4. Extract trace context từ Kafka header (xem `references/tracing.md`)

## Trước khi tạo topic mới

Cập nhật `docs/08-cross-cutting-concepts/event-catalog.md` **trước**, code sau.
Không có dòng trong event catalog thì không có topic.
```

### `CLAUDE.md` ở gốc repo — ngắn thôi

Không lặp lại nội dung skill. Chỉ ghi những gì áp dụng cho **mọi** task:

```markdown
# Agent Instructions

## Lệnh chuẩn — luôn dùng, đừng gọi trực tiếp dotnet/go/pytest
- Build & chạy: `make up` · chỉ hạ tầng: `make infra`
- Test: `make test` · architecture test: `make arch` · lint: `make lint`
- Sinh lại stub proto: `make proto`

## Luật tuyệt đối
1. KHÔNG sửa tay file trong `building-blocks/gen/` — chạy `make proto`
2. KHÔNG để service này tham chiếu package của service khác
3. KHÔNG thêm topic Kafka mà chưa cập nhật event catalog
4. Mọi quyết định kiến trúc → một ADR trong `docs/09-architecture-decisions/`

## Khi làm việc với vùng cụ thể
Đọc skill tương ứng trong `.agents/skills/` trước khi viết code.
```

Thêm `AGENTS.md` một dòng trỏ về `CLAUDE.md` để agent nào cũng tìm được.

### Giữ skill không bị lỗi thời

| Rủi ro | Cách chặn |
|---|---|
| Skill nói một đằng, `tests/arch/` ép một nẻo | Khi sửa luật, **sửa cả hai trong cùng PR**. Ghi vào checklist PR template. |
| Skill viết rồi bỏ đó, không ai đọc | Cuối mỗi tháng, đọc lại skill của tháng đó — 15 phút |
| Vendor skill lỗi thời so với upstream | Ghi rõ nguồn + phiên bản trong `metadata`, kiểm tra lại ở tháng 6 |

---

## Việc cần làm NGAY

Trước khi viết dòng code đầu tiên, viết ba tài liệu này:

0. **`mise.toml`** — pin 4 toolchain. Mất 10 phút, cứu bạn khỏi hàng chục giờ debug môi trường.
1. **`docs/08-cross-cutting-concepts/service-boundaries.md`** — mỗi service sở hữu dữ liệu gì, **không** được biết gì
2. **`docs/08-cross-cutting-concepts/event-catalog.md`** — bảng: event · schema · producer · consumer · partition key
3. **`docs/09-architecture-decisions/001-why-polyglot.md`** — lý do chọn từng ngôn ngữ, kèm trade-off thừa nhận thẳng thắn
4. Khung `docs/` arc42 — 12 file rỗng chỉ có tiêu đề. Có khung rồi thì viết dần dễ hơn nhiều so với đối diện trang trắng.
5. **`CLAUDE.md` + `.agents/skills/proto-contract/` + `.agents/skills/ddd-dotnet/`** — xem [mục 15](#15-agent-skills--ghim-hướng-dẫn-ai-vào-repo). Viết trước khi có code thì AI sinh code đúng convention ngay từ file đầu tiên.

> Có ranh giới service và event catalog rồi thì `proto/` viết ra sẽ đúng ngay lần đầu — và `proto/` đúng thì mọi service viết sau đó đều đúng theo.
