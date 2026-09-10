# Non-sensitive default values for the production environment.
# Secrets (postgres_admin_password, jwt_key) are intentionally NOT set here - export them as
# TF_VAR_postgres_admin_password / TF_VAR_jwt_key, or pass -var-file with a gitignored file.
# In CI, prefer wiring these from an Azure DevOps secret variable group / Key Vault task.

location = "eastus"

resource_group_name            = "ecommerce-rg-prod"
key_vault_name                  = "kv-ecommerce-prod"
container_app_environment_name = "ecommerce-env-prod"
postgres_server_name            = "ecommerce-pg-prod"

postgres_admin_login             = "postgres"
postgres_database_name           = "ecommercedb"
postgres_sku_name                 = "GP_Standard_D2s_v3"
postgres_storage_mb               = 65536
postgres_backup_retention_days   = 14
postgres_geo_redundant_backup_enabled = true

api_image_repository = "docker.io/rabiul1012/ecommerceapp-api"
web_image_repository = "docker.io/rabiul1012/ecommerceapp-web"
image_tag             = "latest"

jwt_issuer          = "ECommerceProject"
jwt_audience        = "ECommerceProjectUsers"
jwt_expiry_minutes = 60

aspnetcore_environment = "Production"

api_min_replicas = 2
api_max_replicas = 10
api_cpu           = 1.0
api_memory        = "2Gi"

web_min_replicas = 2
web_max_replicas = 10
web_cpu           = 1.0
web_memory        = "2Gi"

tags = {
  costCenter = "production"
}
