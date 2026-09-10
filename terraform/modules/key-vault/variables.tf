variable "name" {
  type        = string
  description = "Globally-unique Key Vault name (3-24 alphanumeric/hyphen chars)."
}

variable "location" {
  type        = string
  description = "Azure region for the Key Vault."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group in which to create the Key Vault."
}

variable "sku_name" {
  type        = string
  description = "Key Vault SKU (standard or premium)."
  default     = "standard"
}

variable "purge_protection_enabled" {
  type        = bool
  description = "Whether purge protection is enabled. Recommended true for prod."
  default     = false
}

variable "soft_delete_retention_days" {
  type        = number
  description = "Number of days deleted vaults/secrets are retained."
  default     = 7
}

variable "public_network_access_enabled" {
  type        = bool
  description = "Whether the vault is reachable over the public internet."
  default     = true
}

variable "grant_deployer_access" {
  type        = bool
  description = "Whether to grant the Terraform deployer identity Key Vault Secrets Officer access."
  default     = true
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to the Key Vault."
  default     = {}
}
