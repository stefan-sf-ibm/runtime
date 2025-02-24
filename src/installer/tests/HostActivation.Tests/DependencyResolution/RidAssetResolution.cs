// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Build;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.DotNet.CoreSetup.Test.HostActivation.DependencyResolution
{
    public abstract class RidAssetResolutionBase : ComponentDependencyResolutionBase
    {
        protected SharedTestState SharedState { get; }

        protected RidAssetResolutionBase(SharedTestState sharedState)
        {
            SharedState = sharedState;
        }

        protected abstract void RunTest(
            Action<NetCoreAppBuilder.RuntimeLibraryBuilder> assetsCustomizer,
            string rid,
            string includedAssemblyPaths,
            string excludedAssemblyPaths,
            string includedNativeLibraryPaths,
            string excludedNativeLibraryPaths,
            Action<NetCoreAppBuilder> appCustomizer = null);

        protected const string UnknownRid = "unknown-rid";

        private const string LinuxAssembly = "linux/LinuxAssembly.dll";
        private const string MacOSAssembly = "osx/MacOSAssembly.dll";
        private const string WindowsAssembly = "win/WindowsAssembly.dll";

        [Theory]
        [InlineData("win", WindowsAssembly, $"{LinuxAssembly};{MacOSAssembly}")]
        [InlineData("win10-x64", WindowsAssembly, $"{LinuxAssembly};{MacOSAssembly}")]
        [InlineData("linux-x64", LinuxAssembly, $"{MacOSAssembly};{WindowsAssembly}")]
        [InlineData("osx-x64", MacOSAssembly, $"{LinuxAssembly};{WindowsAssembly}")]
        public void RidSpecificAssembly(string rid, string includedPath, string excludedPath)
        {
            RunTest(
                p => p
                    .WithAssemblyGroup("win", g => g.WithAsset(WindowsAssembly))
                    .WithAssemblyGroup("linux", g => g.WithAsset(LinuxAssembly))
                    .WithAssemblyGroup("osx", g => g.WithAsset(MacOSAssembly)),
                rid, includedPath, excludedPath, null, null);
        }

        [Theory]
        [InlineData(null)]          // RID is computed at run-time
        [InlineData(UnknownRid)]    // RID is from a compile-time fallback
        public void RidSpecificAssembly_CurrentRid(string rid)
        {
            string includedPath = null;
            string excludedPath = null;

            // Host should resolve to the RID corresponding to the platform on which it is running
            if (OperatingSystem.IsLinux())
            {
                includedPath = LinuxAssembly;
                excludedPath = $"{MacOSAssembly};{WindowsAssembly}";
            }
            else if (OperatingSystem.IsMacOS())
            {
                includedPath = MacOSAssembly;
                excludedPath = $"{LinuxAssembly};{WindowsAssembly}";
            }
            else if (OperatingSystem.IsWindows())
            {
                includedPath = WindowsAssembly;
                excludedPath = $"{LinuxAssembly};{MacOSAssembly}";
            }
            else
            {
                includedPath = null;
                excludedPath = $"{LinuxAssembly};{MacOSAssembly};{WindowsAssembly}";
            }

            RidSpecificAssembly(rid, includedPath, excludedPath);
        }

        [Theory]
        [InlineData("win", "win", "linux;osx")]
        [InlineData("win10-x64", "win", "linux;osx")]
        [InlineData("linux-x64", "linux", "osx;win")]
        [InlineData("osx-x64", "osx", "linux;win")]
        public void RidSpecificNativeLibrary(string rid, string includedPath, string excludedPath)
        {
            RunTest(
                p => p
                    .WithNativeLibraryGroup("win", g => g.WithAsset("win/WindowsNativeLibrary.dll"))
                    .WithNativeLibraryGroup("linux", g => g.WithAsset("linux/LinuxNativeLibrary.so"))
                    .WithNativeLibraryGroup("osx", g => g.WithAsset("osx/MacOSNativeLibrary.dylib")),
                rid, null, null, includedPath, excludedPath);
        }

        [Theory]
        [InlineData(null)]          // RID is computed at run-time
        [InlineData(UnknownRid)]    // RID is from a compile-time fallback
        public void RidSpecificNativeLibrary_CurrentRid(string rid)
        {
            string includedPath;
            string excludedPath;

            // Host should resolve to the RID corresponding to the platform on which it is running
            if (OperatingSystem.IsLinux())
            {
                includedPath = "linux";
                excludedPath = "osx;win";
            }
            else if (OperatingSystem.IsMacOS())
            {
                includedPath = "osx";
                excludedPath = "linux;win";
            }
            else if (OperatingSystem.IsWindows())
            {
                includedPath = "win";
                excludedPath = "linux;osx";
            }
            else
            {
                includedPath = null;
                excludedPath = "linux;osx;win";
            }

            RidSpecificNativeLibrary("unknown-rid", includedPath, excludedPath);
        }

        [Theory]
        [InlineData("win10-x64", "win-x64/ManagedWin64.dll")]
        [InlineData("win10-x86", "win/ManagedWin.dll")]
        [InlineData("linux-x64", "any/ManagedAny.dll")]
        public void MostSpecificRidAssemblySelected(string rid, string expectedPath)
        {
            RunTest(
                p => p
                    .WithAssemblyGroup("any", g => g.WithAsset("any/ManagedAny.dll"))
                    .WithAssemblyGroup("win", g => g.WithAsset("win/ManagedWin.dll"))
                    .WithAssemblyGroup("win-x64", g => g.WithAsset("win-x64/ManagedWin64.dll")),
                rid, expectedPath, null, null, null);
        }

        [Theory]
        [InlineData("win10-x64", "win-x64")]
        [InlineData("win10-x86", "win")]
        [InlineData("linux-x64", "any")]
        public void MostSpecificRidNativeLibrarySelected(string rid, string expectedPath)
        {
            RunTest(
                p => p
                    .WithNativeLibraryGroup("any", g => g.WithAsset("any/NativeAny.dll"))
                    .WithNativeLibraryGroup("win", g => g.WithAsset("win/NativeWin.dll"))
                    .WithNativeLibraryGroup("win-x64", g => g.WithAsset("win-x64/NativeWin64.dll")),
                rid, null, null, expectedPath, null);
        }

        [Theory]
        [InlineData("win10-x64", "win/ManagedWin.dll", "native/win-x64")]
        [InlineData("win10-x86", "win/ManagedWin.dll", "native/win-x86")]
        [InlineData("linux-x64", "any/ManagedAny.dll", "native/linux")]
        public void MostSpecificRidAssemblySelectedPerType(string rid, string expectedAssemblyPath, string expectedNativePath)
        {
            RunTest(
                p => p
                    .WithAssemblyGroup("any", g => g.WithAsset("any/ManagedAny.dll"))
                    .WithAssemblyGroup("win", g => g.WithAsset("win/ManagedWin.dll"))
                    .WithNativeLibraryGroup("win-x64", g => g.WithAsset("native/win-x64/n.dll"))
                    .WithNativeLibraryGroup("win-x86", g => g.WithAsset("native/win-x86/n.dll"))
                    .WithNativeLibraryGroup("linux", g => g.WithAsset("native/linux/n.so")),
                rid, expectedAssemblyPath, null, expectedNativePath, null);
        }

        [Theory]
        // For "win" RIDs the DependencyLib which is RID-agnostic will not be included,
        // since there are other assembly (runtime) assets with more specific RID match.
        [InlineData("win10-x64", "win/ManagedWin.dll;win/AnotherWin.dll", "native/win10-x64;native/win10-x64-2")]
        [InlineData("win10-x86", "win/ManagedWin.dll;win/AnotherWin.dll", "native/win-x86")]
        // For "linux" on the other hand the DependencyLib will be resolved because there are
        // no RID-specific assembly assets available.
        [InlineData("linux-x64", "", "native/linux")]
        public void MostSpecificRidAssemblySelectedPerTypeMultipleAssets(string rid, string expectedAssemblyPath, string expectedNativePath)
        {
            // Skip the component on self-contained app case as that won't work and our simple checks will be broken
            // in this complex test case (the PortableLib and PortableLib2 will always resolve, even in this broken case).
            if (GetType() == typeof(PortableComponentOnSelfContainedAppRidAssetResolution))
            {
                return;
            }

            RunTest(
                assetsCustomizer: null,
                appCustomizer: b => b
                    .WithPackage("ridSpecificLib", "1.0.0", p => p
                        .WithAssemblyGroup(null, g => g.WithAsset("DependencyLib.dll"))
                        .WithAssemblyGroup("win", g => g.WithAsset("win/ManagedWin.dll"))
                        .WithAssemblyGroup("win", g => g.WithAsset("win/AnotherWin.dll"))
                        .WithNativeLibraryGroup("win10-x64", g => g.WithAsset("native/win10-x64/n1.dll"))
                        .WithNativeLibraryGroup("win10-x64", g => g.WithAsset("native/win10-x64/n2.dll"))
                        .WithNativeLibraryGroup("win10-x64", g => g.WithAsset("native/win10-x64-2/n3.dll"))
                        .WithNativeLibraryGroup("win-x86", g => g.WithAsset("native/win-x86/n1.dll"))
                        .WithNativeLibraryGroup("win-x86", g => g.WithAsset("native/win-x86/n2.dll"))
                        .WithNativeLibraryGroup("linux", g => g.WithAsset("native/linux/n.so")))
                    .WithPackage("ridAgnosticLib", "2.0.0", p => p
                        .WithAssemblyGroup(null, g => g.WithAsset("PortableLib.dll").WithAsset("PortableLib2.dll"))),
                rid: rid,
                // The PortableLib an PortableLib2 are from a separate package which has no RID specific assets,
                // so the RID-agnostic assets are always included
                includedAssemblyPaths: expectedAssemblyPath + ";PortableLib.dll;PortableLib2.dll", excludedAssemblyPaths: null,
                includedNativeLibraryPaths: expectedNativePath, excludedNativeLibraryPaths: null);
        }

        public class SharedTestState : ComponentSharedTestStateBase
        {
            public DotNetCli DotNetWithNetCoreApp_RuntimeFallbacks { get; }

            public SharedTestState() : base()
            {
                DotNetWithNetCoreApp_RuntimeFallbacks = DotNet("WithNetCoreApp_RuntimeFallbacks")
                    .AddMicrosoftNETCoreAppFrameworkMockCoreClr("4.0.0", UseFallbacksFromBuiltDotNet)
                    .Build();
            }

            protected void UseFallbacksFromBuiltDotNet(NetCoreAppBuilder builder)
            {
                IReadOnlyList<RuntimeFallbacks> fallbacks;
                string depsJson = Path.Combine(new DotNetCli(BuiltDotnetPath).GreatestVersionSharedFxPath, $"{Constants.MicrosoftNETCoreApp}.deps.json");
                using (FileStream fileStream = File.Open(depsJson, FileMode.Open))
                using (DependencyContextJsonReader reader = new DependencyContextJsonReader())
                {
                    fallbacks = reader.Read(fileStream).RuntimeGraph;
                }

                builder.RuntimeFallbacks.Clear();
                foreach (RuntimeFallbacks fallback in fallbacks)
                {
                    builder.WithRuntimeFallbacks(fallback.Runtime, fallback.Fallbacks.ToArray());
                }
            }
        }
    }

    // Run the tests on a framework dependent app
    public class PortableAppRidAssetResolution :
        RidAssetResolutionBase,
        IClassFixture<RidAssetResolutionBase.SharedTestState>
    {
        public PortableAppRidAssetResolution(SharedTestState sharedState)
            : base(sharedState)
        {
        }

        protected override void RunTest(
            Action<NetCoreAppBuilder.RuntimeLibraryBuilder> assetsCustomizer,
            string rid,
            string includedAssemblyPaths,
            string excludedAssemblyPaths,
            string includedNativeLibraryPaths,
            string excludedNativeLibraryPaths,
            Action<NetCoreAppBuilder> appCustomizer)
        {
            using (TestApp app = NetCoreAppBuilder.PortableForNETCoreApp(SharedState.FrameworkReferenceApp)
                .WithProject(p => { p.WithAssemblyGroup(null, g => g.WithMainAssembly()); assetsCustomizer?.Invoke(p); })
                .WithCustomizer(appCustomizer)
                .Build())
            {
                // Use the fallbacks from the product when testing the computed RID
                DotNetCli dotnet = rid == null ? SharedState.DotNetWithNetCoreApp_RuntimeFallbacks : SharedState.DotNetWithNetCoreApp;
                dotnet.Exec(app.AppDll)
                    .EnableTracingAndCaptureOutputs()
                    .RuntimeId(rid)
                    .Execute()
                    .Should().Pass()
                    .And.HaveResolvedAssembly(includedAssemblyPaths, app)
                    .And.NotHaveResolvedAssembly(excludedAssemblyPaths, app)
                    .And.HaveResolvedNativeLibraryPath(includedNativeLibraryPaths, app)
                    .And.NotHaveResolvedNativeLibraryPath(excludedNativeLibraryPaths, app)
                    .And.HaveUsedFallbackRid(rid == UnknownRid);
            }
        }
    }

    // Run the tests on a portable component hosted by a framework dependent app
    public class PortableComponentOnFrameworkDependentAppRidAssetResolution :
        RidAssetResolutionBase,
        IClassFixture<RidAssetResolutionBase.SharedTestState>
    {
        public PortableComponentOnFrameworkDependentAppRidAssetResolution(SharedTestState sharedState)
            : base(sharedState)
        {
        }

        protected override void RunTest(
            Action<NetCoreAppBuilder.RuntimeLibraryBuilder> assetsCustomizer,
            string rid,
            string includedAssemblyPaths,
            string excludedAssemblyPaths,
            string includedNativeLibraryPaths,
            string excludedNativeLibraryPaths,
            Action<NetCoreAppBuilder> appCustomizer)
        {
            var component = SharedState.CreateComponentWithNoDependencies(b => b
                .WithPackage("NativeDependency", "1.0.0", p => assetsCustomizer?.Invoke(p))
                .WithCustomizer(appCustomizer));

            // Use the fallbacks from the product when testing the computed RID
            DotNetCli dotnet = rid == null ? SharedState.DotNetWithNetCoreApp_RuntimeFallbacks : SharedState.DotNetWithNetCoreApp;
            SharedState.RunComponentResolutionTest(component.AppDll, SharedState.FrameworkReferenceApp, dotnet.GreatestVersionHostFxrPath, command => command
                .RuntimeId(rid))
                .Should().Pass()
                .And.HaveSuccessfullyResolvedComponentDependencies()
                .And.HaveResolvedComponentDependencyAssembly(includedAssemblyPaths, component)
                .And.NotHaveResolvedComponentDependencyAssembly(excludedAssemblyPaths, component)
                .And.HaveResolvedComponentDependencyNativeLibraryPath(includedNativeLibraryPaths, component)
                .And.NotHaveResolvedComponentDependencyNativeLibraryPath(excludedNativeLibraryPaths, component)
                .And.HaveUsedFallbackRid(rid == UnknownRid);
        }
    }

    // Run the tests on a portable component hosted by a self-contained app
    // This is testing the currently shipping scenario where SDK does not generate RID fallback graph for self-contained apps
    public class PortableComponentOnSelfContainedAppRidAssetResolution :
        RidAssetResolutionBase,
        IClassFixture<PortableComponentOnSelfContainedAppRidAssetResolution.ComponentSharedTestState>
    {
        private ComponentSharedTestState ComponentSharedState { get; }

        public PortableComponentOnSelfContainedAppRidAssetResolution(ComponentSharedTestState sharedState)
            : base(sharedState)
        {
            ComponentSharedState = sharedState;
        }

        protected override void RunTest(
            Action<NetCoreAppBuilder.RuntimeLibraryBuilder> assetsCustomizer,
            string rid,
            string includedAssemblyPaths,
            string excludedAssemblyPaths,
            string includedNativeLibraryPaths,
            string excludedNativeLibraryPaths,
            Action<NetCoreAppBuilder> appCustomizer)
        {
            var component = SharedState.CreateComponentWithNoDependencies(b => b
                .WithPackage("NativeDependency", "1.0.0", p => assetsCustomizer?.Invoke(p))
                .WithCustomizer(appCustomizer));

            string assemblyPaths = includedAssemblyPaths ?? "";
            if (excludedAssemblyPaths != null)
            {
                assemblyPaths = assemblyPaths.Length == 0 ? (";" + excludedAssemblyPaths) : excludedAssemblyPaths;
            }

            string nativeLibrarypaths = includedNativeLibraryPaths ?? "";
            if (excludedNativeLibraryPaths != null)
            {
                nativeLibrarypaths = nativeLibrarypaths.Length == 0 ? (";" + excludedNativeLibraryPaths) : excludedNativeLibraryPaths;
            }

            // Self-contained apps don't have any RID fallback graph, so currently there's no way to resolve native dependencies
            // from portable components - as we have no way of knowing how to follow RID fallback logic.
            SharedState.RunComponentResolutionTest(component.AppDll, ComponentSharedState.HostApp, ComponentSharedState.HostApp.Location, command => command
                .RuntimeId(rid))
                .Should().Pass()
                .And.HaveSuccessfullyResolvedComponentDependencies()
                .And.NotHaveResolvedComponentDependencyAssembly(assemblyPaths, component)
                .And.NotHaveResolvedComponentDependencyNativeLibraryPath(nativeLibrarypaths, component)
                .And.HaveUsedFallbackRid(true);
        }

        public class ComponentSharedTestState : SharedTestState
        {
            public TestApp HostApp { get; }

            public ComponentSharedTestState()
            {
                HostApp = CreateSelfContainedAppWithMockCoreClr("ComponentHostSelfContainedApp");
            }
        }
    }

    // Run the tests on a portable component hosted by a self-contained app which does have a RID fallback graph
    // This is testing the scenario after SDK starts generating RID fallback graph even for self-contained apps
    //   - https://github.com/dotnet/sdk/issues/3361
    public class PortableComponentOnSelfContainedAppRidAssetResolutionWithRidFallbackGraph :
        RidAssetResolutionBase,
        IClassFixture<PortableComponentOnSelfContainedAppRidAssetResolutionWithRidFallbackGraph.ComponentSharedTestState>
    {
        private ComponentSharedTestState ComponentSharedState { get; }

        public PortableComponentOnSelfContainedAppRidAssetResolutionWithRidFallbackGraph(ComponentSharedTestState sharedState)
            : base(sharedState)
        {
            ComponentSharedState = sharedState;
        }

        protected override void RunTest(
            Action<NetCoreAppBuilder.RuntimeLibraryBuilder> assetsCustomizer,
            string rid,
            string includedAssemblyPaths,
            string excludedAssemblyPaths,
            string includedNativeLibraryPaths,
            string excludedNativeLibraryPaths,
            Action<NetCoreAppBuilder> appCustomizer)
        {
            var component = SharedState.CreateComponentWithNoDependencies(b => b
                .WithPackage("NativeDependency", "1.0.0", p => assetsCustomizer?.Invoke(p))
                .WithCustomizer(appCustomizer));

            // Use the fallbacks from the product when testing the computed RID
            TestApp app = rid == null ? ComponentSharedState.HostApp_RuntimeFallbacks : ComponentSharedState.HostApp;
            SharedState.RunComponentResolutionTest(component.AppDll, app, app.Location, command => command
                .RuntimeId(rid))
                .Should().Pass()
                .And.HaveSuccessfullyResolvedComponentDependencies()
                .And.HaveResolvedComponentDependencyAssembly(includedAssemblyPaths, component)
                .And.NotHaveResolvedComponentDependencyAssembly(excludedAssemblyPaths, component)
                .And.HaveResolvedComponentDependencyNativeLibraryPath(includedNativeLibraryPaths, component)
                .And.NotHaveResolvedComponentDependencyNativeLibraryPath(excludedNativeLibraryPaths, component)
                .And.HaveUsedFallbackRid(rid == UnknownRid);
        }

        public class ComponentSharedTestState : SharedTestState
        {
            public TestApp HostApp { get; }
            public TestApp HostApp_RuntimeFallbacks { get; }

            public ComponentSharedTestState()
            {
                HostApp = CreateSelfContainedAppWithMockCoreClr(
                    "ComponentHostSelfContainedApp",
                    b => b.WithStandardRuntimeFallbacks());

                HostApp_RuntimeFallbacks = CreateSelfContainedAppWithMockCoreClr(
                    "ComponentHostSelfContainedApp_RuntimeFallbacks",
                    UseFallbacksFromBuiltDotNet);
            }
        }
    }
}
