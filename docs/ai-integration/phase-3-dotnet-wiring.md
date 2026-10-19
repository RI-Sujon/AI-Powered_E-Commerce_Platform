# Phase 3 — .NET wiring: `IChatClient` + `IEmbeddingGenerator` in the API

> **Goal:** make the API able to *call* the Phase 1 Azure OpenAI deployments from C# — register
> the two client abstractions in DI, keyless, and prove it end to end. No user‑facing feature yet;
> this is the layer Phases 4–6 sit on.

---

## 1. The idea: program against an abstraction, not a vendor SDK

`Microsoft.Extensions.AI` is to AI what `ILogger` is to logging — a small set of interfaces every
provider implements:

| Interface | What it does | Our feature that uses it |
|---|---|---|
| `IChatClient` | send messages, get a reply (optionally streaming, with tool calls) | Phase 4 copy, Phase 6 assistant |
| `IEmbeddingGenerator<string, Embedding<float>>` | turn text into a vector | Phase 5 search, Phase 6 retrieval |

Feature code depends only on those interfaces. The concrete client (Azure OpenAI here, but it could
be OpenAI, Ollama, AWS Bedrock…) is chosen once, in one file. Swapping providers later doesn't
touch a single service class.

```
feature services ──> IChatClient / IEmbeddingGenerator      (Microsoft.Extensions.AI.Abstractions)
                              │  .AsIChatClient() / .AsIEmbeddingGenerator()   (Microsoft.Extensions.AI.OpenAI)
                              ▼
                     AzureOpenAIClient(endpoint, DefaultAzureCredential)        (Azure.AI.OpenAI)
                              ▼
                     https://ecommerce-openai-sujon.openai.azure.com/
```

`AzureOpenAIClient` (not the base `OpenAIClient`) is what targets an **Azure** endpoint: it uses
**deployment names** (`chat`, `embeddings`) instead of raw model ids and accepts **Entra ID
tokens**.

---

## 2. What changed

### 2.1 Packages (`Project.Application`)

```xml
<PackageReference Include="Microsoft.Extensions.AI" Version="10.10.0" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.10.0" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
<PackageReference Include="Azure.Identity" Version="1.21.0" />
```

**Framework‑package bump (unavoidable):** `Microsoft.Extensions.AI` 10.x and the `Azure.Core`
it pulls in require `Microsoft.Extensions.Caching.*` / `Microsoft.Extensions.Configuration.*`
**≥ 10.0.x**, but every project pinned `9.0.5`. The solution builds with
`TreatWarningsAsErrors`, so the resulting `NU1605` *package downgrade* warnings failed the build.
Fix: bump those four framework packages `9.0.5 → 10.0.12` everywhere they're referenced
(`Project.Endpoint`, `Data`, `Application`, `Core`, `Object`, `Test`, `Web`). They multi‑target
and run fine on `net9.0`; EF Core 9.0.5 is unaffected (its `≥ 9.x` constraints still hold).

### 2.2 New file — `Project.Application/Extensions/AiDependencyGroup.cs`

Follows the existing `ManagersDependencyGroup` pattern.

```csharp
public static IServiceCollection AddAiDependencyGroup(this IServiceCollection services, IConfiguration config)
{
    var endpoint = config["AzureOpenAI:Endpoint"];
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        Console.WriteLine("i  AzureOpenAI:Endpoint not configured - AI features disabled.");
        return services;                         // app still boots; AI endpoints return 503
    }

    var chatDeployment      = config["AzureOpenAI:ChatDeployment"]      ?? "chat";
    var embeddingDeployment = config["AzureOpenAI:EmbeddingDeployment"] ?? "embeddings";

    var azureOpenAi = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

    services.AddChatClient(azureOpenAi.GetChatClient(chatDeployment).AsIChatClient())
            .UseLogging();

    services.AddEmbeddingGenerator(
        azureOpenAi.GetEmbeddingClient(embeddingDeployment).AsIEmbeddingGenerator());

    return services;
}
```

- **`AddChatClient(...)` returns a builder** — `.UseLogging()` wraps the client in a logging
  decorator. Phase 6 will add `.UseFunctionInvocation()` (tool calling). This is the
  `Microsoft.Extensions.AI` middleware pipeline — same decorator idea as ASP.NET middleware.
- **Graceful when unconfigured.** If `AzureOpenAI:Endpoint` is blank the clients aren't
  registered and the app still runs — handy for a bare local checkout.

### 2.3 `Program.cs`

```csharp
builder.Services.AddManagersDependencyGroup();
builder.Services.AddAiDependencyGroup(builder.Configuration);   // <-- added
```

