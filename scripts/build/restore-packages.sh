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

header "Restoring packages"
$DNX_ROOT/dnu restore "$REPOROOT/src" --quiet --runtime "$RID" "$NOCACHE" --parallel
$DNX_ROOT/dnu restore "$REPOROOT/test" --quiet --runtime "$RID" "$NOCACHE" --parallel
$DNX_ROOT/dnu restore "$REPOROOT/tools" --quiet --runtime "$RID" "$NOCACHE" --parallel
set +e
$DNX_ROOT/dnu restore "$REPOROOT/testapp" --quiet --runtime "$RID" "$NOCACHE" --parallel >/dev/null 2>&1
set -e
