// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if NETCOREAPP1_0
using System.Runtime.Loader;
#endif
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    public class MulticoreJitActivator
    {
        public bool TryActivateMulticoreJit()
        {
            var disableMulticoreJit = IsMulticoreJitDisabled();
                
            if (disableMulticoreJit)
            {
                return false;
            }

            StartCliProfileOptimization();
            
            return true;
        }
        
        private bool IsMulticoreJitDisabled()
        {
            #if NETCOREAPP1_0
            return Env.GetEnvironmentVariableAsBool("DOTNET_DISABLE_MULTICOREJIT");
            #else
            return false;
            #endif
        }
        
        private void StartCliProfileOptimization()
        {
            #if NETCOREAPP1_0
            var profileOptimizationRootPath = new MulticoreJitProfilePathCalculator().MulticoreJitProfilePath;

            if (!TryEnsureDirectory(profileOptimizationRootPath))
            {
                return;
            }
            
            AssemblyLoadContext.Default.SetProfileOptimizationRoot(profileOptimizationRootPath);
            
            AssemblyLoadContext.Default.StartProfileOptimization("dotnet");
            #endif
        }

        private bool TryEnsureDirectory(string directoryPath)
        {
            try
            {
                PathUtility.EnsureDirectory(directoryPath);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
