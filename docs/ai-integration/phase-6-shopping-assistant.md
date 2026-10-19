# Phase 6 — Shopping-assistant chatbot (RAG + tool calling + streaming)

> **Goal:** the capstone. A shopper types *"a gift for a 10-year-old who loves science, ~$40"*
> and gets a conversational reply that recommends **real catalog items** — because the model can
> call a `SearchProducts` tool mid-conversation. Anonymous, product-discovery only (no login, no
> orders), streamed token-by-token.

---

## 1. Endpoint

```
POST /api/ai/assistant/chat          (anonymous)

body:  { "messages": [ { "role": "user", "content": "..." },
                       { "role": "assistant", "content": "..." }, ... ] }

response: text/event-stream
    data: "The "
    data: "Kids Science Kit "
    data: "($34.99) is a great fit."
    data: [DONE]

on failure:
    event: error
    data: "The assistant is unavailable right now."
    data: [DONE]
```

The server is **stateless** — the browser keeps the running conversation and resends it each
turn. Only the last 12 messages are used; each is capped at 4000 chars.

---

## 2. How it answers: RAG via a tool, not pre-retrieval

Phase 5's search could have been wired as "always embed the user's last message, stuff results
into the prompt". Instead the model gets a **tool** and decides when and what to search:

```
user: "gift for a 10yo who loves science, ~$40"
          │
          ▼
model → (tool call) SearchProducts("science gifts for 10 year old", count: 5)
          │                       ↑  Microsoft.Extensions.AI's .UseFunctionInvocation()
          │                          runs this automatically and feeds the JSON back
          ▼
model ← [ {Kids Science Kit, $34.99}, {Robot Kit, $49.00}, ... ]
          │
          ▼
model → "The Kids Science Kit ($34.99) is a great fit. The Robot Kit ($49) is slightly over budget."
```

