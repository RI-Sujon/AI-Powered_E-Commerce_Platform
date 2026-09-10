variable "name" {
  type        = string
  description = "Name of the Container App."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group in which to create the Container App."
}

variable "container_app_environment_id" {
  type        = string
  description = "Resource ID of the Container Apps Environment to deploy into."
}

variable "image" {
  type        = string
  description = "Full container image reference, e.g. docker.io/rabiul1012/ecommerceapp-api:latest."
}

variable "target_port" {
  type        = number
  description = "Port the container listens on."
  default     = 8080
}

variable "external_ingress" {
  type        = bool
  description = "Whether ingress accepts traffic from outside the environment."
  default     = true
}

variable "cpu" {
  type        = number
  description = "CPU cores allocated to the container (e.g. 0.25, 0.5, 1.0)."
  default     = 0.5
}

variable "memory" {
  type        = string
  description = "Memory allocated to the container (e.g. \"1Gi\")."
  default     = "1Gi"
}

variable "min_replicas" {
  type        = number
  description = "Minimum number of replicas."
  default     = 1
}

variable "max_replicas" {
  type        = number
  description = "Maximum number of replicas."
  default     = 3
}

variable "registry_server" {
  type        = string
  description = "Private registry login server, e.g. myregistry.azurecr.io. Leave empty for public images (e.g. public Docker Hub repos)."
  default     = ""
}

variable "registry_username" {
  type        = string
  description = "Private registry username. Required only when registry_server is set."
  default     = ""
}

variable "registry_password" {
  type        = string
  description = "Private registry password/token. Supply via TF_VAR_ environment variable, never commit it."
  default     = ""
  sensitive   = true
}

variable "env_vars" {
  description = "Environment variables. Set `value` for plain values or `secret_name` to reference an entry in `secrets`."
  type = list(object({
    name        = string
    value       = optional(string)
    secret_name = optional(string)
  }))
  default = []
}

variable "secrets" {
  description = "Secret values available to the container via secret_name in env_vars (e.g. connection strings, JWT keys)."
  type = list(object({
    name  = string
    value = string
  }))
  default   = []
  sensitive = true
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to the Container App."
  default     = {}
}
