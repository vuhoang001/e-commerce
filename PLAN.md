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
| 5 | **Thiếu `deploy/`, `docs/`, `tests/`, `tools/`** | Không có `docs/adr/` thì không ai biết bạn đã cân nhắc gì. Không có `tests/e2e/` thì không chứng minh được hệ thống chạy thật. |
| 6 | **Một `docker-compose.yml` cho tất cả** | Sẽ đau ở tháng thứ 2. Tách: `compose.infra.yml` (Kafka, Postgres, Redis...) chạy 1 lần rồi để đó, `compose.services.yml` restart liên tục. |

### Một câu hỏi kiến trúc còn thiếu

Bạn có **3 service và một web client**. Client sẽ gọi thẳng 3 service, hay qua một cửa?
→ Cần thêm **`api-gateway`** (C# / .NET + YARP). Không có nó thì trình duyệt phải nói gRPC (không làm được trực tiếp), phải xử lý CORS 3 lần, và auth phải làm 3 lần.

> ⚠️ **Lưu ý kỹ thuật quan trọng — xem [mục 3b](#3b-gateway-c--yarp-yarp-làm-được-gì-và-không-làm-được-gì).** YARP **không** dịch REST ↔ gRPC. Nó là reverse proxy HTTP. Việc dịch phải làm bằng cách khác, và điều đó thay đổi vai trò của gateway.

---

## 2. Cấu trúc hoàn chỉnh

```
ecommerce-polyglot/
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
│   │   ├── csharp/
│   │   ├── go/
│   │   ├── python/
│   │   └── java/
│   ├── dotnet/                     # NuGet nội bộ: logging, otel, kafka wrapper
│   ├── go/                         # module nội bộ: middleware, otel, kafka
│   ├── python/                     # package nội bộ
│   └── scripts/
│       ├── gen-proto.sh
│       ├── wait-for-it.sh
│       └── seed-data.py
│
├── deploy/
│   ├── k8s/
│   ├── connectors/                 # Debezium config
│   └── otel/                       # OpenTelemetry Collector config
│
├── tests/
│   ├── e2e/                        # k6 hoặc Playwright — chạy qua gateway
│   └── contract/                   # buf breaking + consumer-driven contract test
│
├── docs/
│   ├── adr/
│   ├── architecture.md
│   ├── event-catalog.md
│   ├── service-boundaries.md
│   └── runbook.md
│
└── tools/
    └── devcontainer/
```

**Thay đổi lớn nhất so với bản của bạn:** bỏ `src/` ngoài cùng, tách `proto/rpc` với `proto/events`, thêm `building-blocks/gen/`, thêm `api-gateway`.

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
> Với portfolio thì hợp lý, vì mục tiêu chính là **chứng minh năng lực đa nền tảng**. Hãy viết điều này ra trong `docs/adr/001-why-polyglot.md` — thừa nhận trade-off làm bạn đáng tin hơn là giả vờ không có.

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

lint:
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

> **Điểm khó — và cũng là điểm đáng khoe:** truyền trace context qua Kafka không tự động. Bạn phải tự inject vào header ở producer và extract ở consumer, ở **cả 4 ngôn ngữ**. Làm được thì viết `docs/adr/00X-distributed-tracing.md`.

---

## 8. Lộ trình 6 tháng

### Tháng 0.5 — Walking skeleton *(2 tuần)*

> **Mục tiêu:** một request đi từ browser → gateway → 1 service → DB, và thấy được trace.

- [ ] Khung repo theo cấu trúc mục 2
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
- [ ] **Partition key design** → `docs/partition-strategy.md`
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
      → `docs/adr/00X-saga-orchestration.md`
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
- [ ] **12–15 ADR** trong `docs/adr/`
- [ ] `docs/runbook.md` — "service X chết thì làm gì"
- [ ] README: sơ đồ + GIF demo + "chạy trong 2 lệnh"

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
| "Chọn partition key thế nào?" | `docs/partition-strategy.md` |
| "Late data xử lý ra sao?" | Job 3 — session window + side output |
| "Schema thay đổi thì sao?" | `buf breaking` trong CI + Schema Registry |
| "Consumer lag tăng, bạn làm gì?" | Grafana dashboard + rescale bằng savepoint |
| "CDC khác gì outbox polling?" | Tháng 2 — đã làm và đo cả hai |
| "Backpressure là gì?" | Flink UI + metric đã dựng |

### Vị trí Backend / Platform

| Câu hỏi | Trả lời bằng |
|---|---|
| "Chia ranh giới service thế nào?" | `docs/service-boundaries.md` |
| "Sao dùng nhiều ngôn ngữ?" | `docs/adr/001-why-polyglot.md` — kèm thừa nhận trade-off |
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

---

## Việc cần làm NGAY

Trước khi viết dòng code đầu tiên, viết ba tài liệu này:

1. **`docs/service-boundaries.md`** — mỗi service sở hữu dữ liệu gì, **không** được biết gì
2. **`docs/event-catalog.md`** — bảng: event · schema · producer · consumer · partition key
3. **`docs/adr/001-why-polyglot.md`** — lý do chọn từng ngôn ngữ, kèm trade-off thừa nhận thẳng thắn

> Có ranh giới service và event catalog rồi thì `proto/` viết ra sẽ đúng ngay lần đầu — và `proto/` đúng thì mọi service viết sau đó đều đúng theo.
