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
readonly VERSION_SCRIPT="$WORKSPACE/bin/version"

if [[ -s "$VERSION_SCRIPT" ]]; then
  echo "--- parse version"
  source "$VERSION_SCRIPT"
fi

export readonly VERSION="${MAJOR_VER:-0}.${MINOR_VER:-0}.${BUILDKITE_BUILD_NUMBER:-1}"

echo "--- set meta-data"
echo "version=$VERSION"
buildkite-agent meta-data set "version" "$VERSION"
