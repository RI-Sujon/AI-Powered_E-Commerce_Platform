# Phase 2 — pgvector on the shared Postgres server

> **Goal:** make the existing PostgreSQL Flexible Server able to store and compare **vector
> embeddings**, so Phase 5 (semantic search) and Phase 6 (the shopping assistant's retrieval step)
> have a place to keep the numbers that represent "what a product means".
>
> Small phase: one Terraform-managed server parameter + one SQL statement per database.

---

## 1. Why a database change at all

An embedding is a fixed-length array of floats (1536 of them for `text-embedding-3-small`).
"Find products similar to this query" becomes "find the rows whose embedding vector is closest to
the query's embedding vector". Plain PostgreSQL has no vector type and no distance operators.
**`pgvector`** is the extension that adds:

- a `vector(N)` column type
- distance operators: `<->` (L2), `<#>` (inner product), `<=>` (**cosine distance** — what we'll use)
- approximate-nearest-neighbour indexes (`HNSW`, `IVFFlat`) so search stays fast as the catalog grows

Keeping embeddings *in the same database as the products* means one query can filter
(`price < 50`, `in_stock`) **and** rank by similarity — no separate vector database to run, pay
for, or keep in sync.

---

## 2. Two steps, two layers

| Step | Layer | Where it's done | Restart? |
|---|---|---|---|
| Allow the `vector` extension on the server | server parameter `azure.extensions` | **Terraform** (`shared` stack) | No — it's a *dynamic* parameter |
| Actually create the extension | `CREATE EXTENSION vector` | **SQL**, once per database | No |

Azure Flexible Server won't let you `CREATE EXTENSION x` unless `x` is on the
**`azure.extensions` allowlist** first. The allowlist is server-wide; `CREATE EXTENSION` is
per-database.

---

## 3. Implementation

### 3.1 Terraform — allow the extension (`terraform/environments/shared/`)

The server is created outside Terraform and is read-only in the dev/staging/prod stacks. But
`azure.extensions` is a **single server-wide setting**, so it belongs in the one stack that's
applied once — `shared` — not copied into three environment stacks.

```hcl
# main.tf
data "azurerm_postgresql_flexible_server" "shared" {
  name                = var.postgres_server_name        # ecommerce-postgres-sujon
  resource_group_name = data.azurerm_resource_group.shared.name
}

resource "azurerm_postgresql_flexible_server_configuration" "azure_extensions" {
  name      = "azure.extensions"
  server_id = data.azurerm_postgresql_flexible_server.shared.id
  value     = join(",", var.postgres_allowlisted_extensions)   # ["VECTOR"] -> "VECTOR"
}
```

```hcl
# variables.tf
variable "postgres_allowlisted_extensions" {
  type    = list(string)
  default = ["VECTOR"]   # a list so more extensions can be added later without a code change
}
```

Notes:
- The allowlist token is **`VECTOR`** (upper-case) even though the extension is `vector`.
- `azure.extensions` was **empty** before, so we're not clobbering anything. If it already had
  values you'd have to include them here too — this resource sets the *whole* comma-separated list.
- `isDynamicConfig = true` for this parameter → **no server restart**, no downtime.
- `terraform destroy` on the `shared` stack would reset the parameter to its default (empty),
  which would then block `CREATE EXTENSION` on new databases but not drop it from existing ones.

Apply:
```powershell
cd terraform/environments/shared
terraform apply
terraform output postgres_allowlisted_extensions   # -> "VECTOR"
```

### 3.2 SQL — create the extension per database

Run once against **every database the app uses**: `ecommercedb` (staging + prod) and
`ecommercedb_dev` (dev).

```sql
CREATE EXTENSION IF NOT EXISTS vector;
SELECT extversion FROM pg_extension WHERE extname = 'vector';   -- 0.8.2
```

We ran this with a throwaway Npgsql program (psql isn't installed locally and the
`az postgres … execute` CLI extension wouldn't install on this machine — a Windows code-page bug).
Any of these work:
- a tiny `NpgsqlConnection` + `CREATE EXTENSION` (what we did)
- `psql "host=… dbname=… user=postgres sslmode=require" -c "CREATE EXTENSION IF NOT EXISTS vector;"`
- **Phase 3 will also put `CREATE EXTENSION IF NOT EXISTS vector;` as the first line of the EF
  migration** that adds the embedding column — it's idempotent, so a rebuilt database self-heals.

`IF NOT EXISTS` makes it safe to run repeatedly.

---

## 4. What exists now

| | Before | After |
|---|---|---|
| `azure.extensions` on `ecommerce-postgres-sujon` | `""` | `"VECTOR"` (managed by Terraform) |
| `vector` extension in `ecommercedb` | absent | **0.8.2** |
| `vector` extension in `ecommercedb_dev` | absent | **0.8.2** |

Functional check that ran clean in both databases:
```sql
SELECT '[1,0,0]'::vector <=> '[0,1,0]'::vector;   -- => 1  (orthogonal vectors, cosine distance 1)
```

`terraform plan` in the `shared` stack: **No changes.**

---

## 5. Concepts / notes for later

- **Dimensions:** `text-embedding-3-small` → `vector(1536)`. pgvector indexes support up to 2000
  dimensions, so 1536 is fine to index directly.
- **Index (Phase 5):** once there's data, add an HNSW index for fast ANN search:
  ```sql
  CREATE INDEX ON product_embeddings USING hnsw (embedding vector_cosine_ops);
  ```
  HNSW = good recall, fast queries, slower inserts, more memory. `IVFFlat` is the lighter
  alternative. Build the index **after** bulk-loading embeddings, not before.
- **Distance operator must match the index opclass:** query with `<=>` ⇒ index with
  `vector_cosine_ops`. Mixing them silently disables the index.
- **Normalisation:** OpenAI embeddings are already L2-normalised, so cosine distance and inner
  product rank identically — either operator is fine.
- **.NET mapping (Phase 3/5):** the `Pgvector` + `Pgvector.EntityFrameworkCore` NuGet packages
  give a `Vector` type and `EF.Functions`/`CosineDistance` for LINQ queries.

---

## 6. Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| `extension "vector" is not allow-listed` on `CREATE EXTENSION` | `azure.extensions` doesn't include it | add `VECTOR` to the allowlist first (Terraform) |
| Set the allowlist but it wiped another extension | `azurerm_postgresql_flexible_server_configuration` replaces the **entire** value | put every needed token in `postgres_allowlisted_extensions` |
| Extension present in one DB, missing in another | `CREATE EXTENSION` is per-database | run it in `ecommercedb` **and** `ecommercedb_dev` |
| `az postgres flexible-server execute` fails to install | unrelated Windows code-page bug in the CLI extension installer | use psql or a small Npgsql script instead |

---

## 7. Cost

Zero. `pgvector` is a free extension; no new Azure resource. Storage for the vectors themselves is
tiny (1536 × 4 bytes ≈ 6 KB per product).

---

## 8. Next — Phase 3: .NET wiring

Add `Microsoft.Extensions.AI` + `Azure.AI.OpenAI` to the API, register `IChatClient` and
`IEmbeddingGenerator` in `Program.cs` against the Phase 1 endpoint using `DefaultAzureCredential`,
and add `Pgvector.EntityFrameworkCore` so the embedding column has a CLR type. No feature yet —
just the building blocks the admin copy generator (Phase 4) and semantic search (Phase 5) sit on.
