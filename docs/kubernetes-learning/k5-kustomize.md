# K5 — Kustomize: one set of manifests, three environments

> **Goal:** every phase so far has had exactly one environment - the `ecommerce` namespace.
> Real projects need dev/staging/prod running side by side without three hand-maintained copies
> of every YAML file drifting apart from each other. Kustomize (built into `kubectl` since 1.14,
> no separate install) does for K8s manifests what the Terraform `environments/{dev,staging,prod}`
> folders already do for the Azure infrastructure: one shared definition, small per-environment
> patches on top.

---

## 1. Overlays patch a base - they never copy it

```
k8s/
├── base/
│   ├── kustomization.yaml   # lists every plain resource file - the "give me everything" list
│   └── *.yaml                # unchanged from K1-K4 - still the single source of truth
└── overlays/
    ├── dev/kustomization.yaml
    ├── staging/kustomization.yaml
    └── prod/kustomization.yaml
```

Each overlay is `resources: [../../base]` plus a short list of **patches** - JSON6902 operations
against specific fields on specific resources. Nothing in `base/` changed to make this work; an
overlay can only ever add or override, never touch the base files themselves. `kubectl kustomize
k8s/overlays/prod` renders the final YAML without applying anything - useful for reviewing exactly
what a `kubectl apply -k` would do first, same instinct as `terraform plan` before `apply`.

### What actually differs between the three, and why that mirrors the real Terraform setup

| | dev | staging | prod |
|---|---|---|---|
| Namespace | `ecommerce-dev` | `ecommerce-staging` | `ecommerce-prod` |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging` | `Production` |
| Ingress host | `dev.ecommerce.local` | `staging.ecommerce.local` | `prod.ecommerce.local` |
| API/Web replicas | 1 (base default) | 1 (base default) | 2 |
| API/Web CPU + memory | base default | base default | 2x base default |

dev and staging overlays patch *only* the namespace, the environment name, and the Ingress host -
nothing about sizing. That's not a shortcut taken here; it's copied straight from the real
`terraform/environments/dev/terraform.tfvars` and `staging/terraform.tfvars`, which are
byte-for-byte identical except for the environment name and database name. prod's Terraform
doubles `min_replicas` (1→2) and CPU/memory (0.5/1Gi→1.0/2Gi) versus dev/staging - the prod
overlay patches the same ratio onto the base Deployments. Kustomize just makes that real
relationship visible as three small files instead of three near-duplicate manifest sets.

```yaml
# k8s/overlays/prod/kustomization.yaml (the interesting patch)
patches:
  - target: { kind: Deployment, name: ecommerce-api }
    patch: |-
      - op: replace
        path: /spec/replicas
        value: 2
      - op: replace
        path: /spec/template/spec/containers/0/resources
        value:
          requests: { cpu: "200m", memory: "384Mi" }
          limits:   { cpu: "1000m", memory: "768Mi" }
```

`replicas: 2` is a **static floor** here, not real autoscaling - Deployments don't have a min/max
range the way Container Apps does. K6 (HorizontalPodAutoscaler) is what actually replaces this
fixed number with a real min/max, matching what Terraform's `api_min_replicas`/`api_max_replicas`
already give prod in Azure.

### The Namespace object itself gets renamed, not just its contents

`namespace: ecommerce-dev` in an overlay's `kustomization.yaml` does two things at once: it sets
`metadata.namespace` on every namespaced resource *and* - because `base/namespace.yaml` is a
`kind: Namespace` resource - renames that object's own `metadata.name` to match. One line handles
both halves of "give this environment a completely separate namespace," which is why nothing else
in `base/` needed to change to support multiple environments.

---

## 2. Verified

Rendering all three overlays (`kubectl kustomize k8s/overlays/{dev,staging,prod}`) confirmed every
patch above lands correctly with no shared state bleeding between them - namespace, config value,
and Ingress host all differ exactly as intended, and dev/staging really do stay identical to each
other apart from those three fields.

`kubectl apply -k k8s/overlays/prod` was then run against the live cluster (the one already
running K1-K4's original `ecommerce` namespace, left untouched and still serving traffic the whole
time) to prove the render is real, not just correct-looking YAML:

| Check | Result |
|---|---|
| Namespace created | `ecommerce-prod`, isolated from `ecommerce` |
| Replicas | `kubectl get pods -n ecommerce-prod` → 2 API + 2 Web Pods, all `1/1 Running` |
| Resources | `kubectl get pod ... -o jsonpath='{.spec.containers[0].resources}'` → `{"limits":{"cpu":"1","memory":"768Mi"},"requests":{"cpu":"200m","memory":"384Mi"}}` - exactly the patched values, not base's |
| `ASPNETCORE_ENVIRONMENT` | `kubectl exec ... -- printenv ASPNETCORE_ENVIRONMENT` → `Production` |
| Ingress host isolation | `curl -H "Host: prod.ecommerce.local"` reaches the `ecommerce-prod` Pods; a request with **no** Host header (or the wrong one) falls through to the original `ecommerce` namespace's wildcard (`host: "*"`) Ingress instead - proving nginx's host-based routing genuinely separates the three, not just cosmetically |

### An unplanned, genuinely useful discovery: prod correctly refused to auto-migrate

The first real request against prod's fresh database came back `500`, logging `relation
"Products" does not exist`. Not a Kustomize bug - `Program.cs` gates the app's auto-migrate-on-
startup behind `if (app.Environment.IsDevelopment())`. `ecommerce-prod` genuinely runs with
`ASPNETCORE_ENVIRONMENT=Production`, so it genuinely refused to silently run schema migrations on
boot - exactly the behavior you want from a real production environment, and proof that the
environment separation this whole phase is about isn't just a label being set correctly somewhere,
it's actually changing what the running application does.

Fixed the way a real prod migration should happen - explicitly, from outside the running Pods,
not as a side effect of a Pod starting:

```bash
kubectl port-forward -n ecommerce-prod svc/postgres 15432:5432 &
ConnectionStrings__DefaultConnection="Host=localhost;Port=15432;Database=ecommercedb;Username=postgres;Password=...;SSL Mode=Disable;" \
  dotnet ef database update
```

After that, the exact same request returned `200` with a real (empty - nothing's been added to
this brand-new database yet) product list - confirming the schema, not just the connection, is
what changes between "healthy" and "actually working."

---

## 3. Next — K6: HorizontalPodAutoscaler

Replace prod's static `replicas: 2` with a real HPA - a min/max range that scales on CPU load,
watched live under generated traffic. The direct successor to this phase's "static floor" note,
and the last piece needed to make the local cluster's prod overlay behave like Container Apps'
autoscaling already does in Azure.
