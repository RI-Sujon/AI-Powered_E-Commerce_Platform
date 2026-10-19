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
| `AksRabiuru` environment | ✅ created manually in Azure DevOps (Pipelines → Environments) — the YAML references it but Azure DevOps doesn't auto-create environments from YAML, so this is a one-time manual step (see §4) |
| Service connection permissions | ✅ fixed — the service connection's principal (`Azure-AppServiceSujon-Connection`) only had `Contributor` scoped to `ecommerce-rg`/`ecommerce-tfstate-rg` (the resource groups `dev`/`staging`/`prod` merely *reference*). `aks-learning` is different: its Terraform *creates its own new resource group*, which needs subscription-scoped `Contributor` (you can't scope a role assignment to a resource group that doesn't exist yet). Granted via `az role assignment create --assignee <sp-object-id> --role Contributor --scope /subscriptions/<id>` — see §2b. |
| First real pipeline run | ✅ green end-to-end, 2026-10-13 — `Preflight` → `Build` → `Deploy_AKS` (terraform apply, `kubectl apply -k`, rollouts, EF migration, ingress-nginx install, smoke test) all passed after fixing §2c/§2d/§2e below |
| Agent prerequisites (`kubectl`, `helm`) | ✅ confirmed present on the `GhorarDim` agent — `Preflight` stage passed |
| Manual UI verification | ✅ `kubectl port-forward -n ingress-nginx svc/ingress-nginx-controller 18080:80` → `http://localhost:18080/` rendered the live Products page, served by this run's actual build tag (not `latest`) |

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

## 2b. The service-principal permissions gotcha (hit on first real pipeline run)

First `terraform apply` from the pipeline failed with a `403 AuthorizationFailed` reading
`rg-ecommerce-aks-learning` — not a bug in the Terraform, a genuinely missing permission.

**Why:** every environment's Terraform up to now (`dev`/`staging`/`prod`) only *references*
resource groups that already exist (`ecommerce-rg`, via a `data` block) — they never create one.
`aks-learning`'s `main.tf` is different: it has `resource "azurerm_resource_group" "aks"`, meaning
Terraform itself creates that resource group. The service connection's service principal had
`Contributor` scoped only to the two specific resource groups the other environments touch
(`ecommerce-rg`, `ecommerce-tfstate-rg`) — it had never been granted anything on
`rg-ecommerce-aks-learning`, which had only ever been created under a developer's own `az login`
by hand. Worse: even scoping a fix to that one resource group wouldn't be durable, because **you
can't scope a role assignment to a resource group that doesn't exist yet** — if the pipeline ever
destroys and recreates it from scratch, a group-scoped grant would need to be redone every time.

**Fix:** granted the service principal `Contributor` at **subscription** scope instead —
```powershell
az role assignment create --assignee <service-principal-object-id> --role Contributor --scope /subscriptions/<subscription-id>
```
This matches how a human's own `az login` already works against this subscription, and survives
any future destroy/recreate cycle of the resource group itself. Found the right object ID by
checking which principal already had working `Contributor` grants on `ecommerce-rg` (`az role
assignment list --resource-group ecommerce-rg -o table`) — Azure CLI accepts either the
Application object ID or the Service Principal object ID interchangeably for `--assignee`, so the
ID quoted in the pipeline's error message and the one shown by `az role assignment list` can look
different while referring to the same identity.

**Takeaway:** when a new Terraform environment creates infrastructure at a *higher* scope than
every previous environment did (a whole resource group, not just resources inside one), check
the CI identity's permissions at that scope *before* the first real run, not after a 403.

---

## 2c. The quoted-tag gotcha (hit right after the permissions fix)

Next failure, from `kubectl apply -k`:
```
error: invalid Kustomization: json: cannot unmarshal number into Go struct field Image.images.newTag of type string
```
`$(Build.BuildId)` is a numeric-looking string (e.g. `8`). The original PowerShell substitution
(`"newTag: $(tag)"`) wrote that straight into the YAML unquoted — `newTag: 8` — which YAML parses
as an **integer**, but Kustomize's schema requires `newTag` to be a **string**, and silently
accepting `latest` (already a string) never exposed this until the first real numeric build ID
was substituted in.

