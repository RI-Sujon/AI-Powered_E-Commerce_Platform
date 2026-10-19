variable "name" {
  type        = string
  description = "Name of the Container Apps Environment."
}

variable "location" {
  type        = string
  description = "Azure region for the environment."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group in which to create the environment."
}

variable "log_retention_days" {
  type        = number
  description = "Log Analytics retention in days."
  default     = 30
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to the environment and its Log Analytics workspace."
  default     = {}
}
