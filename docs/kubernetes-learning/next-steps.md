# What's next — beyond K1–K8

K1–K8 covered the core mechanics: Pods/Deployments/Services, ConfigMaps/Secrets/PVCs, probes,
Ingress, Kustomize overlays, HPA, a real AKS cluster via Terraform, and finally wiring that into a
CI/CD pipeline. That's a genuinely complete beginner-to-intermediate path. This doc is a roadmap
for what a production-grade setup would still need — not urgent, not required to say "I know
Kubernetes," but worth knowing *exists* and roughly *why*.

Nothing here needs to be done today. Treat it as a reading list / future-session backlog.

---

## 1. Things already flagged as deferred (see k8-cicd-aks.md §6)

Quick recap, not repeated in depth here:
- A first-class Azure DevOps **Kubernetes service connection** instead of `az aks get-credentials`
  inline.
- Moving the Postgres password out of `k8s/base/app-secrets.yaml` into **Azure Key Vault** (via
  the Secrets Store CSI driver).
- **Image vulnerability scanning** (Trivy / ACR Defender) before push.
- **Observability** — nothing currently collects AKS logs/metrics centrally; `kubectl
  logs`/`kubectl top` were enough for a learning exercise but not for anything real.

---

## 2. New topics worth learning next, roughly in order

### 2a. Workload Identity (natural next step — you already have the prerequisite)
`oidc_issuer_enabled = true` is now on the cluster (fixed today, see k8-cicd-aks.md §2d) — that's
the exact feature **Azure AD Workload Identity** needs. Today, if a pod needed to talk to an Azure
resource (Key Vault, Storage, ACR), the only option is a connection string/key sitting in a
Kubernetes Secret. Workload Identity lets a pod authenticate to Azure AD directly, with no secret
to leak, rotate, or commit — this project is one small step away from being able to try it.

### 2b. GitOps instead of `kubectl apply -k` from the pipeline
Right now the pipeline itself runs `kubectl apply -k`, meaning Azure DevOps has direct, standing
credentials to the cluster. **GitOps** (Flux or ArgoCD) flips this: an agent *inside* the cluster
watches the Git repo and pulls changes itself — nothing outside the cluster ever needs cluster
credentials. This is the dominant pattern in real platform teams and is a genuinely different
mental model worth a dedicated session.

### 2c. Postgres isn't production-grade yet
`postgres-deployment.yaml` is a single `Deployment` + one `PersistentVolumeClaim` — if that pod is
rescheduled to a different node, or the node dies, there's no replication and no backup strategy.
Two realistic next steps, from simplest to most "real":
- Switch the Deployment to a **StatefulSet** (stable pod identity/storage — the textbook-correct
  primitive for databases in Kubernetes, even single-replica).
- Or skip self-hosting entirely and point the cluster at **Azure Database for PostgreSQL Flexible
  Server** instead — which is what `terraform/environments/dev|staging|prod` effectively do for
  Container Apps already (worth comparing).

### 2d. Network policies
By default, every pod in a namespace can talk to every other pod. A **NetworkPolicy** resource
can restrict that (e.g., only `ecommerce-api` may reach `postgres:5432`, nothing else can). Cheap
to learn, immediately demonstrates a real security boundary — would pair naturally with the
existing `k8s/base/` layout (one more manifest, same Kustomize pattern already in place).

### 2e. Admission control / policy enforcement
**Azure Policy for AKS** (or OPA Gatekeeper) can block manifests from ever being applied if they
violate a rule — e.g., "no container may run as root," "no Service may be type LoadBalancer"
(which would have caught the exact cost mistake K7/K8's safety rails manually guard against today,
automatically, every time).

### 2f. Observability stack
Specifically: **Azure Monitor Container Insights** (fastest to wire up, Azure-native) or
**Prometheus + Grafana** (more portable, more commonly asked about in interviews). Either would
answer questions this project currently can't: historical CPU/memory trends, alerting on pod
restarts, and dashboards instead of one-off `kubectl top` snapshots.

### 2g. Progressive delivery (blue/green or canary)
Today, `kubectl rollout status` either succeeds or times out — there's no partial/gradual traffic
shift. Tools like **Argo Rollouts** or Flagger add canary/blue-green strategies on top of the same
Deployments already in `k8s/base/`, directly relevant once there's more than one AKS environment
to promote through (mirroring the dev→staging→prod pattern Terraform/Container Apps already use).

---

## 3. Suggested order, if picking this up again later

1. **Workload Identity** (2a) — smallest, most self-contained, builds directly on today's work.
2. **Network policies** (2d) — small, same `k8s/base/` files you already know.
3. **GitOps** (2b) — bigger shift, but conceptually the highest-value "how do real teams do this."
4. **Observability** (2f) — needed before any of the rest feels "real" day-to-day.
5. **Postgres StatefulSet / managed DB** (2c) and **admission control** (2e) — whenever reliability
   and governance become the focus rather than mechanics.
6. **Progressive delivery** (2g) — last, since it only matters once multiple environments exist.

None of this blocks calling K1–K8 "done" — this is deliberately a *separate* document so the
core roadmap (`README.md`) stays a clean record of what was actually completed, not a mix of done
work and someday-ideas.
