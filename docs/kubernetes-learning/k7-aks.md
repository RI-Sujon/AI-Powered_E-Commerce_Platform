# K7 — AKS via Terraform (completed end-to-end, 2026-10-12)

> **Goal:** the one phase that isn't local. Stand up a real Azure Kubernetes Service cluster with
> Terraform, deploy this project's actual stack to it, verify the same things K1–K6 already
> proved locally now hold on real infrastructure, then tear it down — all inside a single
> 2–3 hour session, for well under $1 in actual Azure spend.

This document is the plan, and as of 2026-10-12 it has been **run end-to-end successfully** on a
**new free-trial Azure account** (different from the original one referenced in §0.a): cluster
provisioned, full app stack deployed, K2/K3/K6 parity verified on real infra, and the cluster
destroyed again in the same sitting. See §0.b for the full account history and §4 for the actual
results captured during the run.

---

## 0. Status

### 0.a — Original account (2026-10-06): blocked

The Azure free trial credit expired and the subscription became **disabled / read-only**
(`ReadOnlyDisabledSubscription`): Azure refuses every create, so `terraform apply` was impossible.
Upgrading to pay-as-you-go was skipped on purpose (it risked billing ~$8.32 of earlier usage
against a ~$2 budget). So K7 was completed up to — but not including — the apply:

| Step | Result |
|---|---|
| Terraform written | `terraform/environments/aks-learning/` — `main.tf`, `variables.tf`, `outputs.tf`, `terraform.tfvars` |
| `terraform validate` | ✅ valid |
| `terraform plan` | ✅ **2 to add, 0 to change, 0 to destroy** (resource group + 1-node AKS, Free tier, `Standard_B2s`, kubenet). The plan only reads Azure, so it works on a disabled subscription. |
| Kustomize render | ✅ `kubectl kustomize k8s/overlays/dev` renders all 15 objects the AKS deploy step would apply |
| `terraform apply` + live checks | ⏸ not run — subscription was read-only |

### 0.b — New account (2026-10-12): resuming here

K7 is now being resumed on a **different Azure account's free trial**, not the original one.
Everything in §0.a is historical context for the old account only — treat the old subscription as
abandoned, not something to go back and fix. Status on the new account:

| Step | Result |
|---|---|
| Subscription enabled / writable | ✅ confirmed — `terraform apply` actually reached Azure and started creating the AKS cluster (not just a plan) |
| Regional vCPU quota (`az vm list-usage -l eastus`) | ✅ re-queried on the new account — **identical to §1**: `Total Regional vCPUs: 0 in use / 4 limit` |
| `Standard_B2s` | ❌ **rejected by Azure at apply time**, not just a quota guess: `The VM size of Standard_B2s is not allowed in your subscription in location 'eastus'` (400 Bad Request, 2026-10-12). The returned allow-list contains **zero B-series sizes at all** — this subscription/offer type blocks the whole burstable family outright, independent of the vCPU quota number (which still showed 4 available for `Standard_BS Family`). This is a known anti-abuse restriction on some free-trial/sponsored offer types, not something `terraform plan` or `az vm list-usage` can detect in advance. |
| Fix | Switched `node_vm_size` to **`Standard_D2as_v4`** (2 vCPU, 8GB, AMD) — present in Azure's allow-list for this subscription, same $0.096/hr Linux PAYG rate as its Intel twin `Standard_D2s_v4`. Re-ran `terraform plan` (2 to add, unchanged) then `terraform apply`. |
| `terraform apply` | ✅ **succeeded** — `Apply complete! Resources: 2 added, 0 changed, 0 destroyed` (~5m54s for the AKS cluster, ~30s for the ACR `AcrPull` role assignment). Outputs: `cluster_name = "aks-ecommerce-learning"`, `resource_group_name = "rg-ecommerce-aks-learning"`. |
| Live checks (§4) | ✅ **all passed** — see §4 for the full results table. |
| `terraform destroy` | ✅ **ran and confirmed** — `Destroy complete! Resources: 3 destroyed.` `az aks list -o table` returned empty and `rg-ecommerce-aks-learning` no longer appears in `az group list -o table`. Nothing billable left behind. |

**Deviation from §3:** no `backend.tf`. The cluster is meant to live for one sitting, so it uses
Terraform's default **local state** instead of a remote state account. `*.tfstate` is already
gitignored. Since this is a different account/tenant, double check `az account show` reports the
*new* subscription (not a cached old one) before running `terraform init`/`apply` — a stale
`az login` session pointed at the old, still-disabled subscription is the most likely way to waste
time here.

