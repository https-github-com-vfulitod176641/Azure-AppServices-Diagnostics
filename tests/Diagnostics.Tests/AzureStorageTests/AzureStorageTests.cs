﻿using System;
using Xunit;
using Diagnostics.RuntimeHost.Services.CacheService;
using Diagnostics.RuntimeHost.Services.CacheService.Interfaces;
using Microsoft.Extensions.Configuration;
using Diagnostics.RuntimeHost.Services.StorageService;
using Microsoft.AspNetCore.Hosting;
using Diagnostics.ModelsAndUtils.Models.Storage;
using Diagnostics.RuntimeHost.Models;
using Diagnostics.ModelsAndUtils.Models;
using System.Threading;
using RimDev.Automation.StorageEmulator;
using System.Linq;
using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.Tests.Helpers;
using Diagnostics.Scripts.Models;
using Diagnostics.Scripts.CompilationService;
using Diagnostics.Scripts.CompilationService.Interfaces;
using Microsoft.CodeAnalysis.Scripting;
using Octokit;
using Diagnostics.RuntimeHost.Utilities;
using System.Reflection;
using Diagnostics.Scripts;
using System.IO;
using Diagnostics.ModelsAndUtils.ScriptUtilities;

namespace Diagnostics.Tests.AzureStorageTests
{
    public class AzureStorageTests: IDisposable
    {

        IStorageService storageService;

        IDiagEntityTableCacheService tableCacheService;

        IConfiguration configuration;

        IHostingEnvironment environment;

        AzureStorageEmulatorAutomation emulator;

        public AzureStorageTests()
        {
            StartStorageEmulator();
            configuration = InitConfig();
            environment = new MockHostingEnvironment();
            environment.EnvironmentName = "UnitTest";
            if(string.IsNullOrWhiteSpace(configuration["SourceWatcher:TableName"]))
            {
                configuration["SourceWatcher:TableName"] = "diagentities";
            }
            if (string.IsNullOrWhiteSpace(configuration["SourceWatcher:BlobContainerName"]))
            {
                configuration["SourceWatcher:BlobContainerName"] = "detectors";
            }
            storageService = new StorageService(configuration, environment);
            tableCacheService = new DiagEntityTableCacheService(storageService);
        }

        private void StartStorageEmulator()
        {
            emulator = new AzureStorageEmulatorAutomation();
            emulator.Init();
            emulator.Start();
        }

        public void Dispose()
        {
            emulator.Stop();
        }

        private IConfiguration InitConfig()
        {
             var builder = new ConfigurationBuilder()
                    .AddEnvironmentVariables();
            return builder.Build();
        }

        private bool CheckProcessRunning(int maxAttempts)
        {
            bool isRunning = AzureStorageEmulatorAutomation.IsEmulatorRunning();
            int currentAttempt = 0;
            while (!isRunning && currentAttempt <= maxAttempts)
            {
                currentAttempt++;
                // Wait for 15s and then try 
                Thread.Sleep(15 * 1000);          
                isRunning = AzureStorageEmulatorAutomation.IsEmulatorRunning();
            }
            return isRunning;
        }

        [Fact]
        /// <summary>
        /// Tests load entity and insert entity
        /// </summary>
        public async void TestTableOperations()
        {
            // First check if emulator is running before proceeding. 
            bool isEmulatorRunning = CheckProcessRunning(4);
            if(isEmulatorRunning)
            {
                // Generate fake entity;
                var diagEntity = new DiagEntity
                {
                    PartitionKey = "Detector",
                    RowKey = "xyz",
                    GithubLastModified = DateTime.UtcNow
                };
                var insertResult = await storageService.LoadDataToTable(diagEntity);
                Assert.NotNull(insertResult);
                var retrieveResult = await storageService.GetEntitiesByPartitionkey("Detector");
                Assert.NotNull(retrieveResult);
                Assert.NotEmpty(retrieveResult);
                var gistResult = await storageService.GetEntitiesByPartitionkey("Gist");
                Assert.Empty(gistResult);
            }          
        }

