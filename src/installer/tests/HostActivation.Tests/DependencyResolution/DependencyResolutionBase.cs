// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.CoreSetup.Test.HostActivation.DependencyResolution
{
    public abstract class DependencyResolutionBase
    {
        protected const string MicrosoftNETCoreApp = "Microsoft.NETCore.App";

        public abstract class SharedTestStateBase : TestArtifact
        {
            protected string BuiltDotnetPath { get; }

            public RepoDirectoriesProvider RepoDirectories { get; }

            private static string GetBaseDir(string name)
            {
                string baseDir = Path.Combine(TestArtifactsPath, name);
                return SharedFramework.CalculateUniqueTestDirectory(baseDir);
            }

            public SharedTestStateBase()
                : base(GetBaseDir("dependencyResolution"))
            {
                BuiltDotnetPath = Path.Combine(TestArtifactsPath, "sharedFrameworkPublish");
                RepoDirectories = new RepoDirectoriesProvider(builtDotnet: BuiltDotnetPath);
            }

            public DotNetBuilder DotNet(string name)
            {
                return new DotNetBuilder(Location, BuiltDotnetPath, name);
            }

            public TestApp CreateFrameworkReferenceApp(string fxName, string fxVersion)
            {
                // Prepare the app mock - we're not going to run anything really, so we just need the basic files
                TestApp testApp = CreateTestApp(Location, "FrameworkReferenceApp");
                testApp.PopulateFrameworkDependent(fxName, fxVersion);
                return testApp;
            }

            protected TestApp CreateTestApp(string location, string name)
            {
                TestApp testApp;
                if (location == null)
                {
                    testApp = TestApp.CreateEmpty(name);
                }
                else
                {
                    string path = Path.Combine(location, name);
                    testApp = new TestApp(path);
                }

                RegisterCopy(testApp);
                return testApp;
            }

            public TestApp CreateComponentWithNoDependencies(Action<NetCoreAppBuilder> customizer = null, string location = null)
            {
                TestApp componentWithNoDependencies = CreateTestApp(location, "ComponentWithNoDependencies");
                NetCoreAppBuilder builder = NetCoreAppBuilder.PortableForNETCoreApp(componentWithNoDependencies)
                    .WithProject(p => p.WithAssemblyGroup(null, g => g.WithMainAssembly()));
                customizer?.Invoke(builder);

                return builder.Build(componentWithNoDependencies);
            }

            public TestApp CreateSelfContainedAppWithMockCoreClr(string name, Action<NetCoreAppBuilder> customizer = null)
            {
                TestApp testApp = CreateTestApp(null, name);
                testApp.PopulateSelfContained(TestApp.MockedComponent.CoreClr, customizer);
                return testApp;
            }
        }
    }
}
