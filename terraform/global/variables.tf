# Reference-only file.
#
# Like providers.tf, this is not automatically shared across the dev/staging/prod root modules
# (Terraform doesn't support importing variable declarations between independent root modules).
# It documents the common naming/tag conventions used across environments so environment
# terraform.tfvars files stay consistent - copy values from here when adding a new environment.

variable "project_name" {
  type        = string
  description = "Short project name used as a naming prefix across all environments."
  default     = "ecommerce"
}

variable "common_tags" {
  type        = map(string)
  description = "Baseline tags merged into every environment's resources (each environment also adds its own `environment` tag)."
  default = {
    project    = "ecommerce"
    managed_by = "terraform"
  }
}

# Naming convention reference (actual names live in each environment's terraform.tfvars):
#   resource group:            ecommerce-rg-<env>
#   key vault:                 kv-ecommerce-<env>
#   container apps environment: ecommerce-env-<env>
#   postgresql flexible server: ecommerce-pg-<env>
#   api container app:          ecommerce-api-<env>
#   web container app:          ecommerce-web-<env>