        [Fact]
        /// <summary>
        /// Test if entites are retrieved according to runtime context.
        /// </summary>
        public async void TestCacheOperations()
        {

            // First check if emulator is running before proceeding. 
            bool isEmulatorRunning = CheckProcessRunning(4);
            if(isEmulatorRunning)
            {
                var windowsDiagEntity = new DiagEntity
                {
                    PartitionKey = "Detector",
                    RowKey = "webappDown",
                    GithubLastModified = DateTime.UtcNow,
                    PlatForm = "Windows",
                    ResourceProvider = "Microsoft.Web",
                    ResourceType = "sites",
                    StackType = "AspNet,NetCore",
                    AppType = "WebApp",
                    DetectorType = "Detector"
                };


                var insertResult = await storageService.LoadDataToTable(windowsDiagEntity);

                // Test Analysis detectors

                var appDownAnalysisEntity = new DiagEntity
                {
                    PartitionKey = "Detector",
                    RowKey = "appDownAnalysis",
                    GithubLastModified = DateTime.UtcNow,
                    PlatForm = "Windows",
                    ResourceProvider = "Microsoft.Web",
                    ResourceType = "sites",
                    StackType = "AspNet,NetCore",
                    AppType = "WebApp",
                    DetectorType = "Analysis"
                };

                insertResult = await storageService.LoadDataToTable(appDownAnalysisEntity);
                var webApp = new App("72383ac7-d6f4-4a5e-bf56-b172f2fdafb2", "resourcegp-default", "diag-test");
                var operationContext = new OperationContext<App>(
                                         webApp,
                                         DateTime.Now.ToString(),
                                         DateTime.Now.AddHours(1).ToString(),
                                         true,
                                         new Guid().ToString()
                                        );
                var context = new RuntimeContext<App>(configuration);
                context.OperationContext = operationContext;

                var detectorsForWebApps = await tableCacheService.GetEntityListByType(context, "Detector");
                Assert.NotNull(detectorsForWebApps);
                Assert.NotEmpty(detectorsForWebApps);
                Assert.NotEmpty(detectorsForWebApps.Where(s => s.DetectorType != null && s.DetectorType.Equals("Analysis", StringComparison.CurrentCultureIgnoreCase)));

                var logicApp = new LogicApp("72383ac7-d6f4-4a5e-bf56-b172f2fdafb2", "resourcegp-default", "la-test");
                var logicAppOperContext = new OperationContext<LogicApp>(logicApp,
                                                 DateTime.Now.ToString(),
                                                 DateTime.Now.AddHours(1).ToString(),
                                                 true,
                                                 new Guid().ToString());

                var runtimeContextLogicApp = new RuntimeContext<LogicApp>(configuration);
                runtimeContextLogicApp.OperationContext = logicAppOperContext;

                var detectorsForLogicApps = await tableCacheService.GetEntityListByType(runtimeContextLogicApp, "Detector");
                Assert.NotNull(detectorsForLogicApps);
                Assert.Empty(detectorsForLogicApps);            
                

            }    
        }

        [Fact]
        /// <summary>
        /// Test blob upload operation
        /// </summary>
        public async void TestBlobOperations()
        {
            // First check if emulator is running before proceeding. 
            bool isEmulatorRunning = CheckProcessRunning(4);
            if (isEmulatorRunning)
            {
                // Test .dll blob upload 

                Definition definitonAttribute = new Definition()
                {
                    Id = "blobDetector"
                };

                EntityMetadata metadata = ScriptTestDataHelper.GetRandomMetadata();
                metadata.ScriptText = await File.ReadAllTextAsync("blobDetector.csx");
                var scriptOptions = ScriptTestDataHelper.GetScriptOption(ScriptHelper.GetFrameworkReferences(), ScriptHelper.GetFrameworkImports());
                var serviceInstance = CompilationServiceFactory.CreateService(metadata, scriptOptions);
                ICompilation compilation = await serviceInstance.GetCompilationAsync();

                var assemblyBytes = await compilation.GetAssemblyBytesAsync();
                var blobName = $"{definitonAttribute.Id}/{definitonAttribute.Id}.dll";
                var etagdetector = await storageService.LoadBlobToContainer(blobName, assemblyBytes.Item1);
                Assert.NotNull(etagdetector);
                var assemblyData = await storageService.GetBlobByName(blobName);
                Assert.NotNull(assemblyData);

                // Now test initializing Entry Point of Invoker using assembly
                Assembly temp = Assembly.Load(assemblyData);
                using (EntityInvoker invoker = new EntityInvoker(metadata))
                {
                    Exception ex = Record.Exception(() =>
                    {
                        invoker.InitializeEntryPoint(temp);
                    });

                    Assert.Null(ex);
                    Assert.True(invoker.IsCompilationSuccessful);
                    Assert.Equal(definitonAttribute.Id, invoker.EntryPointDefinitionAttribute.Id);
                }
            }
        }
     }       
}