### Resume checklist — read this when you come back to finish K7

**A. Optional, $0, do any time — local parity checks (§4 step 4 on your own machine)**
1. Start Docker Desktop and enable Kubernetes (Settings → Kubernetes). Confirm: `kubectl get nodes`
   shows `Ready`. (It was off when K7 was written; the `docker-desktop` context pointed at a dead port.)
2. Install metrics-server as in K6, deploy `kubectl apply -k k8s/overlays/dev`.
3. Re-run the cheap checks: probes (K3), delete the Postgres Pod and confirm data survives (K2),
   CPU-burn the API Pod and watch the HPA scale (K6).

**B. Real AKS — only when you have an enabled subscription and budget**

> Notes below reflect the **new account** (2026-10-12) picked up in §0.b. Everything about the
> *old* account's billing/leftovers (expired trial, ~$8.32 prior usage, `rg-ecommerce-dev`
> App Service leftovers) was specific to that account and does not carry over — a fresh trial on a
> different account starts clean. Re-verify the points below on this account rather than assuming
> the old findings still hold; don't skip step 1 just because the old account's quota numbers
> happened to match (§0.b).

1. Check status with a harmless write first — `az account show` is NOT reliable on its own (on
   the old account it said `Enabled` while the subscription was actually read-only):
   `az group create -n rg-test -l eastus`, then delete it. If it fails with
   `ReadOnlyDisabledSubscription` or similar, the subscription is still disabled — stop here.
   Also run `az account show -o table` first and confirm it's showing the **new** account's
   subscription ID/tenant, not a cached login from the old one.
2. This is a fresh free-trial account, so no upgrade-to-pay-as-you-go should be necessary to get
   write access — if `az account show` or the step-1 write test says otherwise (e.g. the trial
   itself is already exhausted), treat that as a new blocker and stop rather than assuming it's
   safe to upgrade; re-check the budget math in §2 against this account's own remaining credit
   first.
3. Quick sanity check that this account has nothing left over yet:
   `az resource list -o table` (should be empty or near-empty on a brand-new account — if it's
   not, figure out why before proceeding).
4. Quota already confirmed in §0.b (`Total Regional vCPUs: 0/4`, same as §1) — no need to
   re-check unless time has passed since 2026-10-12.
5. Run it, all in one sitting:
```
cd terraform/environments/aks-learning
terraform init
terraform plan            # expect: 2 to add
terraform apply
$(terraform output -raw get_credentials_command)   # or paste the printed az command
kubectl get nodes ; kubectl top nodes              # metrics-server is pre-installed on AKS
kubectl apply -k k8s/overlays/aks                  # then §4 steps 3–5
terraform destroy
az aks list -o table      # must be empty
az group list -o table    # rg-ecommerce-aks-learning must be gone
```
6. Never leave the cluster up between sessions (~$1.00/day for the node). If unsure, destroy.

---

## 1. The constraint everything else is designed around

Checked directly against this subscription, not estimated:

```
Total Regional vCPUs (eastus): 4 (currently 0 in use)
```

That's a hard ceiling, region-wide, across every VM family — not a per-resource-type limit. One
AKS node at a 2-vCPU size already uses half of everything this subscription is allowed. There is
no realistic path to multi-node or a separate system/user node pool split at meaningful size; the
plan below is built around exactly **one node**, the same shape as the local `kind` cluster this
whole project already learned on.

AKS also won't schedule onto the smallest VM sizes at all — `Standard_B1s` (1 vCPU) is rejected
outright for node pools. `Standard_B2s` (2 vCPU, 4GB RAM, burstable/cheap tier) would have been the
practical floor on the original account, but **this account's subscription offer type blocks the
entire B-series family** regardless of quota (confirmed by a failed `terraform apply`, see §0.b) —
so the actual floor here is **`Standard_D2as_v4`** (2 vCPU, 8GB RAM, general-purpose AMD), the
cheapest size Azure's allow-list accepts for this subscription. Still just one node, still leaves
2 vCPU of quota spare, comfortably under the 4-vCPU ceiling.

---

## 2. Cost budget — real numbers, not estimates

Queried directly from Azure's public retail pricing API for `eastus`. `Standard_B2s` is kept below
for the record (original account); it is **not usable on the new account** — see §0.b/§1.

