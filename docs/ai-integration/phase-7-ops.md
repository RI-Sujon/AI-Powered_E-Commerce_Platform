# Phase 7 — Operational hardening (reference only, not implemented)

> **Status:** this phase was **deliberately not built**. This project is a learning exercise, not
> a production system, and the actual Azure OpenAI spend here is tiny — the `chat` and
> `embeddings` deployments are capped at 20K TPM each (Phase 1), the models are the cheapest
> viable ones (`gpt-4.1-mini`, `text-embedding-3-small`), and nothing runs unattended. Realistic
> cost at demo/learning volume is a few **cents to low dollars** a month, not something that needs
> a budget alarm.
>
> This doc exists so the *ideas* aren't lost: it's what you'd actually do before letting strangers
> use this for real, and why. Nothing here has been applied.

---

## 1. Why this phase exists at all

Phases 1–6 answer *"does it work?"*. Phase 7 answers *"is it safe to leave running and reachable
by anyone on the internet?"* — a different question. Two things changed once Phase 6 shipped a
public chat widget:

1. **Cost became reachable by anonymous visitors.** `GET /api/ai/products/search` and
   `POST /api/ai/assistant/chat` (Phase 5, 6) need no login. Every call spends real tokens. A
   script hammering either endpoint spends your money, not a customer's.
2. **A debug endpoint went live alongside them.** `GET /ai/selftest` (added in Phase 3 to prove
   the wiring worked) is still anonymous and still burns a chat + embedding call per hit.

Neither is dangerous at this project's scale. Both are exactly the class of thing that *does*
matter once real traffic (or a bot) shows up.

---

## 2. Rate limiting the anonymous AI endpoints

**The problem:** the app already has a rate limiter (`AddRateLimiter` in `Program.cs`, used by
`AuthController` via `[EnableRateLimiting("auth")]`) — but it's a single **global** fixed window
(10 requests/minute shared by *every* caller combined, not per-caller). That's fine for login
(you want to slow down credential-stuffing overall) but wrong for a cost‑bearing endpoint: one
noisy client would also throttle everyone else, and a moderately patient script could still spend
steadily forever within that global budget.

**What you'd add** — a **partitioned** limiter, keyed per client IP, so each caller gets their own
budget:

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", /* existing, unchanged */);

    options.AddPolicy("ai", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,          // 20 AI calls per IP per minute
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0              // reject immediately over the limit, don't queue
        }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

```csharp
// AiController.cs
[AllowAnonymous, EnableRateLimiting("ai")]
[HttpGet("products/search")]
public async Task<ActionResult> SearchProducts(...) { ... }

[AllowAnonymous, EnableRateLimiting("ai")]
[HttpPost("assistant/chat")]
public async Task ChatAssistant(...) { ... }
```

Admin endpoints (`generate-description`, `embeddings/backfill`) are left unlimited — they already
require an Admin JWT, a much smaller abuse surface than "anyone on the internet."

**Behind a reverse proxy / CDN**, `RemoteIpAddress` is the proxy's IP unless you configure
`ForwardedHeaders` — worth checking if this ever sits behind Azure Front Door or similar.

---

## 3. Turn off API-key auth on the OpenAI account

