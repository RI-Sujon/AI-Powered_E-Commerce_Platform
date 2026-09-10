# AI Integration — roadmap

Adding Azure OpenAI to the ECommerceProject, one shippable slice at a time. Each phase has its
own doc with goal, concepts, implementation, outputs, verification, and the gotchas we hit.

| Phase | Doc | Status | Delivers |
|------|-----|--------|----------|
| 1 | [phase-1-infrastructure.md](phase-1-infrastructure.md) | ✅ done | Azure OpenAI account + `chat`/`embeddings` deployments; every API container app can call it keyless via managed identity |
| 2 | [phase-2-pgvector.md](phase-2-pgvector.md) | ✅ done | `pgvector` 0.8.2 enabled on the shared Postgres server (`ecommercedb` + `ecommercedb_dev`) |
| 3 | phase-3-dotnet-wiring.md | ⬜ next | `Microsoft.Extensions.AI` DI wiring in the API (`IChatClient`, `IEmbeddingGenerator`) |
| 4 | phase-4-product-copy.md | ⬜ | Admin "generate product description" feature |
| 5 | phase-5-semantic-search.md | ⬜ | Storefront semantic product search (embeddings + pgvector) |
| 6 | phase-6-shopping-assistant.md | ⬜ | Customer product‑discovery chatbot (RAG + streaming + tool calling) |
| 7 | phase-7-ops.md | ⬜ | Budget alert, `local_auth_enabled = false`, token logging, CI smoke test |

**Models chosen:** `gpt-4.1-mini` (chat) and `text-embedding-3-small` (embeddings) — cheap, and the
only mini‑class chat model this subscription has quota for (see Phase 1 doc).

**Auth model:** managed identity everywhere. No API keys are stored anywhere.