| Item | Rate | 3-hour cost |
|---|---|---|
| ~~`Standard_B2s` node~~ (Linux, pay-as-you-go) | ~~$0.0416/hour~~ | blocked on this account, not usable |
| **`Standard_D2as_v4` node (Linux, pay-as-you-go)** | **$0.096/hour** | **~$0.29** |
| Managed OS disk (30GB Standard SSD, hourly-prorated) | ~$2.40/month | ~$0.01–0.02 |
| AKS control plane | **Free tier** | $0.00 |
| Load Balancer / Public IP | *not created this session* | $0.00 |
| Azure Container Registry | *shared registry already exists and is paid for by dev/staging/prod* | $0.00 marginal |

**Total for a 3-hour session: roughly $0.30.** Even if the session ran unattended for a full
8 hours instead of the planned 2–3, that's still ~$0.80 — still under the $1 ceiling, with less
margin than the original `B2s` plan had. The two things that would actually blow the budget — a
Load Balancer left running for days, or forgetting to `terraform destroy` — are both addressed
directly in the plan below (§5, §6).

---

## 3. What gets built, and what's deliberately skipped

**New, alongside the existing Terraform — nothing existing gets touched:**
```
terraform/
└── environments/
    └── aks-learning/          # new - separate from dev/staging/prod's Container Apps
        ├── main.tf            # azurerm_kubernetes_cluster + a resource group for it
        ├── variables.tf
        ├── outputs.tf         # kube_config, cluster name - what `terraform output` needs
        ├── backend.tf
        └── terraform.tfvars
```

`terraform/environments/{dev,staging,prod}` and every existing module stay exactly as they are —
this is a new, independent environment, not a migration off Container Apps.

**Skipped, on purpose, for this first pass:**
| Skipped | Why |
|---|---|
| Load Balancer / Ingress | Reach the cluster with `az aks command invoke` or `kubectl port-forward` instead — zero LB cost, and K4 already proved the Ingress concept locally. Worth adding *later* as its own short session, not this one. |
| A second node / node pool | Quota doesn't allow it meaningfully; one node mirrors the local cluster's own shape. |
| Uptime SLA tier | Free control plane tier has no cost and no SLA — irrelevant for a 3-hour learning session. |

**One pleasant simplification vs. K6:** AKS ships with **metrics-server pre-installed** as a
core, always-on component. The `--kubelet-insecure-tls` patch K6 needed locally (because `kind`'s
kubelet uses a self-signed cert) simply isn't a problem here — one less step.

---

## 4. The session plan — timeboxed to 2–3 hours

| Phase | Time | What happens |
|---|---|---|
| **1. Provision** | ~25 min | `terraform apply` — resource group + AKS cluster (1× `Standard_B2s`, Free tier, single system node pool). Mostly waiting on Azure, not active work. |
| **2. Connect + sanity check** | ~10 min | `az aks get-credentials`, confirm `kubectl get nodes` shows the one node `Ready`, confirm metrics-server is already serving (`kubectl top nodes`) with no extra steps. |
| **3. Deploy the stack** | ~30 min | `kubectl apply -k k8s/overlays/aks` (the same base manifests K1–K6 verified locally, with the two app images rewritten to Docker Hub — see the warning below) into a fresh namespace. Watch Postgres, Redis, API, Web all reach `Running`. |
| **4. Verify parity with what's already proven locally** | ~45–60 min | Re-run the cheapest, highest-signal checks from each earlier phase — not everything, just enough to confirm the same mechanisms hold on real infra: <br>• probes: same startup/readiness/liveness behavior (K3) <br>• PVC: delete the Postgres Pod, confirm data survives (K2) <br>• HPA: the same `kubectl exec` CPU-burn trick, confirm real autoscaling (K6) <br>• reachability via `az aks command invoke` instead of a public Ingress |
| **5. Capture the results** | ~15 min | Write down what happened — success or a real, honest blocker (quota, a rejected VM size, anything) — while it's fresh, before tearing down. |
| **6. Destroy** | ~10 min | `terraform destroy`, then confirm in the Azure Portal (or `az aks list`) that nothing billable is left running. This step is not optional and does not wait for a "good stopping point" — it's the last thing done in the same sitting, every time. |

