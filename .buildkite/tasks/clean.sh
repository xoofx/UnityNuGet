#!/usr/bin/env bash

#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

# Copyright (C) 2022 One More Game - All Rights Reserved
# Unauthorized copying of this file, via any medium is strictly prohibited
# Proprietary and confidential
#
# shellcheck disable=SC1090

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
WORKSPACE="$(realpath "$SCRIPT_DIR/../..")"
readonly WORKSPACE
readonly CLEAN_SCRIPT="$WORKSPACE/bin/clean"

if [[ -s "$CLEAN_SCRIPT" ]]; then
  echo "--- clean"
  "$CLEAN_SCRIPT"
fi
