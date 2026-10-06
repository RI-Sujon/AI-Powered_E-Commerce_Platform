terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.90.0, < 4.0.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# ---------------------------------------------------------------------------
# K7 - a throwaway, single-node AKS cluster for a timeboxed learning session
# (see docs/kubernetes-learning/k7-aks.md). Independent of dev/staging/prod:
# own resource group, own (local, ephemeral) state. Apply -> verify -> destroy.
# ---------------------------------------------------------------------------

resource "azurerm_resource_group" "aks" {
  name     = var.resource_group_name
  location = var.location

  tags = local.tags
}

resource "azurerm_kubernetes_cluster" "aks" {
  name                = var.cluster_name
  location            = azurerm_resource_group.aks.location
  resource_group_name = azurerm_resource_group.aks.name
  dns_prefix          = var.cluster_name

  # Free control plane, no uptime SLA - irrelevant for a learning session.
  sku_tier = "Free"

  # Exactly one fixed node: the subscription's regional vCPU quota is 4, and
  # no autoscaler means no node can appear unattended and cost money.
  default_node_pool {
    name            = "system"
    vm_size         = var.node_vm_size
    node_count      = 1
    os_disk_size_gb = 30
  }

  identity {
    type = "SystemAssigned"
  }

  # kubenet + the default Standard LB SKU, but nothing creates a Service of
  # type LoadBalancer, so no public IP / LB rule is billed. Reach the cluster
  # with `az aks command invoke` or `kubectl port-forward`.
  network_profile {
    network_plugin = "kubenet"
  }

  tags = local.tags
}

locals {
  tags = {
    project    = "ecommerce"
    managed_by = "terraform"
    stack      = "aks-learning"
    lifetime   = "one-session-destroy-after"
  }
}
