# Non-sensitive default values for the staging environment.
# Secrets (postgres_admin_password, jwt_key) are intentionally NOT set here - export them as
# TF_VAR_postgres_admin_password / TF_VAR_jwt_key, or pass -var-file with a gitignored file.
#
# This subscription only allows 1 Container Apps Environment per region and has no spare quota
# for a 2nd PostgreSQL Flexible Server, so dev/staging/prod all point at the SAME existing shared
# platform resources (created earlier by azure-pipelines.yml) and only manage their own container
# apps. Staging connects to the existing shared "ecommercedb" (not managed by Terraform).

shared_resource_group_name            = "ecommerce-rg"
shared_container_app_environment_name = "ecommerce-env"
shared_postgres_server_name           = "ecommerce-postgres-sujon"
shared_key_vault_name                 = "ecommerce-kv-sujon"

postgres_admin_login   = "postgres"
postgres_database_name = "ecommercedb"

api_image_repository = "docker.io/rabiul1012/ecommerceapp-api"
web_image_repository = "docker.io/rabiul1012/ecommerceapp-web"
# Currently-deployed build. azure-pipelines.yml overrides this per run with
# `terraform apply -var image_tag=$(Build.BuildId)`; keeping it in sync here means a manual
# no-arg `terraform apply` doesn't roll the app back to an old image.
image_tag = "38"

jwt_issuer         = "ECommerceProject"
jwt_audience       = "ECommerceProjectUsers"
jwt_expiry_minutes = 60

aspnetcore_environment = "Staging"

api_min_replicas = 1
api_max_replicas = 3
api_cpu          = 0.5
api_memory       = "1Gi"

web_min_replicas = 1
web_max_replicas = 5
web_cpu          = 0.5
web_memory       = "1Gi"

tags = {
  costCenter = "staging"
}
