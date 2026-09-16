# Kubernetes learning — roadmap

Learning Kubernetes hands-on using this project's existing API/Web/Postgres/Redis stack, on a
**local** cluster first (Docker Desktop's built-in `kind`-based Kubernetes — free, no Azure quota
risk; this subscription's AKS-relevant vCPU quota is only 4, so local is the only sane starting
point anyway). Each phase has its own doc: goal, concepts, the actual manifests, and verification.

| Phase | Doc | Status | Learns |
|---|---|---|---|
| K1 | [k1-pods-deployments-services.md](k1-pods-deployments-services.md) | ✅ done | Pods, Deployments, Services, core `kubectl` workflow |
| K2 | k2-configmaps-secrets-storage.md | ⬜ next | ConfigMaps, Secrets as code, PersistentVolumeClaims — full stack (API+Web+Postgres+Redis) in-cluster |
| K3 | k3-probes.md | ⬜ | Liveness/Readiness/Startup probes, reusing the existing `/health` endpoint |
| K4 | k4-ingress.md | ⬜ | Ingress controller, one entrypoint instead of separate ports; Helm |
| K5 | k5-kustomize.md | ⬜ | Kustomize overlays for dev/staging/prod — the same pattern as the Terraform environments, in a new tool |
| K6 | k6-autoscaling.md | ⬜ | Horizontal Pod Autoscaler, watched live under load |
| K7 (stretch) | k7-aks.md | ⬜ | Azure Kubernetes Service via Terraform — only if/when pursued; quota-constrained on this subscription |

**Cluster:** Docker Desktop → Settings → Kubernetes → `kind` provisioner, 1 node. Images are built
locally (`docker build`) and used directly via `imagePullPolicy: IfNotPresent` — Docker Desktop's
Kubernetes shares its image cache, so nothing needs to be pushed to a registry for local work.

**Layout:**
```
k8s/
└── base/              # plain manifests, K1 → K4
    ├── namespace.yaml
    ├── api-deployment.yaml
    └── api-service.yaml
    # K5 adds overlays/{dev,staging,prod}/ + kustomization.yaml files
```

**Secrets are never committed.** Anything holding a real credential (the Postgres connection
string, the JWT key) is created imperatively with `kubectl create secret` and referenced from a
manifest by name — same principle as `terraform.tfvars` never holding `postgres_admin_password`.