> **Warning — images (found while preparing this session, not yet run on AKS):** `k8s/base` uses
> locally built images (`ecommerce-api:k3-local`, `ecommerce-web:k2-local`). They exist only in
> Docker Desktop's cache, so applying `base` directly on AKS gives `ImagePullBackOff`. That is why
> step 3 uses `k8s/overlays/aks`, which rewrites them to the shared Azure Container Registry
> (`ecommerceacrrabiuru.azurecr.io/ecommerceapp-{api,web}:latest`) with Kustomize's `images:`
> block. The Terraform in `aks-learning/main.tf` grants the cluster's kubelet identity `AcrPull`
> on that registry, so no `imagePullSecrets` are needed — equivalent to `az aks update
> --attach-acr`. Also confirm the `latest` images still match what the base manifests expect
> (config keys, `/health`). If a pod is `ImagePullBackOff`: `kubectl describe pod <name>` → Events
> shows the exact reason (often `401 Unauthorized` if the role assignment hasn't propagated yet —
> wait a few minutes and retry).

Total active time: comfortably inside 2–3 hours, including Azure's own provisioning/deprovisioning
wait time, which is the slowest part and requires no attention while it runs.

### Actual results (2026-10-12 run)

| Check | Result |
|---|---|
| Node ready | ✅ `aks-system-41150430-vmss000000`, v1.35.8, Ubuntu 24.04.5 LTS, containerd 2.3.3-2 |
| metrics-server | ✅ pre-installed and working immediately — `kubectl top nodes` worked with zero extra setup, unlike local `kind`'s `--kubelet-insecure-tls` patch |
| `kubectl apply -k k8s/overlays/aks` | ✅ 14 objects created in new `ecommerce-aks` namespace, 0 errors |
| ACR image pulls | ✅ instant — no `ImagePullBackOff`, no propagation delay this time |
| Pods | ✅ all 4 (`ecommerce-api`, `ecommerce-web`, `postgres`, `redis`) reached `1/1 Running` |
| PVC | ✅ `postgres-data` `Bound` to a real Azure Managed Disk (1Gi RWO) |
| API reachability | Initial `kubectl port-forward` to `svc/ecommerce-api` → `500` (fresh, un-migrated Postgres — same bug pattern as the `ecommerce-prod` overlay earlier this session). Fixed with `dotnet ef database update` via a second port-forward to `svc/postgres`. Re-tested → **`200`**. |
| **K2 parity (PVC persistence)** | ✅ inserted a marker row, deleted the Postgres pod, confirmed the replacement pod still had the row — real Azure Disk survives pod rescheduling exactly like the local PVC did |
| **K6 parity (HPA autoscaling)** | ✅ CPU-burn (`while :; do :; done`) inside the api pod drove real metrics-server-reported CPU to `372%/50%`, and the HPA scaled `ecommerce-api` from 1 → 3 replicas (its max) automatically |
| Ingress | No `ADDRESS` (expected — no ingress controller installed, per §3's "Skipped" table; reachability verified via `kubectl port-forward` instead) |
| `terraform destroy` | ✅ `Destroy complete! Resources: 3 destroyed.` Confirmed via `az aks list -o table` (empty) and `az group list -o table` (`rg-ecommerce-aks-learning` gone). |

Every mechanism K1–K6 proved locally (probes, PVC persistence, HPA autoscaling, Kustomize overlay
correctness, ACR-based image pulls) held exactly the same way on real AKS, with the one pleasant
surprise being metrics-server requiring zero setup.

---

## 5. Safety rails against accidental cost

- **No step in this plan creates anything billable that outlives the session on its own.** No
  Load Balancer, no reserved capacity, no auto-scaling node pool that could grow unattended.
- **Destroy is step 6, not "whenever."** The session isn't considered finished until
  `terraform destroy` has actually run and been confirmed — verification results get written down
  *before* destroying (§4, step 5), specifically so there's never a reason to keep the cluster up
  "just in case" while writing up what happened.
- If the session needs to pause mid-way for any reason, the safe default is to destroy anyway and
  re-`apply` next time (25 minutes and a few cents) rather than leave a cluster running between
  sessions.

---

## 6. What "done" looks like for K7

Not a permanent AKS cluster — a **repeatable, cheap, timeboxed exercise** that can be re-run
whenever there's something new worth verifying against real Azure infrastructure, each time
costing cents and leaving nothing behind. The Terraform itself is the lasting artifact; the
cluster it describes is meant to exist only for the length of one sitting.

**This ran to completion on 2026-10-12**: cluster provisioned (~6 min), stack deployed (14
objects, 4 pods healthy), K2 and K6 parity verified against real Azure Disk and metrics-server,
and the cluster fully destroyed and confirmed gone in the same sitting — total Azure spend well
under the $1 ceiling (node ran well under an hour). K7 is complete; re-running it later (e.g. to
try the skipped Ingress/Load Balancer step) just means repeating this same loop.

---

## 7. Follow-up (2026-10-13): viewing the UI needed Ingress after all

After the 2026-10-12 run, the next session's goal was simple: open the deployed app in a real
browser and click around. That turned out to need the one thing §3 deliberately skipped
(Ingress) — here's why, and what "Helm" and "Ingress" actually are for anyone who hasn't used them
before.

### What is an Ingress, again?

A Kubernetes **Service** (`ClusterIP`, the type used everywhere in this project) only has an
address that's reachable *from inside the cluster*. `ecommerce-web` and `ecommerce-api` each have
one, but a browser running on your laptop is *outside* the cluster — it can't resolve
`ecommerce-api` as a hostname, and `kubectl port-forward` only forwards to one Service at a time.

An **Ingress** is a set of HTTP routing rules ("path `/api` → the api Service, everything else →
the web Service") that sit in front of multiple Services behind one shared address. This project's
[`k8s/base/ingress.yaml`](../../k8s/base/ingress.yaml) already defines those exact rules. The
catch: an Ingress *resource* is just a routing rule — on its own it does nothing. Something has to
actually read that rule and behave as the router. That something is an **Ingress controller**.

### What is Helm, and why did we need it?

**Helm** is Kubernetes' package manager — the equivalent of `apt`/`npm`/`NuGet`, but for groups of
Kubernetes manifests ("charts") instead of OS packages or library code. Instead of hand-writing
the dozen-plus Deployment/Service/ConfigMap/RBAC objects an Ingress controller needs, `helm
install` downloads a pre-built, parameterized chart and applies all of it as one versioned,
upgradeable, uninstallable unit.

The local `docker-desktop`/`kind` cluster already had an Ingress controller running from earlier
K4 work. The real AKS cluster, being brand new, **does not** — §3's "Skipped" table deliberately
left it out to avoid the cost of the Azure Load Balancer + Public IP that an Ingress controller's
Service *normally* provisions when exposed externally (`type: LoadBalancer`).

### Why it was still free to add

An Ingress controller's own Service type is configurable. Installing it with
`--set controller.service.type=ClusterIP` instead of the chart's default `LoadBalancer` means:
- No Azure Load Balancer, no Public IP — nothing billable gets created.
- It's reachable the same way every other in-cluster Service is: `kubectl port-forward`.
- `kubectl get svc -n ingress-nginx` confirming `EXTERNAL-IP: <none>` is the proof there's no
  cloud resource behind it.

So this is a genuine $0 addition, not a quiet violation of the original cost budget.

### What was actually run

```powershell
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
helm install ingress-nginx ingress-nginx/ingress-nginx `
  --namespace ingress-nginx --create-namespace `
  --set controller.service.type=ClusterIP

kubectl wait --namespace ingress-nginx --for=condition=ready pod `
  --selector=app.kubernetes.io/component=controller --timeout=180s

kubectl get svc -n ingress-nginx       # confirm ClusterIP, no EXTERNAL-IP

kubectl port-forward -n ingress-nginx svc/ingress-nginx-controller 18082:80
# then browse http://localhost:18082/
```

One important detail specific to this app: `ecommerce-web`'s client-side JS calls a **relative**
`/api/...` path (`ApiBaseUrl: ""` in
[`k8s/base/web-deployment.yaml`](../../k8s/base/web-deployment.yaml)), by design — it's meant to
be reached through the Ingress, not hit directly. Port-forwarding straight to `svc/ecommerce-web`
(skipping the Ingress) would serve the HTML shell fine but break every `/api/...` call, because
those requests would land on the web container itself instead of being routed to the api Service.
Port-forwarding to the **Ingress controller's** Service instead of the `web` Service is what makes
both routes (`/` and `/api`) resolve correctly, exactly matching how it already worked on the
local cluster.

Confirmed working: `GET /` → `302` (the app's own redirect, not an error), `GET
/api/product/get-product-list` → `200`, and the Products page rendered correctly in a real
browser against the live AKS cluster.

**Cleanup note:** nothing extra to do — `ingress-nginx` lives inside the AKS cluster as a normal
Helm release, so the existing `terraform destroy` step (§4, §5) deletes it along with everything
else when the resource group goes away. No separate `helm uninstall` is required.
