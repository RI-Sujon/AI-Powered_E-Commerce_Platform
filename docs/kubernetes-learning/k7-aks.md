# K7 — AKS via Terraform (written + plan-verified offline — not applied)

> **Goal:** the one phase that isn't local. Stand up a real Azure Kubernetes Service cluster with
> Terraform, deploy this project's actual stack to it, verify the same things K1–K6 already
> proved locally now hold on real infrastructure, then tear it down — all inside a single
> 2–3 hour session, for well under $1 in actual Azure spend.

This document is the plan. As of 2026-10-06 the Terraform is written and plan-verified, but
`terraform apply` has not run and no Azure resources were created — see §0 for status and the
resume checklist.

---

## 0. Status — what was actually done (2026-10-06)

The Azure free trial credit expired and the subscription became **disabled / read-only**
(`ReadOnlyDisabledSubscription`): Azure refuses every create, so `terraform apply` is impossible.
Upgrading to pay-as-you-go was skipped on purpose (it risked billing ~$8.32 of earlier usage
against a ~$2 budget). So K7 was completed up to — but not including — the apply:

| Step | Result |
|---|---|
| Terraform written | `terraform/environments/aks-learning/` — `main.tf`, `variables.tf`, `outputs.tf`, `terraform.tfvars` |
| `terraform validate` | ✅ valid |
| `terraform plan` | ✅ **2 to add, 0 to change, 0 to destroy** (resource group + 1-node AKS, Free tier, `Standard_B2s`, kubenet). The plan only reads Azure, so it works on a disabled subscription. |
| Kustomize render | ✅ `kubectl kustomize k8s/overlays/dev` renders all 15 objects the AKS deploy step would apply |
| `terraform apply` + live checks | ⏸ not run — needs a writable subscription |

**Deviation from §3:** no `backend.tf`. The cluster is meant to live for one sitting, so it uses
Terraform's default **local state** instead of a remote state account (which would also be
unreadable on a disabled subscription). `*.tfstate` is already gitignored.

### Resume checklist — read this when you come back to finish K7

**A. Optional, $0, do any time — local parity checks (§4 step 4 on your own machine)**
1. Start Docker Desktop and enable Kubernetes (Settings → Kubernetes). Confirm: `kubectl get nodes`
   shows `Ready`. (It was off when K7 was written; the `docker-desktop` context pointed at a dead port.)
2. Install metrics-server as in K6, deploy `kubectl apply -k k8s/overlays/dev`.
3. Re-run the cheap checks: probes (K3), delete the Postgres Pod and confirm data survives (K2),
   CPU-burn the API Pod and watch the HPA scale (K6).

**B. Real AKS — only when you have an enabled subscription and budget**
1. Check status with a harmless write — `az account show` is NOT reliable (it said `Enabled` while
   the subscription was actually read-only): `az group create -n rg-test -l eastus`, then delete it. `ReadOnlyDisabledSubscription` means it
   is still disabled — stop here.
2. Upgrading to pay-as-you-go is what re-enables it. **Before upgrading:** the trial's Oct usage
   (~$8.32) may be billed; check Cost management → Invoices after upgrading. Set a budget
   (e.g. $1.50, email alert) first; pay-as-you-go has no hard cap.
3. Make sure old paid leftovers are gone: `az resource list -o table` (the App Service plans in
   `rg-ecommerce-dev` and the tfstate storage accounts were still there on 2026-10-06).
4. Check quota: `az vm list-usage -l eastus -o table` — need 2 free vCPU for `Standard_B2s`.
5. Run it, all in one sitting:
```
cd terraform/environments/aks-learning
terraform init
terraform plan            # expect: 2 to add
terraform apply
$(terraform output -raw get_credentials_command)   # or paste the printed az command
kubectl get nodes ; kubectl top nodes              # metrics-server is pre-installed on AKS
kubectl apply -k k8s/overlays/dev                  # then §4 steps 3–5
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
outright for node pools. The practical floor is **`Standard_B2s`** (2 vCPU, 4GB RAM, burstable/
cheap tier) — leaves 2 vCPU of quota spare, comfortably under the ceiling.

---

## 2. Cost budget — real numbers, not estimates

Queried directly from Azure's public retail pricing API for `eastus`:

| Item | Rate | 3-hour cost |
|---|---|---|
| `Standard_B2s` node (Linux, pay-as-you-go) | $0.0416/hour | ~$0.125 |
| Managed OS disk (30GB Standard SSD, hourly-prorated) | ~$2.40/month | ~$0.01–0.02 |
| AKS control plane | **Free tier** | $0.00 |
| Load Balancer / Public IP | *not created this session* | $0.00 |
| Azure Container Registry | *not created — images already on Docker Hub* | $0.00 |

**Total for a 3-hour session: roughly $0.15.** Even if the session ran unattended for a full
8 hours instead of the planned 2–3, that's still ~$0.35 — the $1 ceiling has real margin built in,
not a number chosen to sound safe. The two things that would actually blow the budget — a Load
Balancer left running for days, or forgetting to `terraform destroy` — are both addressed directly
in the plan below (§5, §6).

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
| Azure Container Registry | Images already publish to Docker Hub (`docker.io/rabiul1012/...`) — AKS pulls public images with no registry needed. |
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
| **3. Deploy the stack** | ~30 min | `kubectl apply -k k8s/base` (the exact same manifests K1–K6 already built and verified locally) into a fresh namespace. Watch Postgres, Redis, API, Web all reach `Running`. |
| **4. Verify parity with what's already proven locally** | ~45–60 min | Re-run the cheapest, highest-signal checks from each earlier phase — not everything, just enough to confirm the same mechanisms hold on real infra: <br>• probes: same startup/readiness/liveness behavior (K3) <br>• PVC: delete the Postgres Pod, confirm data survives (K2) <br>• HPA: the same `kubectl exec` CPU-burn trick, confirm real autoscaling (K6) <br>• reachability via `az aks command invoke` instead of a public Ingress |
| **5. Capture the results** | ~15 min | Write down what happened — success or a real, honest blocker (quota, a rejected VM size, anything) — while it's fresh, before tearing down. |
| **6. Destroy** | ~10 min | `terraform destroy`, then confirm in the Azure Portal (or `az aks list`) that nothing billable is left running. This step is not optional and does not wait for a "good stopping point" — it's the last thing done in the same sitting, every time. |

Total active time: comfortably inside 2–3 hours, including Azure's own provisioning/deprovisioning
wait time, which is the slowest part and requires no attention while it runs.

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
