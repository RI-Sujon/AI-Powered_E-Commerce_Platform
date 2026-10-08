# 2 — Terraform: OpenAI + dev / staging / prod apps

Prerequisite: doc 1 done. Work in `terraform/`. Full reference (read the "why" there):
[terraform/README.md](../../terraform/README.md).

## 2.0 The mental model (30 seconds)

```
terraform init   → download azurerm provider, connect to the backend (state in Azure Storage)
terraform plan   → compare .tf files  vs  state  vs  real Azure → print the diff, change nothing
terraform apply  → perform the diff, then update the state
terraform destroy→ delete everything *this folder's state* owns (data blocks are never deleted)
```
Each folder under `environments/` is its own root module with its own state (`backend.tf`).
`modules/` are templates they call. Only `modules/container-app` and `modules/openai` are used.

## 2.1 Update the files for your new account (once)

For every name you changed in doc 1.3, update: `environments/*/backend.tf` (storage account),
`environments/*/terraform.tfvars` and `shared/terraform.tfvars` (server / vault / account names,
image repos, `image_tag`). Check with `terraform fmt -recursive` then `terraform validate` in each
environment (no Azure needed):

```powershell
cd terraform\environments\dev
terraform init -backend=false     # skip the remote state just to validate syntax
terraform validate
```

## 2.2 `shared` stack first (Azure OpenAI + pgvector allow-list)

```powershell
az cognitiveservices usage list -l eastus -o table     # is there quota for OpenAI / gpt-4.1-mini?
cd terraform\environments\shared
terraform init
terraform plan          # expect: openai account, 2 deployments (chat, embeddings), postgres config
terraform apply
terraform output
```
- Creates `ecommerce-openai-sujon` + deployments **`chat`** (gpt-4.1-mini, GlobalStandard) and
  **`embeddings`** (text-embedding-3-small). 20K TPM each.
- Also sets the server parameter `azure.extensions = VECTOR`. That only *permits* the extension.
- If apply fails on quota/region/model availability, that's a subscription limit, not your code —
  read the error, try another region in `shared/terraform.tfvars`.

Then **create the extension in every database that will exist** (Postgres needs a firewall rule
for your own IP first):

```powershell
$ip = (Invoke-RestMethod https://api.ipify.org)
az postgres flexible-server firewall-rule create -g ecommerce-rg -n ecommerce-postgres-sujon `
   --rule-name my-ip --start-ip-address $ip --end-ip-address $ip
# then, with psql / pgAdmin / any client, connected over SSL as 'postgres':
#   CREATE EXTENSION IF NOT EXISTS vector;
```
Run it in `ecommercedb` now. For `ecommercedb_dev`, run it right after dev creates the database
(2.3). Delete the `my-ip` rule when finished.

## 2.3 `dev` environment

Secrets are **never** in `.tfvars` — they are environment variables for this shell only:

```powershell
cd terraform\environments\dev
$env:TF_VAR_postgres_admin_password = '<the password from doc 1.7>'
$env:TF_VAR_jwt_key                 = '<any random string, 32+ chars>'
terraform init
terraform plan          # READ it. Expect: db ecommercedb_dev, redis, 2 container apps, identity, role assignment
terraform apply
terraform output        # api_url, web_url, postgres_fqdn
```
What gets created: database `ecommercedb_dev` (on the shared server), **Redis Basic C0** (costs
money — see doc 0), API app (with a user-assigned managed identity + the role *Cognitive Services
OpenAI User* on the OpenAI account), Web app.

Verify:
```powershell
curl https://<api-fqdn>/health        # Healthy
az containerapp logs show -n ecommerce-api-dev -g ecommerce-rg --tail 50
```
Order gotcha: the app runs EF migrations at startup (Development only). If the DB lacks the
`vector` extension, a migration that needs it fails. Fix: `CREATE EXTENSION vector;` in
`ecommercedb_dev`, then restart the app:
`az containerapp revision restart ...` or push a new image tag.

The role assignment can take a few minutes to propagate; AI endpoints return 401/403 until then.

## 2.4 `staging` and `prod`

Same commands in their folders, with `TF_VAR_jwt_key` = that environment's own key.

Two things that bite on a **fresh** account (they were already solved on the old one):

1. **No migrations outside Development.** `Program.cs` calls `MigrateAsync()` only when
   `IsDevelopment()`. Staging/prod connect to `ecommercedb`, which is empty on a new server, so the
   API fails on first query. Apply the schema once yourself (needs the firewall rule from 2.2 and the
   `dotnet-ef` tool: `dotnet tool install -g dotnet-ef`):
   ```powershell
   cd src\ProjectMainApp
   dotnet ef database update --project Project.Data --startup-project Project.Endpoint `
     --connection "Host=<server>.postgres.database.azure.com;Database=ecommercedb;Username=postgres;Password=<pw>;SSL Mode=Require"
   ```
2. **Do not run the pipeline's `terraform import` blocks** on a fresh account. They adopt apps that
   already existed from the old imperative pipeline; on a clean account the apps don't exist, so
   `import` fails. In `azure-pipelines.yml` delete the two `if ($inState -notcontains ...)` import
   blocks in the Staging and Production stages (doc 3.6), or apply staging/prod once by hand.

Prod is sized larger (`min_replicas = 2`, 1 CPU / 2 Gi) → it is the most expensive app. Skip it
unless you are practising promotion.

## 2.5 Everyday commands (memorise these)

```powershell
terraform fmt -recursive      # format
terraform validate            # syntax check, no Azure
terraform plan                # preview, read-only
terraform apply               # do it
terraform output              # URLs
terraform state list          # what this folder manages
terraform destroy             # delete what this folder created
```
Steady state: `terraform plan` → **No changes.** If it shows changes you didn't make, something
drifted or a `lifecycle.ignore_changes` is missing.

A Terraform change to only a *secret* value doesn't roll a new revision — force one:
`az containerapp update -n <app> -g ecommerce-rg --revision-suffix r$(Get-Date -Format MMddHHmm)`.

## 2.6 Done when

- `shared`, `dev` (and optionally staging/prod) show **No changes** on a second `terraform plan`.
- `/health` returns `Healthy` for each API you deployed.
- `terraform state list` in each folder shows only what you expect.

Next: [03-azure-devops-pipeline.md](03-azure-devops-pipeline.md).
