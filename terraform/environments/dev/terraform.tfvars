# Non-sensitive default values for the dev environment.
# Secrets (postgres_admin_password, jwt_key) are intentionally NOT set here - export them as
# TF_VAR_postgres_admin_password / TF_VAR_jwt_key, or pass -var-file with a gitignored file.

location = "eastus"

resource_group_name            = "ecommerce-rg-dev"
key_vault_name                  = "kv-ecommerce-dev"
container_app_environment_name = "ecommerce-env-dev"
postgres_server_name            = "ecommerce-pg-dev"

postgres_admin_login   = "postgres"
postgres_database_name = "ecommercedb"
postgres_sku_name       = "B_Standard_B1ms"
postgres_storage_mb     = 32768

api_image_repository = "docker.io/rabiul1012/ecommerceapp-api"
web_image_repository = "docker.io/rabiul1012/ecommerceapp-web"
image_tag             = "latest"

jwt_issuer          = "ECommerceProject"
jwt_audience        = "ECommerceProjectUsers"
jwt_expiry_minutes = 60

aspnetcore_environment = "Development"

api_min_replicas = 1
api_max_replicas = 3
api_cpu           = 0.5
api_memory        = "1Gi"

web_min_replicas = 1
web_max_replicas = 5
web_cpu           = 0.5
web_memory        = "1Gi"

tags = {
  costCenter = "dev"
}
