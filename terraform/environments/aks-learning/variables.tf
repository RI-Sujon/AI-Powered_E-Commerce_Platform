variable "resource_group_name" {
  description = "Resource group created for the throwaway AKS cluster."
  type        = string
  default     = "rg-ecommerce-aks-learning"
}

variable "location" {
  description = "Azure region. Must have vCPU quota for the node size below."
  type        = string
  default     = "eastus"
}

variable "cluster_name" {
  description = "AKS cluster name (also used as the DNS prefix)."
  type        = string
  default     = "aks-ecommerce-learning"
}

variable "node_vm_size" {
  description = "Node size. Standard_B2s would be the cheapest 2 vCPU/4GB floor, but this subscription's offer type blocks the whole B-series (burstable) family outright - confirmed via a failed `terraform apply` on 2026-10-12, not just a quota number. Standard_D2as_v4 (2 vCPU, 8GB, AMD) is the cheapest allowed size on this account, at the same $0.096/hr Linux PAYG rate as its Intel twin Standard_D2s_v4."
  type        = string
  default     = "Standard_D2as_v4"
}

variable "shared_resource_group_name" {
  description = "Resource group hosting the EXISTING shared ACR (created by azure-pipelines.yml)."
  type        = string
  default     = "ecommerce-rg"
}

variable "acr_name" {
  description = "Name of the shared Azure Container Registry that holds the api/web images."
  type        = string
  default     = "ecommerceacrrabiuru"
}
