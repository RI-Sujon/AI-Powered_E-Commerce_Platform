# Phase 5 — Semantic product search (embeddings + pgvector)

> **Goal:** first customer‑facing AI feature. A shopper types *"something warm for a winter hike"*
> and gets the down jacket and the rain shell — not just keyword matches. The query is turned
> into a vector and products are ranked by how close their vectors are.

---

## 1. Endpoints

```
GET  /api/ai/products/search?q=<text>&take=10          (anonymous - storefront)
POST /api/ai/products/embeddings/backfill               (Admin)
```

`search` response:
```json
{ "data": {
    "query": "something warm for a winter hike",
    "count": 3,
    "results": [
      { "id": 2, "name": "Alpine Down Jacket",  "slug": "alpine-down-jacket",  "price": 189.0, "score": 0.4663 },
      { "id": 5, "name": "Waterproof Rain Shell","slug": "waterproof-rain-shell","price": 140.0, "score": 0.4325 },
      { "id": 3, "name": "Trail Runner Shoes",   "slug": "trail-runner-shoes",   "price": 95.0,  "score": 0.2991 }
    ] } }
```
`score` = `1 − cosineDistance` (higher = closer). Only `IsActive` products are returned.

---

## 2. How it works

```
                         ┌─────────────── write path ───────────────┐
 admin adds/edits a product ──► ProductService ──► ProductEmbeddingService.UpsertAsync
                                                      │  embed "Name\nCategory: X\nDescription"
                                                      ▼
                                            ProductEmbeddings row  (vector(1536), SourceHash)

                         ┌─────────────── read path ────────────────┐
 GET /search?q=… ──► ProductEmbeddingService.SearchAsync
                        │  embed the query text
                        ▼
      SELECT … FROM "ProductEmbeddings" e JOIN "Products" p …
      WHERE p."IsActive"  ORDER BY e."Embedding" <=> :queryVector  LIMIT :take
```

- **One `ProductEmbeddings` row per product**, PK = FK to `Products.Id` (cascade delete).
- **`SourceHash`** is the SHA‑256 of the exact embedded text. On save we re‑embed only if the hash
  changed → editing an unrelated field (price, stock) spends no tokens.
- **`<=>`** is pgvector's cosine‑distance operator (Phase 2). The HNSW index uses
  `vector_cosine_ops` so the operator and the index agree.

---

## 3. Implementation

### 3.1 Packages
| Project | Added |
|---|---|
| `Project.Object` | `Pgvector` 0.3.2 (the `Vector` type on the entity) |
| `Project.Data`, `Project.Application`, `Project.Endpoint` | `Pgvector.EntityFrameworkCore` 0.3.0 (`UseVector()`, `CosineDistance()` LINQ) |

### 3.2 Entity + DbContext
`ProductEmbeddingEntity` — `ProductId`, `Vector? Embedding`, `SourceHash`, `Model`, `UpdatedAt`.

```csharp
// AppDbContext.OnModelCreating - Postgres only; the in-memory provider used by the
// unit tests can't map `vector`, so the entity is excluded there.
if (Database.IsNpgsql())
{
    builder.HasPostgresExtension("vector");
    builder.Entity<ProductEmbeddingEntity>(e =>
    {
        e.HasKey(x => x.ProductId);
        e.HasOne<ProductEntity>().WithOne()
         .HasForeignKey<ProductEmbeddingEntity>(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.Embedding).HasColumnType("vector(1536)");
    });
}
else
{
    builder.Ignore<ProductEmbeddingEntity>();
}
```

```csharp
// Program.cs
options.UseNpgsql(connectionString, npgsql => npgsql.UseVector());
```

### 3.3 Migration `AddProductEmbeddings`
`CREATE TABLE "ProductEmbeddings"` + FK + `CREATE EXTENSION IF NOT EXISTS vector` (auto, from
`HasPostgresExtension`). Then a hand‑added line for the ANN index:

```csharp
migrationBuilder.Sql(
  "CREATE INDEX IF NOT EXISTS \"IX_ProductEmbeddings_Embedding_hnsw\" " +
  "ON \"ProductEmbeddings\" USING hnsw (\"Embedding\" vector_cosine_ops);");
```

