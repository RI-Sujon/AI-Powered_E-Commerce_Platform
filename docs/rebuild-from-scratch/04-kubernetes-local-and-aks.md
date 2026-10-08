# 4 — Kubernetes: local (K1–K6), then AKS (K7)

Detailed lessons already exist — this doc is the **sequence and commands**. Lessons:
[docs/kubernetes-learning/](../kubernetes-learning/README.md). Manifests: [k8s/](../../k8s/).

## Part A — Local cluster ($0, do this any time)

### A.0 Cluster
Docker Desktop → Settings → Kubernetes → enable (kind provisioner, 1 node) → wait for green.
```powershell
kubectl config use-context docker-desktop
kubectl get nodes                      # 1 node, Ready
```
If `kubectl` says "connection refused", Docker Desktop / its Kubernetes isn't running.

### A.1 Images (local, never pushed)
Base manifests reference `ecommerce-api:k3-local` and `ecommerce-web:k2-local`
(`imagePullPolicy: IfNotPresent` — Docker Desktop shares its image cache with the cluster):
```powershell
cd src\ProjectMainApp
docker build -f Project.Endpoint/Dockerfile -t ecommerce-api:k3-local .
docker build -f Project.Web/Dockerfile      -t ecommerce-web:k2-local .
```

### A.2 Phase map — what each phase teaches and its signature command

| Phase | Idea | Do / prove |
|---|---|---|
| K1 | Pod → ReplicaSet → Deployment; Service = stable name | `kubectl apply -f k8s/base/namespace.yaml -f k8s/base/api-deployment.yaml -f k8s/base/api-service.yaml`; `kubectl get pods -n ecommerce`; `kubectl logs`, `describe`, `exec`, `port-forward` |
| K2 | ConfigMap (settings), Secret (creds), PVC (disk) | delete the Postgres pod → data survives: `kubectl delete pod -l app=postgres -n ecommerce` |
| K3 | startup / readiness / liveness probes on `/health`, `/health/live` | break the probe path → watch `kubectl get pods -w` restart/NotReady |
| K4 | Ingress = one entrypoint (`/` → web, `/api` → api) | `helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx`; `helm install ingress-nginx ingress-nginx/ingress-nginx --namespace ingress-nginx --create-namespace --set controller.service.type=LoadBalancer`; test `curl http://localhost/api/product/get-product-list` |
| K5 | Kustomize: base + per-env overlays | `kubectl kustomize k8s/overlays/prod`; `kubectl apply -k k8s/overlays/dev` |
| K6 | HPA needs metrics-server | `kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml`, then **local-only** patch `--kubelet-insecure-tls`; CPU-burn the API pod and `kubectl get hpa -w` |

### A.3 The everyday `kubectl` set (write these from memory)

```powershell
kubectl get pods -n ecommerce -o wide         kubectl get all -n ecommerce
kubectl describe pod <name> -n ecommerce      # Events at the bottom explain failures
kubectl logs <pod> -n ecommerce --tail 50     kubectl logs <pod> -f
kubectl exec -it <pod> -n ecommerce -- sh
kubectl port-forward -n ecommerce svc/ecommerce-web 8080:8080
kubectl rollout status deploy/ecommerce-api -n ecommerce
kubectl rollout undo deploy/ecommerce-api -n ecommerce
kubectl top nodes ; kubectl top pods -n ecommerce
kubectl delete -k k8s/overlays/dev            # remove everything an overlay created
```
Debug order for "pod isn't Running": `get pods` (STATUS) → `describe pod` (Events) → `logs`.
`ImagePullBackOff` = image name/tag wrong or not pullable. `CrashLoopBackOff` = app starts then dies →
`logs --previous`. `Pending` = no resources/PVC unbound → `describe`.

## Part B — AKS (K7) — real Azure, ~3 hours, ~$0.15, then destroy

Plan, costs and rationale: [k7-aks.md](../kubernetes-learning/k7-aks.md). Terraform:
[terraform/environments/aks-learning/](../../terraform/environments/aks-learning/).

### B.0 Before you start
1. Subscription accepts writes (test with `az group create`, doc 0).
2. **Everything else is deleted or you accept its cost** (`az resource list -o table`). The cheap AKS
   plan assumes nothing else is running.
3. vCPU quota: `az vm list-usage -l eastus -o table` → need 2 free for `Standard_B2s`. If region quota
   is too low, change `location` in `aks-learning/terraform.tfvars`. If `Standard_B2s` is unavailable,
   the apply error says which sizes are allowed; edit `node_vm_size`.
4. Set the stopwatch. Destroy is the last step, always.

### B.1 Provision (~15–25 min)
```powershell
cd terraform\environments\aks-learning
terraform init
terraform plan      # expect 2 to add: resource group + AKS cluster
terraform apply
```
Local state (no backend) on purpose: the cluster lives one sitting.

### B.2 Connect
```powershell
az aks get-credentials -g rg-ecommerce-aks-learning -n aks-ecommerce-learning
kubectl config current-context        # must be aks-ecommerce-learning — NOT docker-desktop
kubectl get nodes                     # 1 node, Ready
kubectl top nodes                     # works with no setup: metrics-server is built in on AKS
```
Always check the context before applying/deleting. Wrong context = changes on the wrong cluster.

### B.3 Deploy the stack
```powershell
cd ..\..\..                                    # repo root
kubectl apply -k k8s/overlays/aks
kubectl get pods -n ecommerce-aks -w           # postgres, redis, api, web → Running
```
**Why the `aks` overlay:** `k8s/base` uses images that exist only in Docker Desktop's cache; on AKS they
give `ImagePullBackOff`. The overlay rewrites the two images to `docker.io/rabiul1012/ecommerceapp-*:latest`
(change to your Docker Hub user). Check the 4 pods fit on one B2s node: `kubectl describe node`
(Allocated resources). Pending pods = not enough CPU/RAM → lower requests or scale something down.

### B.4 Prove the same things you proved locally
- Probes: `kubectl describe pod <api>` → startup/readiness pass; pod `Ready 1/1`.
- Persistence: `kubectl delete pod -l app=postgres -n ecommerce-aks` → new pod, data still there.
- HPA: `kubectl exec -n ecommerce-aks deploy/ecommerce-api -- sh -c 'while :; do :; done' &`
  then `kubectl get hpa -n ecommerce-aks -w` (replicas climb, up to the node's capacity).
- Reachability **without a Load Balancer** (an LB costs money):
  `kubectl port-forward -n ecommerce-aks svc/ecommerce-web 8080:8080` → http://localhost:8080
  (or `az aks command invoke -g ... -n ... --command "kubectl get pods -A"`).

### B.5 Write down what happened (5 min), then destroy
```powershell
kubectl delete -k k8s/overlays/aks             # optional, tidy
cd terraform\environments\aks-learning
terraform destroy
az aks list -o table                           # must be empty
az group list -o table                         # rg-ecommerce-aks-learning must be gone
kubectl config get-contexts                    # then: kubectl config delete-context aks-ecommerce-learning
```
**Do not end the session before this passes.** Idle cost of the node is ~$1/day.

## Done when
Part A: you can bring the whole stack up locally from an empty cluster without the doc.
Part B: you provisioned, deployed, verified, destroyed, and `az aks list` is empty.
