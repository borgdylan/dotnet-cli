#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common-mono.sh"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -d $DOTNET_INSTALL_DIR ] || mkdir -p $DOTNET_INSTALL_DIR

# Ensure the latest stage0 is installed
header "Installing dotnet stage 0"
#$REPOROOT/scripts/obtain/install.sh

# Put the stage0 on the PATH
#export PATH=$REPOROOT/artifacts/$RID/stage0/bin:$PATH

# Download DNX to copy into stage2
#header "Downloading DNX $DNX_VERSION"
#$REPOROOT/scripts/obtain/install-dnx-mono.sh
