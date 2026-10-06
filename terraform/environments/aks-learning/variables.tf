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
  description = "Node size. Standard_B2s (2 vCPU, 4GB) is the practical AKS floor; B1s is rejected."
  type        = string
  default     = "Standard_B2s"
}
