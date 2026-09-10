variable "resource_group_name" {
  type        = string
  description = "Existing resource group that holds the shared platform resources."
  default     = "ecommerce-rg"
}

variable "location" {
  type    = string
  default = "eastus"
}

variable "openai_account_name" {
  type        = string
  description = "Globally-unique name for the Azure OpenAI account (also its DNS subdomain)."
  default     = "ecommerce-openai-sujon"
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