`terraform/modules/openai` still has `local_auth_enabled = true` (Phase 1 default, "easy local
dev"). Every caller in this codebase has used **managed identity** (`DefaultAzureCredential`)
since Phase 3 — the container's user-assigned identity in Azure, `az login` locally. No code path
actually needs an API key anymore.

```hcl
# terraform/environments/shared/terraform.tfvars
openai_local_auth_enabled = false
```

```powershell
cd terraform/environments/shared
terraform apply
```

**Why it matters:** an API key is a bearer secret — anyone who gets it (leaked env var, log line,
compromised dependency) can call the model as if they were you, with no way to tell *which*
caller it was. Managed identity tokens are short-lived, scoped to one Azure resource, and every
call is attributable to a specific identity in Azure AD sign-in logs. Turning off the key entirely
removes the leak surface rather than just avoiding using it.

---

## 4. Gate or remove the debug endpoint

`GET /ai/selftest` (Phase 3) is anonymous and costs one embedding + one short chat call per hit.
It did its job proving the deployment was wired correctly; once `/api/ai/*` exists, it's
redundant with the real features but still publicly poke-able.

```csharp
app.MapGet("/ai/selftest", async (HttpContext ctx) => { ... })
   .RequireAuthorization(policy => policy.RequireRole("Admin"));
```

(Minimal APIs use `.RequireAuthorization(...)` rather than an `[Authorize]` attribute — there's no
class to put it on.) Keeping it — just gated — is more useful than deleting it: it's a fast,
one-line way to confirm AI wiring in any environment without needing seeded product data or a
frontend.

---

## 5. Token-usage logging (cost observability)

Both `ProductCopyService` (Phase 4) and `AssistantService` (Phase 6) already get
`ChatResponse.Usage` / per-update `UsageContent` back from the model — Phase 4 even returns
`TokensIn`/`TokensOut` in its API response — but nothing writes it to the logs in one consistent,
greppable shape. A single line per call is enough to eyeball cost trends in Log Analytics:

```csharp
_ctx.Log.LogInformation(
    $"AI usage: feature=ProductCopy productId={productId} " +
    $"tokensIn={result.TokensIn} tokensOut={result.TokensOut} model={result.Model}");
```

For the streaming assistant, usage typically arrives as a `UsageContent` inside the *last*
`ChatResponseUpdate.Contents`:

```csharp
UsageDetails? usage = null;
await foreach (var update in _chat.GetStreamingResponseAsync(chat, options, ct))
{
    usage = update.Contents.OfType<UsageContent>().FirstOrDefault()?.Details ?? usage;
    if (!string.IsNullOrEmpty(update.Text)) yield return update.Text;
}
_ctx.Log.LogInformation($"AI usage: feature=Assistant tokensIn={usage?.InputTokenCount} tokensOut={usage?.OutputTokenCount}");
```

Embeddings (Phase 5) were deliberately left out of this — at ~$0.00000002 per call, per-call
token logging there adds log noise for no real cost signal; the existing "embedded N products"
count logs are enough.

---

## 6. A budget alert (the one thing worth doing manually instead)

For a learning project, **the Azure Portal is genuinely the better tool here**, not Terraform:
*Cost Management + Billing → Budgets → + Add* — pick the resource group, a monthly amount, an
email — two minutes, no state file, easy to delete when the project is done. The Terraform
equivalent (`azurerm_consumption_budget_resource_group`) is worth knowing exists, but for a single
person watching a single learning subscription it's more ceremony than the manual click-through:

```hcl
resource "azurerm_consumption_budget_resource_group" "ecommerce" {
  name              = "ecommerce-monthly-budget"
  resource_group_id = data.azurerm_resource_group.shared.id
  amount            = 15
  time_grain        = "Monthly"
  time_period { start_date = "2026-09-01T00:00:00Z" }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThan"
    contact_emails = ["you@example.com"]
  }
}
```

If you ever want it, it's a self-contained resource in the `shared` stack (applied by a human,
like the rest of that stack) — safe to add later without touching anything else in this doc.

---

## 7. A CI smoke test

The pipeline (Phase 1's `azure-pipelines.yml`) deploys and moves on — nothing confirms the AI
wiring actually works *after* a deploy. A cheap, real check: call the anonymous search endpoint
(exercises embeddings end-to-end) right after `terraform output` in each deploy stage:

```powershell
$apiUrl = terraform output -raw api_url
$ok = $false
for ($i = 0; $i -lt 3; $i++) {
  try {
    $resp = Invoke-RestMethod -Uri "$apiUrl/api/ai/products/search?q=smoke-test&take=1" -TimeoutSec 30
    if ($resp.isSuccess) { $ok = $true; break }
  } catch { Start-Sleep -Seconds 15 }
}
if (-not $ok) { throw "AI smoke test failed: /api/ai/products/search didn't return isSuccess=true" }
```

This is exactly the class of check that catches "the managed identity role assignment didn't
apply", "the deployment name changed", or "the endpoint env var is wrong" **before** you find out
by clicking around manually — the same motivation as the `Validate` stage added back when the
pipeline was built.

---

## 8. If this ever stops being "just for learning"

Rough order of what would actually matter, cheapest-to-fix first:

1. Rate limit the two anonymous endpoints (§2) — five minutes, prevents the one scenario that
   could genuinely surprise you on a bill.
2. Gate `/ai/selftest` (§4) — one line.
3. Turn off `local_auth_enabled` (§3) — one `terraform apply`.
4. Add the budget alert (§6) — two minutes in the Portal, or promote to Terraform once there's a
   reason to (e.g. multiple environments, a team, infra-as-code discipline already in place).
5. Token usage logging (§5) and the CI smoke test (§7) — polish, do them when convenient.

None of this blocks anything — Phases 1–6 are complete, tested, and usable as they stand.
