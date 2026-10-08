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
