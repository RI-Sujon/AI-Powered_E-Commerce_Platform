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

# Read-only reference to the shared ACR (already created by azure-pipelines.yml / the `shared`
# stack) - images are pushed there, not to Docker Hub, so AKS needs pull access to it instead of
# relying on public images.
data "azurerm_resource_group" "shared" {
  name = var.shared_resource_group_name
}

data "azurerm_container_registry" "shared" {
  name                = var.acr_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

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

    # AKS stamps these defaults on the node pool itself (not something you can leave "disabled"
    # like oidc_issuer_enabled, but left unset here it caused a no-op drift/update on every plan).
    # Pin them to what Azure already applies so `terraform plan` comes back clean.
    upgrade_settings {
      max_surge                     = "10%"
      drain_timeout_in_minutes      = 0
      node_soak_duration_in_minutes = 0
    }
  }

  identity {
    type = "SystemAssigned"
  }

  # AKS enables this by default on current API versions regardless of what Terraform asks for,
  # and once on, Azure refuses to turn it back off ("OIDCIssuerFeatureCannotBeDisabled"). Pin it
  # to `true` explicitly so the plan matches reality instead of trying to disable it every apply.
  oidc_issuer_enabled = true

  # kubenet + the default Standard LB SKU, but nothing creates a Service of
  # type LoadBalancer, so no public IP / LB rule is billed. Reach the cluster
  # with `az aks command invoke` or `kubectl port-forward`.
  network_profile {
    network_plugin = "kubenet"
  }

  tags = local.tags
}

# Grants the cluster's auto-created kubelet identity `AcrPull` on the shared ACR, so
# `k8s/overlays/aks` can reference ACR images with no imagePullSecrets needed. Equivalent to
# `az aks update --attach-acr`, done declaratively so a fresh `terraform apply` is self-contained.
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                = data.azurerm_container_registry.shared.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

locals {
  tags = {
    project    = "ecommerce"
    managed_by = "terraform"
    stack      = "aks-learning"
    lifetime   = "one-session-destroy-after"
  }
}
