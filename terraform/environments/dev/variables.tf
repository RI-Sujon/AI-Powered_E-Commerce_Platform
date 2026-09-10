variable "shared_resource_group_name" {
  type        = string
  description = "Name of the EXISTING resource group (already created by azure-pipelines.yml) that hosts the shared platform resources."
  default     = "ecommerce-rg"
}

variable "shared_container_app_environment_name" {
  type        = string
  description = "Name of the EXISTING Container Apps Environment shared by all environments (this subscription allows only one per region)."
  default     = "ecommerce-env"
}

variable "shared_postgres_server_name" {
  type        = string
  description = "Name of the EXISTING PostgreSQL Flexible Server shared by all environments."
  default     = "ecommerce-postgres-sujon"
}

variable "shared_key_vault_name" {
  type        = string
  description = "Name of the EXISTING Key Vault shared by all environments."
  default     = "ecommerce-kv-sujon"
}

variable "openai_account_name" {
  type        = string
  description = "Name of the Azure OpenAI account created by the `shared` stack."
  default     = "ecommerce-openai-sujon"
}

variable "openai_chat_deployment" {
  type        = string
  description = "Chat model deployment name on the shared Azure OpenAI account."
  default     = "chat"
}

variable "openai_embedding_deployment" {
  type        = string
  description = "Embedding model deployment name on the shared Azure OpenAI account."
  default     = "embeddings"
}

variable "postgres_admin_login" {
  type        = string
  description = "Administrator login of the existing PostgreSQL server."
  default     = "postgres"
}

variable "postgres_admin_password" {
  type        = string
  description = "Administrator password of the existing PostgreSQL server. Supply via TF_VAR_postgres_admin_password - do not put it in terraform.tfvars."
  sensitive   = true
}

variable "postgres_database_name" {
  type        = string
  description = "Name of the dedicated database this environment creates on the shared server."
  default     = "ecommercedb_dev"
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
  default     = "Development"
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
