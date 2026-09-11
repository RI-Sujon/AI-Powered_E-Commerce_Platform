# UI integration for Phases 4–6

> Wires the three AI features into `Project.Web` (Razor Pages + jQuery), following the codebase's
> existing patterns rather than introducing a new stack: one `*Service.js` per API area, a shared
> component per widget, `Toast` for feedback, Bootstrap + the site's CSS custom properties for
> styling.

---

## What was added

| Feature | Where | Files |
|---|---|---|
| Shared API client | — | **new** `wwwroot/js/Services/AiService.js` |
| Phase 4 — generate description | Admin product modal | `wwwroot/js/Components/product-admin-modal.js` (edited) |
| Phase 5 — semantic search | Storefront product listing | `Pages/Product/Index.cshtml`, `wwwroot/js/Pages/product-home-page.js` (edited); `ProductSearchItem` enriched (`Project.Object/Responses/ProductSearchResponse.cs`, `ProductEmbeddingService.cs`) |
| Phase 6 — assistant chat | Global floating widget | **new** `Pages/Shared/_AssistantWidget.cshtml`, `wwwroot/js/Components/assistant-widget.js`; wired into `_Layout.cshtml` |
| Local dev | — | `Project.Web/Program.cs` — Kestrel port now reads `WEB_PORT` (default 8080, unchanged for Docker) so the API and Web can run side by side on one machine |

`AiService.js` matches `AdminService.js`/`ProductService.js` exactly — same `window.API_BASE_URL` + `ServiceUtils.getHeaders()` pattern — **except** `streamChat()`, which uses `fetch` + a `ReadableStream` reader instead of `$.ajax`, because Server-Sent Events need an incremental body read that jQuery's AJAX doesn't expose. That's the one deliberate deviation from the all-jQuery convention.

---

## Phase 4 — "Generate with AI"

A tone dropdown + button appear next to the Description field in the Add/Edit product modal — **Edit mode only**. The API endpoint reads the product's current facts from the DB by id, so it can't run before the product is first saved; Add mode shows a disabled hint instead ("Save the product first…"). Click → fills the Description textarea with the draft; the admin still reviews and edits before Save. Errors (AI not configured, content filter, etc.) surface through the existing `Toast.error()`.

## Phase 5 — AI Search toggle

A small magic-wand toggle sits next to the existing keyword search box on `/Product` — **off by default**, so the fast, reliable keyword search stays the default experience. When toggled on, Enter/search-icon calls `AiService.searchProducts()` instead of `ProductService.getProductList()`.

`ProductSearchItem` was missing `Stock`/`DiscountStartDate`/`DiscountEndDate` (it only had what Phase 5's backend needed at the time) — enriched it so AI-search hits render through the **existing `ItemCard` component**, unchanged, plus a small "N% match" badge derived from `Score`. Semantic search returns a flat ranked list (capped at 20), not true pages, so the pager and category/sort filters are disabled while AI Search is active. A 503 (AI unconfigured) falls back to keyword search automatically with an explanatory toast.

## Phase 6 — shopping assistant widget

A floating bubble (bottom-right, every page) opens a chat panel. `assistant-widget.js` keeps the conversation in memory + `sessionStorage` (no server persistence — matches the stateless API) and streams replies via `AiService.streamChat()`, parsing the `data: "delta"` / `event: error` / `data: [DONE]` SSE framing from the Phase 6 doc, growing the assistant's bubble token-by-token with a typing indicator and auto-scroll. Send is disabled while a reply streams.

---

## Verified

Built the whole solution (0 errors, 0 warnings) and ran the API (`:8080`) and Web (`:5000`, `WEB_PORT=5000`, `ApiBaseUrl=http://localhost:8080`) side by side against the real dev DB + Azure OpenAI:

- `dotnet test` → 8/8 passing.
- `/Product` page: single `AiService.js` include (no duplicate-declaration `SyntaxError` — it's loaded once, globally, in `_Layout.cshtml`), AI Search toggle and the assistant FAB both present in the rendered HTML and in a headless-Chrome screenshot.
- No console errors on page load (`chrome --headless --enable-logging=stderr`).
- Cross-origin (`:5000` → `:8080`) preflight + `GET /api/ai/products/search` + `POST /api/ai/assistant/chat` all returned correct `Access-Control-Allow-Origin` headers and worked — Dev CORS (`AllowAnyOrigin`) already covers this.
- `GET /api/ai/products/search?q=warm jacket` from the Web origin returned real ranked results **with** the newly-added `stock`/`discountStartDate`/`discountEndDate` fields — confirms the `ItemCard`-compatible enrichment reached the wire.
- The chat endpoint streamed real SSE frames cross-origin.

Not click-tested through an actual scripted browser session (login → edit product → Generate; type a query → toggle AI Search; open the widget → send a message) — that's the natural next check, and everything the click path depends on (the exact endpoints, auth headers, CORS, response shapes) is confirmed working.

---

## Not done here

- Turning assistant replies' product mentions into clickable cards (needs a response-format change or client-side name matching).
- Rate limiting on the now more-reachable anonymous `/search` and `/assistant/chat` endpoints — Phase 7.
- AI generation in Add-product mode (would need the backend to accept ad-hoc draft text not tied to a saved product id).
