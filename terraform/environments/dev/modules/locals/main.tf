#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

# Copyright (C) 2022 One More Game - All Rights Reserved
# Unauthorized copying of this file, via any medium is strictly prohibited
# Proprietary and confidential

data "terraform_remote_state" "infra_apps" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "core-infrastructure.environments.dev.apps.tfstate"
  }
}

data "terraform_remote_state" "infra_boundary" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "core-infrastructure.environments.dev.boundary.tfstate"
  }
}

data "terraform_remote_state" "infra_core" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "core-infrastructure.environments.dev.core.tfstate"
  }
}

variable "buildserver" {
  description = "Whether or not the terraform run is happening on a build server."
  type        = bool
  default     = false
}

module "const" {
  source = "git@github.com:PlayOneMoreGame/terraform-modules//const"
}

locals {
  core_env = data.terraform_remote_state.infra_core.outputs.v1.core_env
  core_tpl = merge(
    data.terraform_remote_state.infra_core.outputs.v1.templates,
    data.terraform_remote_state.infra_boundary.outputs,
  )
}

resource "local_file" "env" {
  filename = "./out/.generated.env"
  content = var.buildserver ? local.core_tpl.srv : local.core_tpl.cli
}

resource "local_file" "cli_env" {
  filename = "./out/.generated.cli.env"
  content = local.core_tpl.cli
}

resource "local_file" "srv_env" {
  filename = "./out/.generated.srv.env"
  content = local.core_tpl.srv
}

resource "local_file" "boundary_env" {
  filename = "./out/.generated.auth-method"
  content = local.core_tpl.auth_method_env
}

resource "local_file" "consul_ca" {
  filename = "./out/consul-ca.crt"
  content = local.core_tpl.consul_ca
}

resource "local_file" "nomad_ca" {
  filename = "./out/nomad-ca.crt"
  content = local.core_tpl.nomad_ca
}

resource "local_file" "vault_ca" {
  filename = "./out/vault-ca.crt"
  content = local.core_tpl.vault_ca
}
