# AI Integration — roadmap

Adding Azure OpenAI to the ECommerceProject, one shippable slice at a time. Each phase has its
own doc with goal, concepts, implementation, outputs, verification, and the gotchas we hit.

| Phase | Doc | Status | Delivers |
|------|-----|--------|----------|
| 1 | [phase-1-infrastructure.md](phase-1-infrastructure.md) | ✅ done | Azure OpenAI account + `chat`/`embeddings` deployments; every API container app can call it keyless via managed identity |
| 2 | [phase-2-pgvector.md](phase-2-pgvector.md) | ✅ done | `pgvector` 0.8.2 enabled on the shared Postgres server (`ecommercedb` + `ecommercedb_dev`) |
| 3 | [phase-3-dotnet-wiring.md](phase-3-dotnet-wiring.md) | ✅ done | `Microsoft.Extensions.AI` DI wiring in the API (`IChatClient`, `IEmbeddingGenerator`) + `/ai/selftest` |
| 4 | [phase-4-product-copy.md](phase-4-product-copy.md) | ✅ done | Admin "generate product description" — `POST /api/ai/products/{id}/generate-description` |
| 5 | [phase-5-semantic-search.md](phase-5-semantic-search.md) | ✅ done | Storefront semantic search — `GET /api/ai/products/search` + `POST /api/ai/products/embeddings/backfill` |
| 6 | [phase-6-shopping-assistant.md](phase-6-shopping-assistant.md) | ✅ done | Customer chatbot — `POST /api/ai/assistant/chat` (SSE, `SearchProducts` tool) |
| 4–6 UI | [ui-integration.md](ui-integration.md) | ✅ done | Wires the three features into `Project.Web`: AI-generate button, AI Search toggle, floating assistant widget |
| 7 | [phase-7-ops.md](phase-7-ops.md) | 📖 reference only | Documented but **not implemented** — a learning project on this subscription doesn't need it. Explains rate limiting, `local_auth_enabled = false`, gating `/ai/selftest`, token logging, budget alerts, and a CI smoke test, for future reference. |

**Models chosen:** `gpt-4.1-mini` (chat) and `text-embedding-3-small` (embeddings) — cheap, and the
only mini‑class chat model this subscription has quota for (see Phase 1 doc).

**Auth model:** managed identity everywhere. No API keys are stored anywhere.
