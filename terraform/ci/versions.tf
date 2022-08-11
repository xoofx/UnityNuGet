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
  required_version = ">= 0.13"
  required_providers {
    buildkite = {
      source  = "buildkite/buildkite"
      version = "~> 0.5.0"
    }
    github = {
      source  = "integrations/github"
      version = "~> 4.19"
    }
  }
}
