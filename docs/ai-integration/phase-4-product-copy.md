# Phase 4 — Admin feature: AI product‑description generator

> **Goal:** first real AI feature. An admin opens a product, clicks "generate description", and
> gets a marketing‑copy **draft** built from that product's facts. They edit it and save it the
> normal way. The endpoint never writes to the database.
>
> Admin‑only, not customer‑facing → low prompt‑injection risk, a safe first slice.

---

## 1. The endpoint

```
POST /api/ai/products/{id}/generate-description        [Authorize(Roles = "Admin")]

body (all optional):
{ "tone": "premium and outdoorsy", "maxWords": 70 }

200:
{ "data": {
    "productId": 1,
    "draft": "The Trailhead 20L Daypack is designed for those who seek reliable performance…",
    "model": "gpt-4.1-mini-2025-04-14",
    "tokensIn": 168, "tokensOut": 85,
    "cached": false
  }, "isSuccess": true }
```

| Situation | Status |
|---|---|
| product doesn't exist | 404 (`KeyNotFoundException` from `GetProductById` → existing `ExceptionMiddleware`) |
| not logged in / not an Admin | 401 / 403 |
| `AzureOpenAI:Endpoint` not configured for this environment | 503 (`AiUnavailableException`) |
| content safety filter blocked it | 400 |

---

## 2. Design decisions

### Read‑only, human‑in‑the‑loop
The endpoint returns a draft and nothing else. The admin reviews it (LLMs still occasionally
drift) and saves via the existing `PUT /api/product/{id}`. AI writes a *suggestion*, a person
commits it.

### Grounded prompt (anti‑hallucination)
```
system:
  You are a senior e-commerce copywriter…
  - Use ONLY the facts provided. Never invent specifications, materials, dimensions,
    certifications, brand claims, or origin.
  - Do not mention price, discounts, shipping, or returns.
  - One paragraph, {maxWords} words maximum. No headings, no bullets, no markdown.
  - Tone: {tone}.
user:
  Product name: {Name}
  Category: {CategoryName ?? "not specified"}
  In stock: {yes|no}
  Current description (hint only):
  """ {Description} """
  Write a fresh description.
```
`ChatOptions { MaxOutputTokens = maxWords * 3, Temperature = 0.7 }`. The rules live in the
**system** message (instructions); the product data is in the **user** message (content).

### Response caching — with `IMemoryCache`, not the project's `ICacheProvider`
Repeated clicks on an unchanged product shouldn't re‑spend tokens. But the codebase's
`ICacheProvider` is registered `AddScoped` **and** `new`s its own `MemoryCache` in its
constructor — so every HTTP request gets a fresh, empty cache; it never caches across requests.
`ProductCopyService` uses **`IMemoryCache`** directly (a real singleton, `AddMemoryCache()` in
`Program.cs`), keyed by `SHA256(product fields + tone + maxWords)`, 1‑hour TTL. Editing the
product changes the key, so stale copy is never served.

### AI is an *optional* dependency
`ProductCopyService` gets `IChatClient` via `serviceProvider.GetService<IChatClient>()` — **not**
constructor injection. If AI isn't configured the client isn't registered; constructor injection
would throw and break every route on the controller. Instead the service resolves `null` and
throws `AiUnavailableException` → a clean **503**, and the rest of the API is unaffected.

---

## 3. Files

| File | Purpose |
|---|---|
| `Project.Object/Requests/GenerateProductDescriptionRequest.cs` | `Tone?`, `MaxWords?` (clamped 20–200, default 80) |
| `Project.Object/Responses/GenerateProductDescriptionResponse.cs` | draft + model + token counts + `Cached` flag |
| `Project.Core/AiUnavailableException.cs` | thrown when AI is off; mapped to 503 |
| `Project.Application/Service/Defination/IProductCopyService.cs` | the contract |
| `Project.Application/Service/ProductCopyService.cs` | fetch product → build prompt → call model → cache → return |
| `Project.Endpoint/Controllers/AiController.cs` | `POST /api/ai/products/{id}/generate-description`, `[Authorize(Roles="Admin")]` |
| `ManagersDependencyGroup.cs` | registers `IProductCopyService` |
| `Middleware/ExceptionMiddleware.cs` | `+ catch (AiUnavailableException) → 503` |
| `Program.cs` | `+ builder.Services.AddMemoryCache()` |

Layering is unchanged: controller → `IProductCopyService` (Application) → `IChatClient`
(abstraction from Phase 3) → Azure OpenAI.

---

## 4. Verified end to end (local run against `ecommercedb_dev` + the real deployment)

| Check | Result |
|---|---|
| `dotnet build` solution | ✅ 0 errors |
| `dotnet test` | ✅ 8/8 |
| generate for a real product | ✅ grounded — mentions "20 litre", "blue", "hiking"; invents no materials/price |
| call again, same `tone`+`maxWords` | ✅ `cached: true`, identical draft, no token spend |
| call with a different `tone` | ✅ `cached: false`, new draft |
| product id 999 | ✅ 404 |
| no `Authorization` header | ✅ 401 |

(To exercise the `[Authorize(Roles="Admin")]` path locally: register a user, then
`UPDATE "Users" SET "Role"='Admin' WHERE "Email"=…` — there's no seeded admin.)

---

## 5. Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| Second identical call still `cached:false` | `ICacheProvider` is `AddScoped` and builds its own `MemoryCache` per instance → per‑request only | use `IMemoryCache` (singleton) directly |
| `error CS8858: … 'with' … receiver type … is not a record type` | tried `cachedResponse with { Cached = true }` on a plain class | wrote a small `Copy(src, cached)` helper |
| Whole `AiController` 500s when AI is disabled | `IChatClient` constructor‑injected but not registered → controller can't be constructed | resolve it with `IServiceProvider.GetService<IChatClient>()` and null‑check |
| `MSB3021: file is being used by another process` on rebuild | `dotnet run` spawns a child host; killing the launcher PID leaves it running | `taskkill /IM Project.Endpoint.exe` (or the child PID) before building |

---

## 6. Cost

~250 tokens per generation on `gpt-4.1-mini` ≈ **$0.0001**. Cached repeats are free. The 1‑hour
cache plus admin‑only access keeps this negligible.

---

## 7. Next — Phase 5: semantic product search

Add `Pgvector.EntityFrameworkCore`, a `ProductEmbedding` table + EF migration, generate an
embedding per product with the Phase 3 `IEmbeddingGenerator`, and expose
`GET /api/product/search?q=…` that embeds the query and ranks by `embedding <=> queryVector`
(cosine distance, the `pgvector` from Phase 2). First customer‑facing AI feature.
