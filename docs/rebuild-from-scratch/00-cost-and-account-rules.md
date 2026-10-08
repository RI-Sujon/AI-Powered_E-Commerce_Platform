# 0 — Cost and account rules (read first)

## What happened last time (so it doesn't repeat)

- A free trial gives **a credit (usually $200) valid for 30 days**. When either the credit or the 30
  days runs out, the subscription becomes **disabled / read-only** (`ReadOnlyDisabledSubscription`):
  you can read and delete, but not create. `az account show` can still say `Enabled` — the only
  reliable test is a real write: `az group create -n rg-test -l eastus`.
- This project, left fully running, used about **$111 in one month**. Container Apps alone were
  **$8.32 in ~3 days (≈ $2.8/day)** for six apps (api+web × dev/staging/prod), because every app had
  `min_replicas = 1` and so was billed even when idle.
- Things that bill while idle (check the portal's Cost analysis → group by Resource):
  Container Apps with min replicas ≥ 1, PostgreSQL Flexible Server (B1ms), **Azure Cache for Redis
  (dev creates one, Basic C0)**, Log Analytics, storage accounts (pennies), AKS node VM + disk.

## Account rule

Use a trial only on an account you are actually eligible for. Microsoft limits free trials per
person/payment method/phone; creating extra accounts to get repeat trials can violate the terms and
get accounts blocked. Legit alternatives worth checking first: a Visual Studio / MSDN subscription
credit through your employer (often $50–150/month), Azure for Students, Microsoft Learn sandboxes.

## Budget plan for one 30-day credit

Do the expensive things **short and in order**, then delete. Suggested:

| Days | Do | Keep running afterwards? |
|---|---|---|
| 1 | doc 1 + 2: foundation, Terraform **dev only** | no — destroy at end of day if you stop |
| 2 | doc 3: ADO pipeline, deploy dev (staging/prod only if you want to practise promotion) | no |
| 3 | doc 4 part B: AKS, 3 hours, `terraform destroy` same sitting | **never** |
| any | doc 4 part A (local Kubernetes) | free, any time |
| last | doc 5 teardown, verify | everything gone |

Rules:
1. **Set a budget alert on day 1:** Cost Management → Budgets → e.g. $20/mo, alert at 50 / 80%.
   (Alerts lag by hours — they warn, they don't stop spending.)
2. **Never leave at the end of a day with more running than you need.** `az resource list -o table`
   before you close the laptop.
3. Skip staging/prod apps unless practising promotion. Each env's apps are a standing daily cost.
4. Skip Redis in dev if you don't need it (it's a separate resource in `environments/dev/main.tf`).
5. If the credit is nearly gone, **delete first, learn on local Kubernetes** (free) afterwards.

## Cost per thing (look up current prices — these numbers change)

Use <https://azure.microsoft.com/pricing/calculator/> or the retail API
(`https://prices.azure.com/api/retail/prices?$filter=...`). Known from the K7 plan:
`Standard_B2s` AKS node ≈ $0.0416/h, AKS Free-tier control plane $0, so a 3-hour AKS session ≈ $0.15.
