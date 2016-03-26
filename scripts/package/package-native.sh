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

source "$DIR/../common/_common.sh"

if [[ "$OSNAME" == "ubuntu" ]]; then
    # Create Debian package
    $REPOROOT/scripts/package/package-debian.sh -v "0.1.0.0" -i "$STAGE2_DIR" -o "dotnet-nightly" -p "dotnet-nightly" -m "$REPOROOT/Documentation/manpages" --previous-version-url "https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-ubuntu-x64.latest.deb"
elif [[ "$OSNAME" == "osx" ]]; then
    # Create OSX PKG
    $REPOROOT/packaging/osx/package-osx.sh
fi
