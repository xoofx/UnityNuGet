#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

# Copyright (C) 2022 One More Game - All Rights Reserved
# Unauthorized copying of this file, via any medium is strictly prohibited
# Proprietary and confidential

output "infra" {
  description = "infrastructure state export"
  value = {
    apps     = data.terraform_remote_state.infra_apps.outputs.v1
    boundary = data.terraform_remote_state.infra_boundary.outputs
    core     = data.terraform_remote_state.infra_core.outputs.v1
    meta     = merge(
      data.terraform_remote_state.infra_core.outputs.v1.core_env,
      {
        api   = data.terraform_remote_state.infra_apps.outputs.v1.api
        fabio = data.terraform_remote_state.infra_apps.outputs.v1.fabio
      }
    )
  }
}

output "global_config" {
  value = {
    meta = merge(
      data.terraform_remote_state.infra_core.outputs.v2.global_config,
      {
        api   = data.terraform_remote_state.infra_apps.outputs.v1.api
        fabio = data.terraform_remote_state.infra_apps.outputs.v1.fabio
      }
    )
  }
}

output "files" {
  description = "metadata about the file outputs from the module"
  value       = {
    "srv_env" = {
      filename = abspath(local_file.srv_env.filename)
    }
    "cli_env" = {
      filename = abspath(local_file.cli_env.filename)
    }
    "boundary" = {
      filename = abspath(local_file.boundary_env.filename)
    }
    "consul_cat" = {
      filename = abspath(local_file.consul_ca.filename)
    }
    "nomad_ca" = {
      filename = abspath(local_file.nomad_ca.filename)
    }
    "vault_ca" = {
      filename = abspath(local_file.vault_ca.filename)
    }
  }
}
