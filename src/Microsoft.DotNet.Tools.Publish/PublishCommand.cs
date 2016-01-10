﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Publish
{
    public class PublishCommand
    {
        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string OutputPath { get; set; }
        public string Framework { get; set; }
        public string Runtime { get; set; }
        public bool NativeSubdirectories { get; set; }
        public NuGetFramework NugetFramework { get; set; }
        public IEnumerable<ProjectContext> ProjectContexts { get; set; }

        public int NumberOfProjects { get; private set; }
        public int NumberOfPublishedProjects { get; private set; }

        public bool TryPrepareForPublish()
        {
            if (Framework != null)
            {
                NugetFramework = NuGetFramework.Parse(Framework);

                if (NugetFramework.IsUnsupported)
                {
                    Reporter.Output.WriteLine($"Unsupported framework {Framework}.".Red());
                    return false;
                }
            }

            ProjectContexts = SelectContexts(ProjectPath, NugetFramework, Runtime);
            if (!ProjectContexts.Any())
            {
                string errMsg = $"'{ProjectPath}' cannot be published  for '{Framework ?? "<no framework provided>"}' '{Runtime ?? "<no runtime provided>"}'";
                Reporter.Output.WriteLine(errMsg.Red());
                return false;
            }

            return true;
        }

        public void PublishAllProjects()
        {
            NumberOfPublishedProjects = 0;
            NumberOfProjects = 0;
            foreach (var project in ProjectContexts)
            {
                if (PublishProjectContext(project, OutputPath, Configuration, NativeSubdirectories))
                {
                    NumberOfPublishedProjects++;
                }

                NumberOfProjects++;
            }
        }
		
		private static bool RIDEquals(string rid1, string rid2)
		{
			if (Regex.IsMatch(rid1, "^ubuntu(.)*$") && Regex.IsMatch(rid2, "^ubuntu(.)*$"))
			{
				string[] r1 = rid1.Split(new char[] {'-'});
				string[] r2 = rid2.Split(new char[] {'-'});
				
				return r1[1] == r2[1];
			}
			else
			{
				return string.Equals(rid1, rid2, StringComparison.OrdinalIgnoreCase);
			}
		}
		
        /// <summary>
        /// Publish the project for given 'framework (ex - dnxcore50)' and 'runtimeID (ex - win7-x64)'
        /// </summary>
        /// <param name="context">project that is to be published</param>
        /// <param name="outputPath">Location of published files</param>
        /// <param name="configuration">Debug or Release</param>
        /// <returns>Return 0 if successful else return non-zero</returns>
        private static bool PublishProjectContext(ProjectContext context, string outputPath, string configuration, bool nativeSubdirectories)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier.Yellow()}");

            var options = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);

            // Generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName(),
                    context.RuntimeIdentifier);
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var result = Command.Create("dotnet-build",
                $"--framework \"{context.TargetFramework.DotNetFrameworkName}\" " +
                $"--output \"{outputPath}\" " +
                $"--configuration \"{configuration}\" " +
                "--no-host " +
                $"\"{context.ProjectFile.ProjectDirectory}\"")
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            if (result.ExitCode != 0)
            {
                return false;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration);

            foreach (var export in exporter.GetAllExports())
            {
                // Skip copying project references
                if (export.Library is ProjectDescription)
                {
                    continue;
                }

                Reporter.Verbose.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishFiles(export.RuntimeAssemblies, outputPath, false);
                PublishFiles(export.NativeLibraries, outputPath, nativeSubdirectories);
            }

            // Publish a host if this is an application
            if (options.EmitEntryPoint.GetValueOrDefault())
            {
                Reporter.Verbose.WriteLine($"Making {context.ProjectFile.Name.Cyan()} runnable ...");
                PublishHost(context, outputPath);
            }

            Reporter.Output.WriteLine($"Published to {outputPath}".Green().Bold());
            return true;
        }

        private static int PublishHost(ProjectContext context, string outputPath)
        {
            if (context.TargetFramework.IsDesktop())
            {
                return 0;
            }

            var hostPath = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            if (!File.Exists(hostPath))
            {
                Reporter.Error.WriteLine($"Cannot find {Constants.HostExecutableName} in the dotnet directory.".Red());
                return 1;
            }

            var outputExe = Path.Combine(outputPath, context.ProjectFile.Name + Constants.ExeSuffix);

            // Copy the host
            File.Copy(hostPath, outputExe, overwrite: true);

            return 0;
        }

        private static void PublishFiles(IEnumerable<LibraryAsset> files, string outputPath, bool nativeSubdirectories)
        {
            foreach (var file in files)
            {
                var destinationDirectory = DetermineFileDestinationDirectory(file, outputPath, nativeSubdirectories);

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(file.ResolvedPath, Path.Combine(destinationDirectory, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }

        private static string DetermineFileDestinationDirectory(LibraryAsset file, string outputPath, bool nativeSubdirectories)
        {
            var destinationDirectory = outputPath;

            if (nativeSubdirectories)
            {
                destinationDirectory = Path.Combine(outputPath, GetNativeRelativeSubdirectory(file.RelativePath));
            }

            return destinationDirectory;
        }

        private static string GetNativeRelativeSubdirectory(string filepath)
        {
            string directoryPath = Path.GetDirectoryName(filepath);

            string[] parts = directoryPath.Split(new string[] { "native" }, 2, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                throw new Exception("Unrecognized Native Directory Format: " + filepath);
            }

            string candidate = parts[1];
            candidate = candidate.TrimStart(new char[] { '/', '\\' });

            return candidate;
        }

        private static IEnumerable<ProjectContext> SelectContexts(string projectPath, NuGetFramework framework, string runtime)
        {
            var allContexts = ProjectContext.CreateContextForEachTarget(projectPath);
            if (string.IsNullOrEmpty(runtime))
            {
                // Nothing was specified, so figure out what the candidate runtime identifiers are and try each of them
                // Temporary until #619 is resolved
                foreach (var candidate in PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers())
                {
                    var contexts = GetMatchingProjectContexts(allContexts, framework, candidate);
                    if (contexts.Any())
                    {
                        return contexts;
                    }
                }
                return Enumerable.Empty<ProjectContext>();
            }
            else
            {
                return GetMatchingProjectContexts(allContexts, framework, runtime);
            }
        }

        /// <summary>
        /// Return the matching framework/runtime ProjectContext.
        /// If 'framework' or 'runtimeIdentifier' is null or empty then it matches with any.
        /// </summary>
        private static IEnumerable<ProjectContext> GetMatchingProjectContexts(IEnumerable<ProjectContext> contexts, NuGetFramework framework, string runtimeIdentifier)
        {
            foreach (var context in contexts)
            {
                if (context.TargetFramework == null || string.IsNullOrEmpty(context.RuntimeIdentifier))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(runtimeIdentifier) || RIDEquals(runtimeIdentifier, context.RuntimeIdentifier))
                {
                    if (framework == null || framework.Equals(context.TargetFramework))
                    {
                        yield return context;
                    }
                }
            }
        }
    }
}
