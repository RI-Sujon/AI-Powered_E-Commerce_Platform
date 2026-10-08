# 1 — Azure foundation (the part Terraform does NOT create)

Terraform in this repo only *reads* the platform (`data` blocks) and *creates* the per-environment
apps. So these things must exist first, made by hand with `az`. This is the "chicken-and-egg" layer:
Terraform's own state storage can't be made by Terraform, because Terraform needs it to start.

> Commands below are PowerShell. Written from general Azure knowledge (the originals were not
> recorded in git). If a flag errors, run the command with `--help`.

## 1.0 Tools you need installed

`az` (Azure CLI), `terraform` (>= 1.5), `docker` (Docker Desktop), `kubectl`, `helm`, `git`.
Check each with `<tool> version`.

## 1.1 Log in and pick the subscription

```powershell
az login
az account list -o table
az account set -s "<subscription name or id>"
az account show --query "{name:name,id:id}" -o table
```
*Why:* every command acts on the *current* subscription. Always `az account show` first.

## 1.2 Register resource providers

A new subscription has many providers switched off; creating a resource whose provider isn't
registered fails with `MissingSubscriptionRegistration`.

```powershell
foreach ($p in 'Microsoft.App','Microsoft.OperationalInsights','Microsoft.DBforPostgreSQL',
               'Microsoft.KeyVault','Microsoft.CognitiveServices','Microsoft.ContainerService',
               'Microsoft.Cache','Microsoft.Storage') {
  az provider register -n $p --wait
}
az extension add -n containerapp --upgrade
```

## 1.3 Pick your unique names (do this on paper first)

These must be **globally unique** across all of Azure, so the `-sujon` suffix may be taken on a new
account. Pick one suffix, e.g. `rabiul01`, and use it everywhere:

| Thing | Rules | Old value | Your value |
|---|---|---|---|
| Postgres server | lowercase, letters/digits/hyphen | `ecommerce-postgres-sujon` | |
| Key Vault | 3–24 chars | `ecommerce-kv-sujon` | |
| OpenAI account | becomes the URL subdomain | `ecommerce-openai-sujon` | |
| State storage ×4 | 3–24, lowercase+digits only | `ecommercetfstate{dev,stg,prod,shared}` | |
| Docker Hub user | your own account | `rabiul1012` | |

If you change a name, change it in **every** place it appears — find them with
`git grep -n "ecommerce-postgres-sujon"` etc. (each `environments/*/terraform.tfvars`,
`shared/terraform.tfvars`, each `backend.tf`, `azure-pipelines.yml`).

## 1.4 Resource groups

```powershell
$loc = 'eastus'
az group create -n ecommerce-rg        -l $loc   # the app platform
az group create -n ecommerce-tfstate-rg -l $loc  # Terraform state only — never put apps here
```
*Why two:* app resources come and go; state must outlive them. Deleting `ecommerce-rg` must never
delete your state.

## 1.5 Terraform state storage (one account per environment)

```powershell
foreach ($n in 'ecommercetfstatedev','ecommercetfstatestg','ecommercetfstateprod','ecommercetfstateshared') {
  az storage account create -n $n -g ecommerce-tfstate-rg -l $loc --sku Standard_LRS
  az storage container create -n tfstate --account-name $n
}
```
Matches each `terraform/environments/*/backend.tf` (`storage_account_name`, `container_name = "tfstate"`,
`key = "<env>.terraform.tfstate"`). *Why separate accounts:* separate state per env, so `destroy`
in dev can never touch prod.

If `az storage container create` complains about auth, add `--auth-mode login` and make sure you
have role *Storage Blob Data Contributor* on the account.

## 1.6 Container Apps environment

```powershell
az containerapp env create -n ecommerce-env -g ecommerce-rg -l $loc
az containerapp env show -n ecommerce-env -g ecommerce-rg --query properties.defaultDomain -o tsv
```
*Why:* an "environment" is the shared network/log boundary that container apps run inside. It
creates a Log Analytics workspace automatically. The printed `defaultDomain` is what Terraform
uses to predict each app's URL (`<app>.<defaultDomain>`).

## 1.7 PostgreSQL Flexible Server (+ the databases)

```powershell
$pw = Read-Host "Postgres admin password" -MaskInput      # remember it — it is a Terraform input later
az postgres flexible-server create -g ecommerce-rg -n ecommerce-postgres-sujon -l $loc `
  --admin-user postgres --admin-password $pw `
  --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 16 `
  --public-access 0.0.0.0 --yes
az postgres flexible-server db create -g ecommerce-rg -s ecommerce-postgres-sujon -d ecommercedb
```
- `--public-access 0.0.0.0` = the "allow Azure services" firewall rule, so Container Apps can connect.
- `ecommercedb` is the database **staging and prod** use. **dev** makes its own `ecommercedb_dev`
  through Terraform, so don't create that one by hand.
- The `vector` extension needs two steps (done in doc 2): allow-list it on the server (Terraform
  `shared` stack does it), then `CREATE EXTENSION vector;` **in each database**.

## 1.8 Key Vault

```powershell
az keyvault create -n ecommerce-kv-sujon -g ecommerce-rg -l $loc --enable-rbac-authorization true
```
*Why:* the Terraform envs `data`-read it. It isn't wired to the apps at runtime yet (see
`terraform/README.md` §9). If you don't need it, delete the `azurerm_key_vault` data block instead.

## 1.9 Docker Hub images (must exist before Terraform deploys apps)

Terraform's `container-app` module points apps at `docker.io/<user>/ecommerceapp-{api,web}:<image_tag>`.
`terraform.tfvars` pins `image_tag = "45"` — **that tag will not exist on your fresh Docker Hub**, and
the apps would fail to start. Push your own first image and set `image_tag` to it:

```powershell
docker login
cd src\ProjectMainApp
docker build -f Project.Endpoint/Dockerfile -t <you>/ecommerceapp-api:1 .
docker build -f Project.Web/Dockerfile      -t <you>/ecommerceapp-web:1 .
docker push <you>/ecommerceapp-api:1
docker push <you>/ecommerceapp-web:1
```
Make both repositories **public** (no registry credentials in Terraform). Then in each
`environments/*/terraform.tfvars` set `api_image_repository`, `web_image_repository`, `image_tag = "1"`.
Later the pipeline (doc 3) does exactly this build+push for you, tagging with `$(Build.BuildId)`.

## 1.10 Done when…

```powershell
az group list -o table                         # ecommerce-rg, ecommerce-tfstate-rg
az resource list -g ecommerce-tfstate-rg -o table   # 4 storage accounts
az resource list -g ecommerce-rg -o table      # env, log workspace, postgres, key vault
az postgres flexible-server db list -g ecommerce-rg -s ecommerce-postgres-sujon -o table
```
Then continue to [02-terraform.md](02-terraform.md).
