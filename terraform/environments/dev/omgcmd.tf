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
    key                  = "UnityNuGet.environments.dev.tfstate"
  }

  required_version = ">= 0.13"

  required_providers {
    acme = {
      source  = "xaevman/acme"
      version = "~> 0.0.5"
    }
    aws = {
      version = "~> 3.74.3"
    }
    rollbar = {
      source  = "rollbar/rollbar"
      version = "~> 1.4.0"
    }
  }
}

provider "acme" {
  server_url = "https://acme-v02.api.letsencrypt.org/directory"
}

provider "aws" {
  region  = "us-west-2"
  profile = local.use_assume_role ? null : "dev"

  allowed_account_ids = local.use_assume_role ? null : [ module.locals.infra.core.core_env.aws.account_id ]

  dynamic "assume_role" {
    for_each = local.use_assume_role ? [{
      role_arn = module.locals.infra.core.core_env.const.dev_admin_arns[0]
    }] : []
    content {
      role_arn = assume_role.value["role_arn"]
    }
  }
}

provider "aws" {
  alias   = "eu_north"
  region  = "eu-north-1"
  profile = local.use_assume_role ? null : "dev"

  allowed_account_ids = local.use_assume_role ? null : [ module.locals.infra.core.core_env.aws.account_id ]

  dynamic "assume_role" {
    for_each = local.use_assume_role ? [{
      role_arn = module.locals.infra.core.core_env.const.dev_admin_arns[0]
    }] : []
    content {
      role_arn = assume_role.value["role_arn"]
    }
  }
}

provider "aws" {
  alias   = "core"
  region  = "us-west-2"
  profile = local.use_assume_role ? null : "core"

  allowed_account_ids = local.use_assume_role ? null : [ "276128656246" ]

  dynamic "assume_role" {
    for_each = local.use_assume_role ? [{
      role_arn = module.locals.infra.core.core_env.const.newcore_admin_arns[0]
    }] : []
    content {
      role_arn = assume_role.value["role_arn"]
    }
  }
}

provider "aws" {
  alias   = "core_services"
  region  = "us-west-2"
  profile = local.use_assume_role ? null : "core_services"

  allowed_account_ids = local.use_assume_role ? null : [ "053014315747" ]

  dynamic "assume_role" {
    for_each = local.use_assume_role? [{
      role_arn = module.locals.infra.core.core_env.const.core_admin_arns[0]
    }] : []
    content {
      role_arn = assume_role.value["role_arn"]
    }
  }
}

provider "nomad" {
  region = "us-west-2"
}

provider "nomad" {
  alias  = "eu_north"
  region = "eu-north-1"
}

provider "rollbar" {
  api_key = data.vault_generic_secret.rollbar.data["api_key"]
}

provider "vault" {
  address = "https://${module.locals.infra.core.core_env.vault.addr}"
}

data "vault_generic_secret" "rollbar" {
  path = "secret/rollbar"
}

locals {
  project_name           = "UnityNuGet"
  project_description    = ""
  environment_name       = "dev"
  infra_environment_name = "dev"
  use_assume_role        = var.buildserver ? true : var.use_assume_role

  tags = {
    Environment      = "dev"
    InfraEnvironment = "dev"
    Project          = "UnityNuGet"
  }
}

module "locals" {
  source = "./modules/locals"
}

variable "buildserver" {
  description = "Whether or not the terraform run is happening on a build server."
  type        = bool
  default     = false
}

variable "use_assume_role" {
  description = "Whether or not to assume_role in AWS providers or just use current AWS login context."
  type        = bool
  default     = false
}
