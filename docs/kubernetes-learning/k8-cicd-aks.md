# K8 — Wiring AKS into Azure DevOps CI/CD

> **Goal:** take the AKS deployment that's been done entirely by hand so far (K7, plus the
> 2026-10-13 Ingress/Helm follow-up) and make it something a pipeline can run on its own —
> build images, deploy to AKS, migrate the database, smoke-test through an Ingress — the same
> sequence of commands already proven manually, now expressed as one pipeline file.

This is a direct answer to "how does this connect to Azure DevOps?" Nothing about K1–K7 or the
existing `azure-pipelines.yml` (which deploys to **Azure Container Apps**, not AKS) changes —
this adds a second, separate pipeline that only touches AKS.

---

## 0. Status (2026-10-13)

| Step | Result |
|---|---|
| Remote state for `aks-learning` | ✅ added `terraform/environments/aks-learning/backend.tf`, migrated the already-running cluster's local state into it (`terraform init -migrate-state -force-copy`) — `terraform plan` confirmed all 5 resources still tracked correctly afterwards |
| New pipeline file | ✅ [`azure-pipelines-aks.yml`](../../azure-pipelines-aks.yml) written — `trigger: none`, must be run manually |
| First real pipeline run | ⏳ not yet triggered — see §4 for how to actually kick it off in Azure DevOps |
| Agent prerequisites (`kubectl`, `helm`) | ⏳ not yet confirmed present on the `GhorarDim` agent — the pipeline's `Preflight` stage checks this and fails fast with a clear message if they're missing |

---

## 1. Why a separate pipeline file, not a new stage in `azure-pipelines.yml`

`azure-pipelines.yml` triggers automatically on every push to `recA1` and deploys to Container
Apps, which **scale to zero** — an idle environment costs nothing between deploys. AKS is the
opposite: a running node costs money every hour regardless of traffic. Adding an AKS stage to that
same auto-triggered pipeline would mean **every push spins up a billable node pool** — exactly the
kind of accidental-cost mistake K7's safety rails (§5 of that doc) were written to prevent.

| | `azure-pipelines.yml` | `azure-pipelines-aks.yml` |
|---|---|---|
| Trigger | automatic, on push to `recA1` | `trigger: none` — manual "Run pipeline" only |
| Target | Container Apps (dev → staging → prod) | AKS (`aks-learning`, one environment) |
| Cost model | scale-to-zero, cheap to run often | real hourly node cost, must be destroyed after use |
| Destroy step | not applicable (nothing to tear down) | deliberately **not** in the pipeline — see §5 |

---

## 2. The remote-state gotcha (the actual lesson here)

