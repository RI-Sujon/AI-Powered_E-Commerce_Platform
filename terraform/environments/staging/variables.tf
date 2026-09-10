variable "location" {
  type        = string
  description = "Azure region for all resources."
  default     = "eastus"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the resource group for the staging environment."
}

variable "key_vault_name" {
  type        = string
  description = "Globally-unique Key Vault name for the staging environment."
}

variable "key_vault_purge_protection_enabled" {
  type        = bool
  description = "Whether Key Vault purge protection is enabled. Recommended true for prod."
  default     = false
}

variable "container_app_environment_name" {
  type        = string
  description = "Name of the Container Apps Environment for the staging environment."
}

variable "postgres_server_name" {
  type        = string
  description = "Globally-unique PostgreSQL Flexible Server name for the staging environment."
}

variable "postgres_admin_login" {
  type        = string
  description = "PostgreSQL administrator login."
  default     = "postgres"
}

variable "postgres_admin_password" {
  type        = string
  description = "PostgreSQL administrator password. Supply via TF_VAR_postgres_admin_password - do not put it in terraform.tfvars."
  sensitive   = true
}

variable "postgres_database_name" {
  type        = string
  description = "Application database name."
  default     = "ecommercedb"
}

variable "postgres_sku_name" {
  type        = string
  description = "PostgreSQL Flexible Server SKU."
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  type        = number
  description = "PostgreSQL storage size in MB."
  default     = 32768
}

variable "api_image_repository" {
  type        = string
  description = "API image repository, e.g. docker.io/rabiul1012/ecommerceapp-api."
}

variable "web_image_repository" {
  type        = string
  description = "Web image repository, e.g. docker.io/rabiul1012/ecommerceapp-web."
}

variable "image_tag" {
  type        = string
  description = "Image tag to deploy."
  default     = "latest"
}

variable "jwt_key" {
  type        = string
  description = "JWT signing key. Supply via TF_VAR_jwt_key - do not put it in terraform.tfvars."
  sensitive   = true
}

variable "jwt_issuer" {
  type        = string
  description = "JWT issuer."
  default     = "ECommerceProject"
}

variable "jwt_audience" {
  type        = string
  description = "JWT audience."
  default     = "ECommerceProjectUsers"
}

variable "jwt_expiry_minutes" {
  type        = number
  description = "JWT expiry in minutes."
  default     = 60
}

variable "aspnetcore_environment" {
  type        = string
  description = "ASPNETCORE_ENVIRONMENT value for the container apps."
  default     = "Staging"
}

variable "api_min_replicas" {
  type    = number
  default = 1
}

variable "api_max_replicas" {
  type    = number
  default = 3
}

variable "api_cpu" {
  type    = number
  default = 0.5
}

variable "api_memory" {
  type    = string
  default = "1Gi"
}

variable "web_min_replicas" {
  type    = number
  default = 1
}

variable "web_max_replicas" {
  type    = number
  default = 5
}

variable "web_cpu" {
  type    = number
  default = 0.5
}

variable "web_memory" {
  type    = string
  default = "1Gi"
}

variable "tags" {
  type        = map(string)
  description = "Extra tags merged into the common tag set."
  default     = {}
}
