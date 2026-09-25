# K6 — HorizontalPodAutoscaler: watched live under real load

> **Goal:** replace the "static floor" K5's prod overlay left behind with the real thing. A
> `replicas: 2` in a Deployment is a fixed number a human has to change by hand; a
> HorizontalPodAutoscaler watches a metric and changes it for you - the same job Container
> Apps' `min_replicas`/`max_replicas` already does in Azure, done the Kubernetes-native way.

---

## 1. What an HPA actually watches, and what it changes

A HorizontalPodAutoscaler is its own object, separate from the Deployment it controls - it
doesn't replace `spec.replicas` in the YAML, it **overwrites it live**, on a loop (every 15s by
default), based on a metric it polls from the Kubernetes Metrics API:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ecommerce-api
  namespace: ecommerce
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ecommerce-api
  minReplicas: 1
  maxReplicas: 3
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 50
```

`averageUtilization: 50` is a percentage of what the container **requested**
(`api-deployment.yaml`'s `resources.requests.cpu: 100m`), not a percentage of the node's total
CPU. That's why K3's resource requests weren't just about scheduling - the HPA has been depending
on that number existing since before it was added.

### The Metrics API isn't optional infrastructure

`kubectl top` and the HPA both read from the **Metrics API** (`metrics.k8s.io`), which nothing in
K1-K5 ever installed. Without it, an HPA object creates fine and then fails every single reconcile
with `unable to fetch metrics from resource metrics API` - it looks like a config problem but is
actually a missing component: **metrics-server**.

```bash
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
```

### The one local-cluster patch metrics-server needs

metrics-server verifies the kubelet's TLS certificate by default - and `kind`/Docker Desktop's
kubelet uses a self-signed one, which fails that check out of the box. This is a well-known,
expected gap for local clusters, not a real security compromise (it's still cluster-internal
traffic):

```bash
kubectl patch deployment metrics-server -n kube-system --type='json' \
  -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]'
```

---

## 2. What we built

```
k8s/base/
├── api-hpa.yaml   # new - min 1, max 3, target 50% CPU
└── web-hpa.yaml   # new - min 1, max 5, target 50% CPU
```

Values mirror the real `dev.tfvars`/`staging.tfvars` exactly (`api_min_replicas: 1,
api_max_replicas: 3`, `web_max_replicas: 5`). prod's overlay now patches the HPA's own
`minReplicas`/`maxReplicas` instead of the Deployment's `replicas` field:

```yaml
# k8s/overlays/prod/kustomization.yaml
- target: { kind: HorizontalPodAutoscaler, name: ecommerce-api }
  patch: |-
    - op: replace
      path: /spec/minReplicas
      value: 2
    - op: replace
      path: /spec/maxReplicas
      value: 10
```

`2`/`10` is prod's real Terraform value too (`api_min_replicas: 2, api_max_replicas: 10`) - K5's
"static floor" comment said K6 would replace it with a real min/max range, and this is that. The
CPU/memory sizing patch prod already had stays exactly as K5 left it; only the replica-count patch
changed shape.

### Honest gap, worth stating plainly

Real Container Apps in this project doesn't actually scale on CPU by default - the Terraform
module doesn't configure a custom scale rule, so Azure falls back to its own default (HTTP
concurrency-based). CPU utilization here is a reasonable, common HPA metric to learn the
*mechanism* with, but it isn't a byte-for-byte mirror of what's deployed in Azure - the min/max
**replica counts** are the part that genuinely matches; the *trigger* doesn't.

---

## 3. Verified - a real scale-up and a real scale-down, both watched live

Lightweight product-list requests, even 40 concurrent `wget` loops against the Service, never
pushed CPU usage anywhere near the 50% target (it sat at 7-9% the whole time) - the endpoint
genuinely isn't CPU-heavy enough on its own. Rather than fight the app's own workload shape to
force a number, CPU was burned **directly inside the running API container** via `kubectl exec`
(two `while true; do :; done` shell loops) - a clean, honest way to prove the autoscaler mechanism
itself works, independent of how expensive any particular endpoint happens to be.

| t+ | `cpu:` (target 50%) | Replicas | What happened |
|---|---|---|---|
| 8s | 7% | 1 | baseline, before the burn started reporting |
| 16s | **234%** | 1 → 3 (scaling) | HPA reacted within one ~15s metrics cycle |
| 32s | 499% | 3 | hit `maxReplicas` - can't scale past the ceiling regardless of how far over target |
| 48s-96s | 261% → 175% | 3 | usage spreading as new replicas came up; still only 1 of 3 Pods was actually loaded |
| load stopped | | | |
| 0-285s after | 8-19% | 3 | **held at 3 the whole time** - no scale-down yet |
| 300s after | 8% | 3 → 2 | scale-down begins |
| 345s after | 10% | 2 → 1 | back to baseline |

The 300-second hold before the first scale-down isn't a fluke or a slow reaction - it's the HPA's
default `stabilizationWindowSeconds: 300` for scale-down decisions, and it's deliberate:
scaling *up* fast is cheap insurance against real load; scaling *down* fast risks flapping
(dropping a replica the instant load dips, then immediately needing it back). Scale-up has no such
window by default - which the 16-second reaction time above demonstrates directly.

Nothing about this required touching Postgres, Redis, Web, or the original Pod's identity - the
first API Pod (`ecommerce-api-...-4rn2j`) never restarted through any of it; the two extra
replicas were created and torn down around it.

---

## 4. Next

K6 completes the roadmap's core Kubernetes concepts against this project's own stack. K7 (AKS via
Terraform) remains a stretch goal, gated on this subscription's vCPU quota rather than anything
left to learn locally.