K7 intentionally used Terraform's **local state** for `aks-learning` — fine for a human running
`apply` then `destroy` back-to-back in one sitting on one machine. A CI agent doesn't work that
way: Azure DevOps gives it a **fresh checkout every run** (`workspace: clean: all`), so a local
`.tfstate` file from a previous run simply isn't there next time. Terraform would see no state,
assume nothing exists, and either try to recreate already-existing resources (producing "already
exists" errors) or lose track of them entirely — making the eventual `terraform destroy` useless
against resources Terraform no longer remembers creating.

The fix applied today: added `backend.tf` pointing at the **same shared remote-state storage
account** the `shared` Terraform environment already created (`ecommercetfstateshared7`,
container `tfstate`), with its own blob key (`aks-learning.terraform.tfstate`) so it doesn't
collide with `dev`/`staging`/`prod`/`shared`'s own state files. Then, because the cluster was
*already running* under local state, `terraform init -migrate-state -force-copy` copied that
existing state into the new backend instead of starting blind — `terraform plan` immediately
afterward confirmed all 5 tracked resources (`azurerm_resource_group.aks`,
`azurerm_kubernetes_cluster.aks`, `azurerm_role_assignment.aks_acr_pull`, and the two `data`
lookups) were still correctly recognized, with no spurious "create" actions.

**Takeaway:** local state is fine for solo, same-sitting, same-machine work. The moment something
else (a pipeline agent, a teammate, a second machine) needs to run `apply`/`destroy` against the
same infrastructure, it needs a shared remote backend — this is true of every environment in this
repo, not just AKS; `dev`/`staging`/`prod` already had this from the start, `aks-learning` didn't
because it was built for manual, single-sitting use.

---

## 3. What the new pipeline actually does

| Stage | What happens |
|---|---|
| **Preflight** | Checks `kubectl`, `helm`, `terraform`, `docker`, `az` are all on the agent's PATH. Fails immediately with a named missing tool instead of failing confusingly three stages later. |
| **Build** | Identical to `azure-pipelines.yml`'s Build stage: `az acr login`, `docker build`/`push` both images tagged `$(Build.BuildId)` and `latest`. |
| **Deploy_AKS** | `terraform apply` (now safe to run repeatedly thanks to §2) → `az aks get-credentials` → rewrite `k8s/overlays/aks/kustomization.yaml`'s `newTag: latest` to this run's actual build tag (so the cluster always runs what was just built, not whatever `latest` happened to mean last time) → `kubectl apply -k k8s/overlays/aks` → `kubectl rollout status` on both Deployments → `dotnet ef database update` against the in-cluster Postgres (via a temporary background `kubectl port-forward`, same pattern used by hand on 2026-10-12/13) → `helm upgrade --install ingress-nginx` (ClusterIP only) → a smoke test that curls `/api/product/get-product-list` through a temporary port-forward to the ingress controller and fails the pipeline if it's not `200`. |

The EF-migration and smoke-test steps both use the same "`Start-Job` a background
`kubectl port-forward`, do the real work, `Stop-Job`/`Remove-Job` in a `finally`" pattern — it's
just the manual two-terminal workflow from earlier sessions collapsed into one non-interactive
script block, since a pipeline step can't open a second terminal by hand.

---

## 4. How to actually run it

Because of `trigger: none`, this pipeline never runs on its own — it has to be created once and
triggered manually each time:

1. In Azure DevOps: **Pipelines → New pipeline → (your repo) → Existing Azure Pipelines YAML
   file → `/azure-pipelines-aks.yml`**. Save it (don't run yet if you want to check the agent
   first).
2. Confirm the `GhorarDim` agent machine has `kubectl` and `helm` installed (the same way `docker`
   and `terraform` were already added for the existing pipeline) — the `Preflight` stage will
   tell you immediately if not.
3. Create the `AksRabiuru` environment (**Pipelines → Environments → New environment**) — same
   idea as the existing `DeveRabiuru`/`StagingRabiuru`/`ProdRabiuru` environments. Add an approval
   check here if you want a human to confirm before real spend happens, same as you might for
   `ProdRabiuru`.
4. **Run pipeline** — this is the moment an actual AKS node starts being billed, same as running
   `terraform apply` by hand.

---

## 5. Safety rails (same spirit as K7 §5, re-stated for CI)

- **No `Destroy_AKS` stage exists, on purpose.** Automatically tearing down real infrastructure
  (on a schedule, or in a pipeline `finally`) is easy to get subtly wrong and hard to notice when
  it fails silently. Destroying stays a deliberate, manual action every time:
  ```powershell
  cd terraform/environments/aks-learning
  terraform destroy
  az aks list -o table      # must be empty
  az group list -o table    # rg-ecommerce-aks-learning must be gone
  ```
- **`trigger: none`** is the main defense against accidental spend — this pipeline only ever runs
  when a human explicitly clicks "Run pipeline."
- **ingress-nginx stays ClusterIP-only** inside the pipeline too (`--set
  controller.service.type=ClusterIP`) — `kubectl get svc -n ingress-nginx` output is printed in
  the pipeline log specifically so an `EXTERNAL-IP` ever appearing there would be visible
  immediately, not buried.

---

## 6. Deliberately left for later (not needed for a learning pipeline)

- A real **Kubernetes service connection** (Azure DevOps has a first-class type for this) instead
  of `az aks get-credentials` inline — functionally equivalent, just a cleaner credential story.
- Moving the hardcoded in-cluster Postgres password off `k8s/base/app-secrets.yaml` and into Azure
  Key Vault (via the Secrets Store CSI driver) — acceptable for a destroy-after-session learning
  cluster, not something to carry into anything long-lived.
- Image vulnerability scanning before push (Trivy, or ACR Defender) — not wired into either
  pipeline yet.
- Observability (Container Insights / Prometheus) — nothing collects AKS logs/metrics centrally
  yet; `kubectl logs`/`kubectl top` were enough for this learning exercise.
