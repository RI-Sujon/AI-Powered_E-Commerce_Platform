variable "name" {
  type        = string
  description = "Globally-unique PostgreSQL Flexible Server name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group in which to create the server."
}

variable "location" {
  type        = string
  description = "Azure region for the server."
}

variable "postgres_version" {
  type        = string
  description = "PostgreSQL major version."
  default     = "16"
}

variable "administrator_login" {
  type        = string
  description = "Administrator login name."
}

variable "administrator_password" {
  type        = string
  description = "Administrator password. Supply via TF_VAR_postgres_admin_password or a gitignored *.auto.tfvars file - never commit it."
  sensitive   = true
}

variable "database_name" {
  type        = string
  description = "Name of the application database created on the server."
}

variable "storage_mb" {
  type        = number
  description = "Allocated storage in MB."
  default     = 32768
}

variable "sku_name" {
  type        = string
  description = "Flexible Server SKU, e.g. B_Standard_B1ms, GP_Standard_D2s_v3."
  default     = "B_Standard_B1ms"
}

variable "backup_retention_days" {
  type        = number
  description = "Number of days to retain backups."
  default     = 7
}

variable "geo_redundant_backup_enabled" {
  type        = bool
  description = "Whether backups are geo-redundant."
  default     = false
}

variable "allow_azure_services" {
  type        = bool
  description = "Whether to add the firewall rule allowing traffic from Azure services (0.0.0.0-0.0.0.0)."
  default     = true
}

variable "allowed_ip_ranges" {
  description = "Extra named firewall rules, e.g. { office = { start_ip_address = \"1.2.3.4\", end_ip_address = \"1.2.3.4\" } }."
  type = map(object({
    start_ip_address = string
    end_ip_address   = string
  }))
  default = {}
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to the server."
  default     = {}
}
