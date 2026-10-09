variable "resource_group_name" {
  type        = string
  description = "Existing resource group that holds the shared platform resources."
  default     = "ecommerce-rg"
}

variable "location" {
  type    = string
  default = "eastus"
}

variable "postgres_server_name" {
  type        = string
  description = "Name of the existing shared PostgreSQL Flexible Server."
  default     = "ecommerce-postgres-rabiuru"
}

variable "postgres_allowlisted_extensions" {
  type        = list(string)
  description = "Extensions added to the server's azure.extensions allowlist. VECTOR (pgvector) powers Phase 5 semantic search. Each still needs CREATE EXTENSION per database."
  default     = ["VECTOR"]
}

variable "openai_account_name" {
  type        = string
  description = "Globally-unique name for the Azure OpenAI account (also its DNS subdomain)."
  default     = "ecommerce-openai-rabiuru"
}

variable "openai_local_auth_enabled" {
  type        = bool
  description = "Allow API-key auth on the OpenAI account. Keep true until the app authenticates purely via managed identity, then flip to false."
  default     = true
}

variable "openai_chat_capacity" {
  type        = number
  description = "Thousands of TPM for the chat deployment."
  default     = 20
}

variable "openai_embedding_capacity" {
  type        = number
  description = "Thousands of TPM for the embedding deployment."
  default     = 20
}

variable "acr_name" {
  type        = string
  description = "Globally-unique name for the shared Azure Container Registry (login server becomes <name>.azurecr.io). 5-50 characters, letters and digits only - no hyphens."
  default     = "ecommerceacrrabiuru"
}

variable "acr_sku" {
  type        = string
  description = "ACR SKU. Basic (~$0.167/day) is cheapest and enough for a learning project's two small images."
  default     = "Basic"
}
