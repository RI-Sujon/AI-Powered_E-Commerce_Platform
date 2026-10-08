# 5 — Teardown (leave $0 running)

Do this at the end of **every** session, and fully at the end of the trial. Order matters: delete
what *depends on* things first, and delete the Terraform state **last**.

## 5.1 Per-environment (Terraform-managed things)

```powershell
cd terraform\environments\dev        # then staging, prod
$env:TF_VAR_postgres_admin_password = 'x'   # destroy still needs the required variables set (any value)
$env:TF_VAR_jwt_key                 = 'x'
terraform destroy
```
Removes only that environment's apps, identity, role assignment, dev's `ecommercedb_dev` and
dev's Redis. It never touches `ecommerce-rg`, the Container Apps env, Postgres, Key Vault (those were
only `data`-read).

`shared`: `terraform destroy` removes the OpenAI account (and the extension allow-list parameter).

## 5.2 AKS
```powershell
cd terraform\environments\aks-learning ; terraform destroy
az aks list -o table                 # empty
```

## 5.3 The by-hand platform (doc 1), reverse order

```powershell
az group delete -n ecommerce-rg -y --no-wait         # env, Log Analytics, Postgres, Key Vault, anything left
# Only when you're done with this account entirely (this deletes Terraform's state!):
az group delete -n ecommerce-tfstate-rg -y --no-wait
```
Key Vault and Cognitive Services are *soft-deleted*: the name stays reserved for a while.
If you rebuild with the same names and get "name already in use / soft-deleted", purge:
`az keyvault purge -n <name>` / `az cognitiveservices account purge ...`, or pick new names.

## 5.4 Verify nothing is left (don't trust the delete, check)

```powershell
az group list -o table                       # only DefaultResourceGroup-* style leftovers
az resource list -o table                    # empty or nothing billable
az aks list -o table ; az containerapp list -o table
```
Portal: Cost Management → Cost analysis → group by **Resource** → look at the last 24 h.
Also stop the ADO agent (`Ctrl+C` on `run.cmd`) and delete service connections / PAT if abandoning
the account.

## 5.5 Local cleanup
```powershell
kubectl config get-contexts                  # delete stale AKS contexts
kubectl config delete-context aks-ecommerce-learning
docker system prune                          # optional
```
