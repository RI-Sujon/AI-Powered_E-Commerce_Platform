variable "name" {
  type        = string
  description = "Globally-unique registry name. Becomes the login server <name>.azurecr.io. Must be 5-50 characters, letters and digits only (no hyphens)."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group in which to create the registry."
}

variable "location" {
  type        = string
  description = "Azure region for the registry."
}

variable "sku" {
  type        = string
  description = "Basic, Standard, or Premium. Basic is cheapest and enough for a learning project."
  default     = "Basic"
}

variable "admin_enabled" {
  type        = bool
  description = "Enable the registry's built-in admin username/password login. Simplest way to authenticate Container Apps to pull images; disable later in favor of managed-identity + AcrPull if you want keyless auth."
  default     = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
