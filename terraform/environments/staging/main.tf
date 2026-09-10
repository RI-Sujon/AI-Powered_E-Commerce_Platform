terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.90.0, < 4.0.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# --------------------------------------------------------------------------
# This subscription only allows ONE Container Apps Environment per region and
# has no spare quota for a second PostgreSQL Flexible Server, so dev/staging/
# prod deliberately do NOT create their own resource group/environment/
# server/vault. Instead they read the existing ones (created previously by
# azure-pipelines.yml) as read-only data sources - Terraform never creates,
# modifies, or destroys them - and only manage what genuinely differs per
# environment: the two container apps. The database ("ecommercedb") is shared
# and pre-existing too, so Terraform only references its name, never creates it.
# --------------------------------------------------------------------------

data "azurerm_resource_group" "shared" {
  name = var.shared_resource_group_name
}

data "azurerm_container_app_environment" "shared" {
  name                = var.shared_container_app_environment_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

data "azurerm_postgresql_flexible_server" "shared" {
  name                = var.shared_postgres_server_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

data "azurerm_key_vault" "shared" {
  name                = var.shared_key_vault_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

# The single Azure OpenAI account created by the `shared` stack. Read-only here; this
# environment only grants its API app access to it (see azurerm_role_assignment below).
data "azurerm_cognitive_account" "openai" {
  name                = var.openai_account_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

locals {
  environment = "staging"

  common_tags = merge(var.tags, {
    environment = local.environment
    project     = "ecommerce"
    managed_by  = "terraform"
  })

  api_app_name = "ecommerce-api-${local.environment}"
  web_app_name = "ecommerce-web-${local.environment}"

  # Container App FQDNs are predictable (<app-name>.<environment default_domain>), so both apps'
  # URLs can be wired up without a circular dependency between the api/web modules.
  api_fqdn = "${local.api_app_name}.${data.azurerm_container_app_environment.shared.default_domain}"
  web_fqdn = "${local.web_app_name}.${data.azurerm_container_app_environment.shared.default_domain}"

  # Staging shares the existing "ecommercedb" (schema + data already there). Terraform does not
  # create or manage that database - it's a read-only shared resource like the server itself -
  # so the name is just referenced from a variable. See terraform/README.md section 3.
  postgres_connection_string = "Host=${data.azurerm_postgresql_flexible_server.shared.fqdn};Database=${var.postgres_database_name};Username=${var.postgres_admin_login};Password=${var.postgres_admin_password};SSL Mode=Require;"
}

module "api_app" {
  source                       = "../../modules/container-app"
  name                         = local.api_app_name
  resource_group_name          = data.azurerm_resource_group.shared.name
  container_app_environment_id = data.azurerm_container_app_environment.shared.id
  image                        = "${var.api_image_repository}:${var.image_tag}"
  target_port                  = 8080
  health_probe_path            = "/health"
  assign_identity              = true # needed so the API can call Azure OpenAI via RBAC
  location                     = data.azurerm_resource_group.shared.location
  min_replicas                 = var.api_min_replicas
  max_replicas                 = var.api_max_replicas
  cpu                          = var.api_cpu
  memory                       = var.api_memory
  tags                         = local.common_tags

  secrets = [
    { name = "postgres-connection", value = local.postgres_connection_string },
    { name = "jwt-key", value = var.jwt_key },
  ]

  env_vars = [
    { name = "ConnectionStrings__DefaultConnection", secret_name = "postgres-connection" },
    { name = "Jwt__Key", secret_name = "jwt-key" },
    { name = "Jwt__Issuer", value = var.jwt_issuer },
    { name = "Jwt__Audience", value = var.jwt_audience },
    { name = "Jwt__ExpiryMinutes", value = tostring(var.jwt_expiry_minutes) },
    { name = "Cors__AllowedOrigins__0", value = "https://${local.web_fqdn}" },
    { name = "ASPNETCORE_ENVIRONMENT", value = var.aspnetcore_environment },
    # Azure OpenAI: endpoint + deployment names. No key - the app authenticates with its
    # user-assigned managed identity. AZURE_CLIENT_ID tells DefaultAzureCredential which
    # identity to use (required when the identity is user-assigned).
    { name = "AZURE_CLIENT_ID", value = module.api_app.identity_client_id },
    { name = "AzureOpenAI__Endpoint", value = data.azurerm_cognitive_account.openai.endpoint },
    { name = "AzureOpenAI__ChatDeployment", value = var.openai_chat_deployment },
    { name = "AzureOpenAI__EmbeddingDeployment", value = var.openai_embedding_deployment },
  ]
}

# Data-plane access: the API's managed identity may call the shared Azure OpenAI account.
# "Cognitive Services OpenAI User" allows inference (chat/embeddings) but not management.
resource "azurerm_role_assignment" "api_openai" {
  scope                = data.azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = module.api_app.principal_id
}

module "web_app" {
  source                       = "../../modules/container-app"
  name                         = local.web_app_name
  resource_group_name          = data.azurerm_resource_group.shared.name
  container_app_environment_id = data.azurerm_container_app_environment.shared.id
  image                        = "${var.web_image_repository}:${var.image_tag}"
  target_port                  = 8080
  min_replicas                 = var.web_min_replicas
  max_replicas                 = var.web_max_replicas
  cpu                          = var.web_cpu
  memory                       = var.web_memory
  tags                         = local.common_tags

  env_vars = [
    { name = "ApiBaseUrl", value = "https://${local.api_fqdn}" },
    { name = "ASPNETCORE_ENVIRONMENT", value = var.aspnetcore_environment },
  ]
}
