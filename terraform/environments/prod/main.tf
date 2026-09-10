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
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}

locals {
  environment = "prod"

  common_tags = merge(var.tags, {
    environment = local.environment
    project     = "ecommerce"
    managed_by  = "terraform"
  })

  api_app_name = "ecommerce-api-${local.environment}"
  web_app_name = "ecommerce-web-${local.environment}"

  # Container App FQDNs are predictable (<app-name>.<environment default_domain>), so both apps'
  # URLs can be wired up without a circular dependency between the api/web modules.
  api_fqdn = "${local.api_app_name}.${module.container_app_env.default_domain}"
  web_fqdn = "${local.web_app_name}.${module.container_app_env.default_domain}"
}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = var.resource_group_name
  location = var.location
  tags     = local.common_tags
}

module "key_vault" {
  source                    = "../../modules/key-vault"
  name                       = var.key_vault_name
  location                   = var.location
  resource_group_name        = module.resource_group.name
  purge_protection_enabled   = var.key_vault_purge_protection_enabled
  tags                       = local.common_tags
}

module "postgresql" {
  source                        = "../../modules/postgresql"
  name                          = var.postgres_server_name
  resource_group_name           = module.resource_group.name
  location                      = var.location
  administrator_login           = var.postgres_admin_login
  administrator_password        = var.postgres_admin_password
  database_name                 = var.postgres_database_name
  sku_name                      = var.postgres_sku_name
  storage_mb                    = var.postgres_storage_mb
  backup_retention_days         = var.postgres_backup_retention_days
  geo_redundant_backup_enabled  = var.postgres_geo_redundant_backup_enabled
  tags                          = local.common_tags
}

module "container_app_env" {
  source              = "../../modules/container-app-env"
  name                = var.container_app_environment_name
  location            = var.location
  resource_group_name = module.resource_group.name
  tags                = local.common_tags
}

module "api_app" {
  source                        = "../../modules/container-app"
  name                          = local.api_app_name
  resource_group_name           = module.resource_group.name
  container_app_environment_id  = module.container_app_env.id
  image                         = "${var.api_image_repository}:${var.image_tag}"
  target_port                   = 8080
  min_replicas                  = var.api_min_replicas
  max_replicas                  = var.api_max_replicas
  cpu                            = var.api_cpu
  memory                         = var.api_memory
  tags                           = local.common_tags

  secrets = [
    { name = "postgres-connection", value = module.postgresql.connection_string },
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
  ]
}

module "web_app" {
  source                        = "../../modules/container-app"
  name                          = local.web_app_name
  resource_group_name           = module.resource_group.name
  container_app_environment_id  = module.container_app_env.id
  image                         = "${var.web_image_repository}:${var.image_tag}"
  target_port                   = 8080
  min_replicas                  = var.web_min_replicas
  max_replicas                  = var.web_max_replicas
  cpu                            = var.web_cpu
  memory                         = var.web_memory
  tags                           = local.common_tags

  env_vars = [
    { name = "ApiBaseUrl", value = "https://${local.api_fqdn}" },
    { name = "ASPNETCORE_ENVIRONMENT", value = var.aspnetcore_environment },
  ]
}