**Applied to both databases:** `ecommercedb_dev` auto‑migrates on a Dev run; `ecommercedb`
(staging/prod) was updated once by hand —
`dotnet ef database update --connection "…Database=ecommercedb…"`. Existing products in
`ecommercedb` have **no embedding yet** — run the backfill endpoint there once.

### 3.4 `ProductEmbeddingService`
- `UpsertAsync(productId)` — best‑effort: returns `false` (never throws) when AI is off, so it's
  safe on the product‑save path. Two plain queries for product + category name (composing a filter
  onto a left‑join projection doesn't translate in EF Core).
- `BackfillAsync()` — loads all products, compares hashes, **batch‑embeds** the stale ones with a
  single `IEmbeddingGenerator.GenerateAsync(IEnumerable<string>)` call, returns the count.
- `SearchAsync(query, take)` — clamps `take` to 1‑50, embeds the query, runs the ordered join.
  Throws `AiUnavailableException` (→ 503) when AI is off; `ArgumentException` (→ 400) on empty `q`.

### 3.5 Write‑path hook
`ProductService.AddProduct` / `UpdateProduct` call `RefreshEmbeddingBestEffort(id)` after the DB
write — wrapped in try/catch so an Azure OpenAI hiccup can never fail a catalog edit. `Delete`
needs nothing (cascade FK).

---

## 4. Verified end to end (local, against `ecommercedb_dev` + real Azure OpenAI)

| Check | Result |
|---|---|
| `dotnet build` + `dotnet test` | ✅ 0 errors, 8/8 |
| add 5 products | ✅ each embedded inline (`ProductEmbedding: upserted product N`) |
| backfill afterwards | ✅ `updated: 0` (nothing stale) |
| `search "something warm for a winter hike"` | ✅ Down Jacket (0.47) → Rain Shell (0.43) → Trail Shoes (0.30) |
| `search "keep my coffee hot"` | ✅ Stainless Water Bottle top (0.35) — matched "insulated", not the word "hot" |
| `search "poles for balance on steep trails"` | ✅ Trekking Poles (0.50) |
| empty `q` / no auth on backfill | ✅ 400 / 401 |
| HNSW index present in both DBs | ✅ |

Local test products + user removed from `ecommercedb_dev` afterwards.

---

## 5. Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| `dotnet ef migrations add` produces an **empty** `Up()` | ran it with `--no-build`; stale `AppDbContextModelSnapshot` in the compiled DLL | run without `--no-build` (let ef build first) |
| `database update`: `PendingModelChangesWarning … model has pending changes` | same stale‑build cause — the built snapshot lagged the source | rebuild, then `database update` |
| all 8 unit tests fail after adding the entity | the `UseInMemoryDatabase` test provider can't map the `vector` column type / `HasPostgresExtension` | guard the pgvector model config with `if (Database.IsNpgsql())`, `builder.Ignore<…>()` otherwise |
| write‑path hook logged `The LINQ expression … could not be translated` | `groupjoin.DefaultIfEmpty().Where(id==…).First()` isn't translatable | fetch product + category name as two simple queries |
| `MSB3021 file in use` on rebuild | `dotnet run` child host still alive after killing the launcher PID | `taskkill /IM Project.Endpoint.exe` first |

---

## 6. Cost & scale

- Embedding a product ≈ **20–40 tokens** on `text-embedding-3-small` ≈ **$0.0000004**. A search =
  one query embedding, same order. Effectively free at this scale.
- `SourceHash` avoids re‑embedding unchanged text.
- HNSW index is overkill under ~1k products (a sequential scan is fine) but it's in place, so the
  query plan doesn't change as the catalog grows.

---

## 7. Next — Phase 6: the shopping‑assistant chatbot

Combine everything: `POST /api/ai/assistant/chat` (anonymous, streaming). The model gets a
`search_products` **tool** backed by `SearchAsync`, so it can look things up mid‑conversation and
recommend only real catalog items. Adds `.UseFunctionInvocation()` to the chat pipeline, SSE
streaming, and a chat‑history cap.