Plus a self‑test endpoint next to `/health`:

```csharp
app.MapGet("/ai/selftest", async (HttpContext ctx) =>
{
    var chat       = ctx.RequestServices.GetService<IChatClient>();
    var embeddings = ctx.RequestServices.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
    if (chat is null || embeddings is null)
        return Results.Json(new { ok = false, reason = "AzureOpenAI:Endpoint not configured" }, statusCode: 503);

    var vector = await embeddings.GenerateVectorAsync("waterproof hiking jacket");
    var reply  = await chat.GetResponseAsync("Reply with exactly: OK");
    return Results.Json(new { ok = true, chat = reply.Text, embeddingDimensions = vector.Length,
                              tokensIn = reply.Usage?.InputTokenCount, tokensOut = reply.Usage?.OutputTokenCount });
});
```

> Costs a fraction of a cent per call. `TODO(phase 7)`: gate behind admin auth or delete before
> real production traffic.

### 2.4 `appsettings.json`

```json
"AzureOpenAI": {
  "Endpoint": "",
  "ChatDeployment": "chat",
  "EmbeddingDeployment": "embeddings"
}
```

The **deployed** app doesn't use these blanks — Terraform (Phase 1) already injects
`AzureOpenAI__Endpoint`, `AzureOpenAI__ChatDeployment`, `AzureOpenAI__EmbeddingDeployment` as
container env vars. .NET maps `AzureOpenAI__Endpoint` (env) → `AzureOpenAI:Endpoint` (config key);
`__` is the nesting separator.

---

## 3. Auth — nothing new to configure

`new DefaultAzureCredential()` walks a chain of credential sources and uses the first that works:

| Where | What it finds | Set up by |
|---|---|---|
| In the container | the user‑assigned managed identity, selected by the `AZURE_CLIENT_ID` env var | Phase 1 Terraform |
| Locally | your `az login` session | you |

Both principals were granted `Cognitive Services OpenAI User` on the account in Phase 1, so the
same code works in both places with **zero secrets**.

---

## 4. Verification

```
dotnet build ProjectMainApp.sln -c Release        # Build succeeded (1 pre-existing warning)
dotnet test  Project.Test                          # Passed! 8/8

# ran the API locally against the real dev DB + OpenAI endpoint:
GET http://localhost:8080/ai/selftest
{ "ok": true, "chat": "OK", "embeddingDimensions": 1536, "tokensIn": 12, "tokensOut": 2 }
```

Startup log line to look for:
```
AI: Azure OpenAI wired - https://ecommerce-openai-sujon.openai.azure.com/ (chat=chat, embeddings=embeddings)
```

**After the next pipeline deploy**, confirm in the cloud:
```
curl https://ecommerce-api-dev.whitewater-3611f9ba.eastus.azurecontainerapps.io/ai/selftest
```
It should return `ok: true` with no extra config — the env vars are already there from Phase 1.

---

## 5. Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| `NU1605 … package downgrade … 10.0.12 to 9.0.5 … Warning As Error` | `Microsoft.Extensions.AI` 10.x + transitive `Azure.Core` need `Microsoft.Extensions.Caching/Configuration` ≥ 10.x; projects pinned 9.0.5; solution treats warnings as errors | bump those framework packages to `10.0.12` in every project that references them |
| Build fine, but which `OpenAI` SDK version wins? | `Azure.AI.OpenAI` 2.1.0 → `OpenAI` 2.1.0; `Microsoft.Extensions.AI.OpenAI` 10.10.0 → `OpenAI` 2.13.0 | NuGet unifies to 2.13.0; it's binary‑compatible within v2 — verified by the working self‑test |
| First `/ai/selftest` call is slow **locally** | `DefaultAzureCredential` probes the IMDS managed‑identity endpoint (a few seconds to time out) before falling back to Azure CLI | expected; instant in‑container where the MI is real |
| `CS0618 ExcludeSharedTokenCacheCredential is obsolete` warning | pre‑existing, in the Key Vault block — not from this phase | left as‑is |

---

## 6. Cost

`/ai/selftest` ≈ 12 in + 2 out chat tokens + one short embedding ≈ **~$0.00001 per call**. Nothing
else calls Azure OpenAI yet.

---

## 7. Next — Phase 4: admin product‑description generator

First real feature. A `ProductCopyService` in `Project.Application` that injects `IChatClient`,
a prompt that turns a product's name + attributes into marketing copy, and
`POST /api/admin/products/{id}/description:generate` returning a draft the admin edits and saves.
Reuse the existing `AddRateLimiter` and `ICacheProvider`. Not user‑facing → low prompt‑injection
risk, good first slice.
