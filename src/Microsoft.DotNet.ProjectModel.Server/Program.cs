﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ProjectModel.Server
{
    public class Program
    {
        private readonly Dictionary<int, ProjectContextManager> _projectContextManagers;
        private readonly WorkspaceContext _workspaceContext;
        private readonly ProtocolManager _protocolManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _hostName;
        private readonly int _port;
        private Socket _listenSocket;

        public Program(int intPort, string hostName, ILoggerFactory loggerFactory)
        {
            _port = intPort;
            _hostName = hostName;
            _loggerFactory = loggerFactory;
            _protocolManager = new ProtocolManager(maxVersion: 4, loggerFactory: _loggerFactory);
            _workspaceContext = WorkspaceContext.Create();
            _projectContextManagers = new Dictionary<int, ProjectContextManager>();
        }

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet-projectmodel-server";
            app.Description = ".NET Project Model Server";
            app.FullName = ".NET Design Time Server";
            app.Description = ".NET Design Time Server";
            app.HelpOption("-?|-h|--help");

            var verbose = app.Option("--verbose", "Verbose ouput", CommandOptionType.NoValue);
            var hostpid = app.Option("--hostPid", "The process id of the host", CommandOptionType.SingleValue);
            var hostname = app.Option("--hostName", "The name of the host", CommandOptionType.SingleValue);
            var port = app.Option("--port", "The TCP port used for communication", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                var loggerFactory = new LoggerFactory();
                loggerFactory.AddConsole(verbose.HasValue() ? LogLevel.Debug : LogLevel.Information);

                var logger = loggerFactory.CreateLogger<Program>();

                if (!MonitorHostProcess(hostpid, logger))
                {
                    return 1;
                }

                var intPort = CheckPort(port, logger);
                if (intPort == -1)
                {
                    return 1;
                }

                if (!hostname.HasValue())
                {
                    logger.LogError($"Option \"{hostname.LongName}\" is missing.");
                    return 1;
                }

                var program = new Program(intPort, hostname.Value(), loggerFactory);
                program.OpenChannel();

                return 0;
            });

            return app.Execute(args);
        }

        public void OpenChannel()
        {
            var logger = _loggerFactory.CreateLogger($"OpenChannel");

            // This fixes the mono incompatibility but ties it to ipv4 connections
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, _port));
            _listenSocket.Listen(10);

            logger.LogInformation($"Process ID {Process.GetCurrentProcess().Id}");
            logger.LogInformation($"Listening on port {_port}");

            while (true)
            {
                var acceptSocket = _listenSocket.Accept();
                logger.LogInformation($"Client accepted {acceptSocket.LocalEndPoint}");

                var connection = new ConnectionContext(acceptSocket,
                                                       _hostName,
                                                       _protocolManager,
                                                       _workspaceContext,
                                                       _projectContextManagers,
                                                       _loggerFactory);

                connection.QueueStart();
            }
        }

        public void Shutdown()
        {
            if (_listenSocket.Connected)
            {
                _listenSocket.Shutdown(SocketShutdown.Both);
            }
        }

        private static int CheckPort(CommandOption port, ILogger logger)
        {
            if (!port.HasValue())
            {
                logger.LogError($"Option \"{port.LongName}\" is missing.");
            }

            int result;
            if (int.TryParse(port.Value(), out result))
            {
                return result;
            }
            else
            {
                logger.LogError($"Option \"{port.LongName}\" is not a valid Int32 value.");
                return -1;
            }
        }

        private static bool MonitorHostProcess(CommandOption host, ILogger logger)
        {
            if (!host.HasValue())
            {
                logger.LogError($"Option \"{host.LongName}\" is missing.");
                return false;
            }

            int hostPID;
            if (int.TryParse(host.Value(), out hostPID))
            {
                var hostProcess = Process.GetProcessById(hostPID);
                hostProcess.EnableRaisingEvents = true;
                hostProcess.Exited += (s, e) =>
                {
                    Process.GetCurrentProcess().Kill();
                };

                logger.LogDebug($"Server will exit when process {hostPID} exits.");
                return true;
            }
            else
            {
                logger.LogError($"Option \"{host.LongName}\" is not a valid Int32 value.");
                return false;
            }
        }
    }
}
