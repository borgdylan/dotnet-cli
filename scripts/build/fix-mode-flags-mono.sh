#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Managed code doesn't need 'x'
find . -type f -name "*.dll" | xargs chmod 644
find . -type f -name "*.exe" | xargs chmod 644

# Generally, dylibs and sos have 'x' (no idea if it's required ;))
#if [ "$(uname)" == "Darwin" ]; then
#    find . -type f -name "*.dylib" | xargs chmod 755
#else
#    find . -type f -name "*.so" | xargs chmod 755
#fi

# Executables (those without dots) are executable :)
chmod 755 bin/csc
chmod 755 bin/csi
chmod 755 bin/fsc
chmod 755 bin/fsi
chmod 755 bin/dotnet
chmod 755 bin/dotnet-nuget
