#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

# Copyright (C) 2022 One More Game - All Rights Reserved
# Unauthorized copying of this file, via any medium is strictly prohibited
# Proprietary and confidential

terraform {
  backend "azurerm" {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "unitynuget.ci.tfstate"
  }
}

data "terraform_remote_state" "buildkite" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "core-infrastructure.services.buildkite.tfstate"
  }
}

data "terraform_remote_state" "github" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-tf-root"
    storage_account_name = "omgtfroot"
    container_name       = "tfstate"
    key                  = "core-infrastructure.services.github.tfstate"
  }
}
