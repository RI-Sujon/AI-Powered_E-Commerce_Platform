# Non-sensitive default values for the production environment.
# Secrets (postgres_admin_password, jwt_key) are intentionally NOT set here - export them as
# TF_VAR_postgres_admin_password / TF_VAR_jwt_key, or pass -var-file with a gitignored file.
# In CI, prefer wiring these from an Azure DevOps secret variable group / Key Vault task.
#
# This subscription only allows 1 Container Apps Environment per region and has no spare quota
# for a 2nd PostgreSQL Flexible Server, so dev/staging/prod all point at the SAME existing shared
# platform resources (created earlier by azure-pipelines.yml) and only manage their own container
# apps. Prod connects to the existing shared "ecommercedb" (not managed by Terraform).

shared_resource_group_name            = "ecommerce-rg"
shared_container_app_environment_name = "ecommerce-env"
shared_postgres_server_name           = "ecommerce-postgres-rabiuru"
shared_key_vault_name                 = "ecommerce-kv-rabiuru"

postgres_admin_login   = "postgres"
postgres_database_name = "ecommercedb"

api_image_repository = "docker.io/rabiul1012/ecommerceapp-api"
web_image_repository = "docker.io/rabiul1012/ecommerceapp-web"
# Currently-deployed build. azure-pipelines.yml overrides this per run with
# `terraform apply -var image_tag=$(Build.BuildId)`; keeping it in sync here means a manual
# no-arg `terraform apply` doesn't roll the app back to an old image.
image_tag = "44"

jwt_issuer         = "ECommerceProject"
jwt_audience       = "ECommerceProjectUsers"
jwt_expiry_minutes = 60

aspnetcore_environment = "Production"

api_min_replicas = 2
api_max_replicas = 10
api_cpu          = 1.0
api_memory       = "2Gi"

web_min_replicas = 2
web_max_replicas = 10
web_cpu          = 1.0
web_memory       = "2Gi"

tags = {
  costCenter = "production"
}
