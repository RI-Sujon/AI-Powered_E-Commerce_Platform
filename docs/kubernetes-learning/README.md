# Kubernetes learning — roadmap

Learning Kubernetes hands-on using this project's existing API/Web/Postgres/Redis stack, on a
**local** cluster first (Docker Desktop's built-in `kind`-based Kubernetes — free, no Azure quota
risk; this subscription's AKS-relevant vCPU quota is only 4, so local is the only sane starting
point anyway). Each phase has its own doc: goal, concepts, the actual manifests, and verification.

| Phase | Doc | Status | Learns |
|---|---|---|---|
| K1 | [k1-pods-deployments-services.md](k1-pods-deployments-services.md) | ✅ done | Pods, Deployments, Services, core `kubectl` workflow |
| K2 | [k2-configmaps-secrets-storage.md](k2-configmaps-secrets-storage.md) | ✅ done | ConfigMaps, Secrets as code, PersistentVolumeClaims — full stack (API+Web+Postgres+Redis) in-cluster |
| K3 | [k3-probes.md](k3-probes.md) | ✅ done | Liveness/Readiness/Startup probes, reusing the existing `/health` endpoint |
| K4 | [k4-ingress.md](k4-ingress.md) | ✅ done | Ingress controller (ingress-nginx via Helm), one entrypoint instead of separate ports |
| K5 | [k5-kustomize.md](k5-kustomize.md) | ✅ done | Kustomize overlays for dev/staging/prod — the same pattern as the Terraform environments, in a new tool |
| K6 | [k6-autoscaling.md](k6-autoscaling.md) | ✅ done | Horizontal Pod Autoscaler + metrics-server, watched live under real CPU load |
| K7 (stretch) | k7-aks.md | ⬜ next | Azure Kubernetes Service via Terraform — only if/when pursued; quota-constrained on this subscription |

**Cluster:** Docker Desktop → Settings → Kubernetes → `kind` provisioner, 1 node. Images are built
locally (`docker build`) and used directly via `imagePullPolicy: IfNotPresent` — Docker Desktop's
Kubernetes shares its image cache, so nothing needs to be pushed to a registry for local work.

**Layout:**
```
k8s/
├── base/                  # plain manifests, K1 → K4 - the single source of truth
│   ├── kustomization.yaml # K5 - lists every file below for overlays to pull in as one unit
│   ├── namespace.yaml
│   ├── app-config.yaml           # ConfigMap - non-secret settings
│   ├── app-secrets.yaml          # Secret - local-only creds, safe to commit (see K2 doc)
│   ├── postgres-pvc.yaml
│   ├── postgres-deployment.yaml  # pgvector/pgvector:pg16, exec probe (pg_isready)
│   ├── postgres-service.yaml
│   ├── redis-deployment.yaml     # exec probe (redis-cli ping)
│   ├── redis-service.yaml
│   ├── api-deployment.yaml       # startup+readiness on /health, liveness on /health/live (K3)
│   ├── api-service.yaml
│   ├── api-hpa.yaml              # K6 - min 1, max 3, target 50% CPU (needs metrics-server)
│   ├── web-deployment.yaml       # readiness+liveness on /health; ApiBaseUrl="" (K4, relative /api calls)
│   ├── web-service.yaml
│   ├── web-hpa.yaml              # K6 - min 1, max 5, target 50% CPU
│   └── ingress.yaml              # K4 - routes /api → ecommerce-api, / → ecommerce-web
└── overlays/              # K5 - each patches base for one environment, never copies it
    ├── dev/kustomization.yaml       # namespace + ASPNETCORE_ENVIRONMENT + Ingress host only
    ├── staging/kustomization.yaml   # same shape as dev - mirrors dev.tfvars ≈ staging.tfvars
    └── prod/kustomization.yaml      # + 2x CPU/memory, HPA min/max 2-10 (mirrors prod.tfvars)
```

**metrics-server isn't in `k8s/`, same reasoning as ingress-nginx.** Installed once via the
official manifest (`kubectl apply -f .../metrics-server/.../components.yaml`) plus one local-only
patch (`--kubelet-insecure-tls`, needed because `kind`'s kubelet uses a self-signed cert) — see
the K6 doc. Without it, `kubectl top` and every HPA fail with "unable to fetch metrics."

**Third-party components aren't hand-written YAML.** The ingress-nginx *controller* (K4) is
installed via Helm (`helm install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx
--create-namespace --set controller.service.type=LoadBalancer`), not committed to `k8s/base/` —
only the `Ingress` *resource* that configures it is ours to own.

**Secrets are never committed.** Anything holding a real credential (the Postgres connection
string, the JWT key) is created imperatively with `kubectl create secret` and referenced from a
manifest by name — same principle as `terraform.tfvars` never holding `postgres_admin_password`.