**Fix:** quote it explicitly in the replacement — `"newTag: ""$(tag)"""` in PowerShell produces
`newTag: "8"` in the file (the doubled `""` is PowerShell's escape for a literal `"` inside a
double-quoted string). Applied in `azure-pipelines-aks.yml`.

**Takeaway:** anything that looks like a number (build IDs, version tags, ports) needs explicit
quoting when template-substituted into YAML — YAML's type inference will happily turn an unquoted
numeric-looking string into an integer, which breaks any schema (like Kustomize's) that expects a
string there.

---

## 2d. The OIDC-issuer drift that became a hard failure

The "harmless provider-schema drift on `oidc_issuer_enabled`/`oidc_issuer_url`" flagged back in §2
stopped being harmless the moment a real `terraform apply` tried to act on it:
```
Error: updating Kubernetes Cluster ...
"code": "OIDCIssuerFeatureCannotBeDisabled",
"message": "OIDC issuer feature cannot be disabled."
```
**Why:** `main.tf` never set `oidc_issuer_enabled`, so the provider defaulted the field to `false`
in its plan. But the real cluster already has the OIDC issuer on (current AKS API versions enable
it by default regardless of what's requested at create time) — and once on, Azure refuses to turn
it back off. Every `apply` was silently trying (and, this time, failing) to disable a feature that
can never be disabled.

A second, more benign instance of the same root cause showed up right after: the node pool's
`upgrade_settings` (`max_surge = "10%"`, etc.) are stamped on by Azure too, unset in our config,
causing a no-op "update in-place" every single plan.

**Fix:** declared both explicitly in `main.tf` so the config matches what Azure actually has —
`oidc_issuer_enabled = true` on the cluster, and a `upgrade_settings` block (`max_surge = "10%"`,
`drain_timeout_in_minutes = 0`, `node_soak_duration_in_minutes = 0`) on `default_node_pool`.
Verified with `terraform plan` → `No changes. Your infrastructure matches the configuration.`

**Takeaway:** when a cloud provider's API silently applies its own defaults to fields your config
never set, "no changes needed" drift isn't always safe to ignore — some of those fields can only
be set one way (enabled → can't go back to disabled), so letting Terraform "re-apply its default"
can turn into a hard failure the moment the live resource no longer matches that default. Pin
anything the provider auto-populates as soon as `terraform plan` shows it, rather than waiting for
an `apply` to fail on it.

---

## 2e. `$using:` scope gotcha in the EF migration task

Next failure, before any Postgres connection was even attempted:
```
Start-Job : The value of the using variable '$using:aksNamespace' cannot be retrieved because it
has not been set in the local session.
```
**Why:** `$(aksNamespace)` everywhere else in this pipeline (e.g. the `kubectl rollout status -n
$(aksNamespace)` lines) is an **Azure Pipelines macro** — literal text substitution the pipeline
engine performs on the whole script before handing it to PowerShell at all. It was never a real
PowerShell variable, so there was nothing for `$using:aksNamespace` to capture — `$using:` only
works when the named variable already exists in the calling scope, which it never did here.

**Fix:** write the macro directly inside the `Start-Job` script block too — `$(aksNamespace)` gets
replaced with the literal namespace text before PowerShell runs, so the job's script block never
needs `$using:` at all:
```powershell
$job = Start-Job -ScriptBlock {
  kubectl port-forward -n $(aksNamespace) svc/postgres 15434:5432
}
```

**Takeaway:** Azure Pipelines `$(variable)` macros and PowerShell `$using:variable` solve two
different problems — macros are substituted into script *text* before execution; `$using:` reaches
into an *already-running PowerShell session's* variable scope from inside a job/runspace. Don't mix
them: a macro reference never creates a real local variable, so `$using:` on it will always fail.

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