Advantages: the model can search multiple times in one turn, rephrase the query
("winter hiking warm clothing and gear" — not the user's words), and skip the search entirely for
"thanks!". All of this happens **inside a single `GetStreamingResponseAsync` call**.

---

## 3. Implementation

### 3.1 Chat pipeline — `AiDependencyGroup`
```csharp
services.AddChatClient(azureOpenAi.GetChatClient(chatDeployment).AsIChatClient())
        .UseFunctionInvocation()   // <- executes ChatOptions.Tools automatically
        .UseLogging();
```
`UseFunctionInvocation()` is global on `IChatClient`; it's a no-op for callers that pass no tools
(Phase 4's copy generator).

### 3.2 `AssistantService`
```csharp
public IAsyncEnumerable<string> StreamReplyAsync(IReadOnlyList<AssistantMessage> messages, CancellationToken ct)
{
    // validate EAGERLY - before the controller sets 200 + text/event-stream
    if (_chat is null) throw new AiUnavailableException(...);
    if (messages.Count == 0) throw new ArgumentException(...);
    if (!messages[^1].Role.Equals("user", OrdinalIgnoreCase)) throw new ArgumentException(...);
    return StreamCore(messages, ct);   // the lazy iterator
}

private async IAsyncEnumerable<string> StreamCore(...)
{
    [Description("Search this store's catalog by natural-language need. Call this for ANY question about products...")]
    async Task<IReadOnlyList<object>> SearchProducts(
        [Description("What the shopper wants, in natural language")] string query,
        [Description("How many results to return, 1-8")] int count = 5)
    {
        var r = await _search.SearchAsync(query, Math.Clamp(count, 1, 8), ct);
        return r.Results.Select(x => (object)new { x.Id, x.Name, x.Slug, price = x.Price, x.Score }).ToList();
    }

    var chat = [ new ChatMessage(ChatRole.System, SystemPrompt), ...mapped history (last 12)... ];
    var options = new ChatOptions {
        Tools = [ AIFunctionFactory.Create(SearchProducts) ],
        ToolMode = ChatToolMode.Auto,
        MaxOutputTokens = 500,
        Temperature = 0.4f
    };

    await foreach (var update in _chat!.GetStreamingResponseAsync(chat, options, ct))
        if (!string.IsNullOrEmpty(update.Text)) yield return update.Text;
}
```

The tool is a **local function** so it captures `ct` and `_search`. `AIFunctionFactory.Create`
reads the `[Description]` attributes to build the schema the model sees.

### 3.3 System prompt (the guardrail)
> You are the shopping assistant for an online store. Your only job is to help shoppers find
> products in THIS store's catalog.
> - For anything about products… call SearchProducts first and recommend ONLY items it returns.
> - Never invent products, prices, specifications, stock, shipping or returns.…
> - Keep replies short…
> - If asked about anything other than shopping in this store, briefly decline and steer back.

### 3.4 Controller — SSE
```csharp
[AllowAnonymous, HttpPost("assistant/chat")]
public async Task ChatAssistant([FromBody] AssistantChatRequest request, CancellationToken ct)
{
    Response.Headers.CacheControl = "no-cache";
    Response.Headers["X-Accel-Buffering"] = "no";   // stop proxies/Kestrel buffering the stream
    Response.ContentType = "text/event-stream";
    try
    {
        await foreach (var delta in _assistant.StreamReplyAsync(request.Messages, ct))
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(delta)}\n\n", ct);
            await Response.Body.FlushAsync(ct);       // push each chunk immediately
        }
    }
    catch (OperationCanceledException) { }           // client hung up
    catch (Exception ex) { /* -> event: error frame */ }
    await Response.WriteAsync("data: [DONE]\n\n", ct);
}
```

`[ApiController]` + `[MinLength(1)]` on `Messages` means an empty `messages` array is rejected with
a normal 400 ProblemDetails *before* the action runs — it never reaches the stream.

---

## 4. Verified end to end (local, real Azure OpenAI + `ecommercedb_dev`)

| Check | Result |
|---|---|
| `dotnet build` + `dotnet test` | ✅ 0 errors, 8/8 |
| "gift for a 10yo who loves science, ~$40" | ✅ tool call `SearchProducts('science gifts for 10 year old')`; recommended **Kids Science Kit $34.99**, flagged Robot Kit "$49, over budget" |
| multi-turn follow-up ("anything for a winter hike?") with history | ✅ new tool call, used the Down Jacket / Water Bottle |
| streaming | ✅ arrives token-by-token as `data:` frames, ends `data: [DONE]` |
| off-topic ("write me a quicksort") | ✅ politely declined, steered back to shopping |
| "do you sell live tigers?" | ✅ "We do not sell live animals" — no hallucination |
| last message role ≠ user | ✅ `event: error` frame |
| empty `messages` | ✅ HTTP 400 (model validation) |

Local test products + user removed from `ecommercedb_dev` afterwards.

---

## 5. Minimal browser client (for a Project.Web widget later)

```html
<script>
async function ask(messages) {
  const res = await fetch('/api/ai/assistant/chat', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ messages })
  });
  const reader = res.body.getReader(), dec = new TextDecoder();
  let buf = '', reply = '';
  while (true) {
    const { value, done } = await reader.read(); if (done) break;
    buf += dec.decode(value, { stream: true });
    const frames = buf.split('\n\n'); buf = frames.pop();
    for (const f of frames) {
      const line = f.split('\n').find(l => l.startsWith('data: '));
      if (!line) continue;
      const p = line.slice(6);
      if (p === '[DONE]') return reply;
      reply += JSON.parse(p);            // append delta, render as it grows
    }
  }
  return reply;
}
</script>
```

Wiring this into `Project.Web` as a floating chat panel is a small follow-up — the API is the
substance of this phase.

---

## 6. Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| Bad requests returned 200 with an error mixed into the stream | `IAsyncEnumerable` iterators are lazy — validation inside `StreamCore` didn't run until the first `MoveNext`, after the controller had already sent `200 text/event-stream` | split a non-iterator `StreamReplyAsync` that validates and *then* returns the private iterator |
| Response arrived all at once, not streamed | Kestrel / proxy buffering | `X-Accel-Buffering: no` + `Response.Body.FlushAsync()` after every chunk |
| `.UseFunctionInvocation()` affects Phase 4 too | it's registered on the shared `IChatClient` | harmless — no tools passed there, so nothing runs |

---

## 7. Cost

One assistant turn ≈ a few hundred prompt + completion tokens on `gpt-4.1-mini` + one small
embedding for the tool call ≈ **well under $0.001**. `MaxOutputTokens = 500` and the 12-message
history cap bound the worst case.

---

## 8. Not done (deliberately)

- A chat widget in `Project.Web` (snippet above; small follow-up).
- **Rate limiting** on this anonymous, token-spending endpoint — Phase 7.
- Persisting conversations for analytics.

This completes the AI feature set: infra (1–2), wiring (3), admin copy (4), semantic search (5),
and the assistant (6). Phase 7 is operational hardening.
