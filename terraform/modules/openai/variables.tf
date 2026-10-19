variable "name" {
  type        = string
  description = "Name of the Azure OpenAI (Cognitive Services) account. Also used as the custom subdomain, so it must be globally unique and DNS-safe."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group to create the account in."
}

variable "location" {
  type        = string
  description = "Azure region. Must offer the requested models - eastus does."
  default     = "eastus"
}

variable "local_auth_enabled" {
  type        = bool
  description = "Whether API-key auth is allowed. Start true for easy local dev; flip to false once every caller uses managed identity."
  default     = true
}

# ---- Chat model deployment -------------------------------------------------
# NOTE: this subscription has 0 quota on regional "Standard" for the mini
# models, so the chat deployment must be "GlobalStandard" (verified via
# `az cognitiveservices usage list`). Embeddings are fine on "Standard".

variable "chat_deployment_name" {
  type        = string
  description = "Deployment name the app targets for chat completions."
  default     = "chat"
}

variable "chat_model_name" {
  type    = string
  default = "gpt-4.1-mini"
}

variable "chat_model_version" {
  type    = string
  default = "2025-04-14"
}

variable "chat_scale_type" {
  type        = string
  description = "Deployment type. Must be GlobalStandard on this subscription (regional Standard quota for mini models is 0)."
  default     = "GlobalStandard"
}

variable "chat_capacity" {
  type        = number
  description = "Thousands of tokens-per-minute for the chat deployment. Small on purpose - raise if you hit 429s."
  default     = 20
}

# ---- Embedding model deployment -----------------------------------------------

variable "embedding_deployment_name" {
  type        = string
  description = "Deployment name the app targets for embeddings."
  default     = "embeddings"
}

variable "embedding_model_name" {
  type    = string
  default = "text-embedding-3-small"
}

variable "embedding_model_version" {
  type    = string
  default = "1"
}

variable "embedding_scale_type" {
  type    = string
  default = "Standard"
}

variable "embedding_capacity" {
  type        = number
  description = "Thousands of tokens-per-minute for the embedding deployment."
  default     = 20
}

variable "rai_policy_name" {
  type        = string
  description = "Responsible-AI / content-filter policy applied to each deployment. Azure attaches Microsoft.DefaultV2 automatically; pinning it here keeps `terraform plan` clean and keeps content filtering on."
  default     = "Microsoft.DefaultV2"
}

variable "tags" {
  type    = map(string)
  default = {}
}
