﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Components.PackageManager.Model;
using Microsoft.R.Components.Test.Fakes.InteractiveWindow;
using Microsoft.R.Host.Client;
using Microsoft.R.Host.Client.Test.Script;
using Microsoft.R.Support.Settings;
using Microsoft.UnitTests.Core.Threading;
using Microsoft.UnitTests.Core.XUnit;
using Xunit;

namespace Microsoft.R.Components.Test.PackageManager {
    public class PackageManagerIntegrationTest : IDisposable, IAsyncLifetime {
        private readonly ExportProvider _exportProvider;
        private readonly TestRInteractiveWorkflowProvider _workflowProvider;
        private readonly MethodInfo _testMethod;
        private readonly TestFilesFixture _testFiles;
        private readonly string _repo1Path;
        private readonly string _libPath;
        private readonly string _lib2Path;
        private IRInteractiveWorkflow _workflow;

        public PackageManagerIntegrationTest(RComponentsMefCatalogFixture catalog, TestMethodFixture testMethod, TestFilesFixture testFiles) {
            _exportProvider = catalog.CreateExportProvider();
            _workflowProvider = _exportProvider.GetExportedValue<TestRInteractiveWorkflowProvider>();
            _testMethod = testMethod.MethodInfo;
            _testFiles = testFiles;
            _workflowProvider.HostClientApp = new RHostClientTestApp();
            _repo1Path = _testFiles.GetDestinationPath(Path.Combine("Repos", TestRepositories.Repo1));
            _libPath = Path.Combine(_testFiles.GetDestinationPath("library"), _testMethod.Name);
            _lib2Path = Path.Combine(_testFiles.GetDestinationPath("library2"), _testMethod.Name);
            Directory.CreateDirectory(_libPath);
            Directory.CreateDirectory(_lib2Path);
        }

        [Test]
        [Category.PackageManager]
        public async Task AvailablePackagesCranRepo() {
            var result = await _workflow.Packages.GetAvailablePackagesAsync();

            // Since this is coming from an internet repo where we don't control the data,
            // we only test a few of the values that are less likely to change over time.
            var abcPkg = result.Should().ContainSingle(pkg => pkg.Package == "abc").Which;
            abcPkg.Version.Length.Should().BeGreaterOrEqualTo(0);
            abcPkg.Depends.Should().Contain("abc.data");
            abcPkg.License.Should().Contain("GPL");
            abcPkg.NeedsCompilation.Should().Be("no");
        }

        [Test]
        [Category.PackageManager]
        public async Task AvailablePackagesLocalRepo() {
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalRepoAsync(eval, _repo1Path);
            }

            var result = await _workflow.Packages.GetAvailablePackagesAsync();
            result.Should().HaveCount(3);

            var rtvslib1Expected = TestPackages.RtvsLib1.Clone();
            rtvslib1Expected.Title = null;
            rtvslib1Expected.Built = null;
            rtvslib1Expected.Author = null;
            rtvslib1Expected.Repository = $"file:///{_repo1Path.ToRPath()}/src/contrib";

            var rtvslib1Actual = result.SingleOrDefault(pkg => pkg.Package == TestPackages.RtvsLib1Description.Package);
            rtvslib1Actual.ShouldBeEquivalentTo(rtvslib1Expected);
        }

        [Test]
        [Category.PackageManager]
        public async Task AdditionalFieldsCranRepo() {
            var all = await _workflow.Packages.GetAvailablePackagesAsync();
            var repository = all.FirstOrDefault(pkg => pkg.Package == "ggplot2")?.Repository;

            var actual = await _workflow.Packages.GetAdditionalPackageInfoAsync("ggplot2", repository);

            // This additional data is retrieved from a live web site.  When that data changes in the future,
            // this test may start failing.  Update the assertions below as needed, or relax them.
            actual.Package.Should().Be("ggplot2");
            actual.Title.Should().Be("An Implementation of the Grammar of Graphics");
            actual.Description.Should().StartWith("An implementation of the grammar of graphics in R. It combines");
            actual.Published.Should().NotBeEmpty();
            actual.Depends.Should().NotBeEmpty();
            actual.Suggests.Should().NotBeEmpty();
            actual.Imports.Should().NotBeEmpty();
            actual.Enhances.Should().NotBeEmpty();
            actual.Author.Should().NotBeEmpty();
            actual.Maintainer.Should().NotBeEmpty();
            actual.Version.Should().NotBeEmpty();
            actual.URL.Should().Be("http://ggplot2.org, https://github.com/hadley/ggplot2");
            actual.BugReports.Should().Be("https://github.com/hadley/ggplot2/issues");
            actual.License.Should().Be("GPL-2");
            actual.NeedsCompilation.Should().Be("no");
        }

        [Test]
        [Category.PackageManager]
        public async Task AdditionalFieldsLocalRepo() {
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalRepoAsync(eval, _repo1Path);
            }

            var all = await _workflow.Packages.GetAvailablePackagesAsync();
            var actual = all.SingleOrDefault(pkg => pkg.Package == TestPackages.RtvsLib1Description.Package);

            await _workflow.Packages.AddAdditionalPackageInfoAsync(actual);

            var expected = TestPackages.RtvsLib1Additional.Clone();
            expected.Built = null;
            expected.Repository = $"file:///{_repo1Path.ToRPath()}/src/contrib";

            actual.ShouldBeEquivalentTo(expected);
        }

        [Test]
        [Category.PackageManager]
        public async Task InstalledPackagesDefault() {
            // Get the installed packages from the default locations
            var result = await _workflow.Packages.GetInstalledPackagesAsync();

            // Validate some of the base packages
            result.Should().NotBeEmpty();

            // Since we don't control this package, only spot check a few fields unlikely to change
            var graphics = result.SingleOrDefault(pkg => pkg.Package == "graphics");
            graphics.Should().NotBeNull();
            graphics.LibPath.Should().NotBeNullOrWhiteSpace();
            graphics.Priority.Should().Be("base");
            graphics.Depends.Should().BeNull();
            graphics.Imports.Should().Be("grDevices");
            graphics.LinkingTo.Should().BeNull();
            graphics.Suggests.Should().BeNull();
            graphics.Title.Should().Be("The R Graphics Package");
            graphics.Author.Should().Be("R Core Team and contributors worldwide");
        }

        [Test]
        [Category.PackageManager]
        public async Task InstalledPackagesCustomLibPathNoPackages() {
            // Setup library path to point to the test folder, don't install anything in it
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalLibsAsync(eval, _libPath);
            }

            var result = await _workflow.Packages.GetInstalledPackagesAsync();

            // Since we can't remove Program Files folder from library paths,
            // we'll get some results, but nothing from the test folder.
            result.Should().NotBeEmpty().And.NotContain(pkg => pkg.LibPath == _libPath.ToRPath());
        }

        [Test]
        [Category.PackageManager]
        public async Task InstalledPackagesCustomLibPathOnePackage() {
            // Setup library path to point to the test folder, install a package into it
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalRepoAsync(eval, _repo1Path);
                await SetLocalLibsAsync(eval, _libPath);
                await InstallPackageAsync(eval, TestPackages.RtvsLib1Description.Package, _libPath);
            }

            var result = await _workflow.Packages.GetInstalledPackagesAsync();
            ValidateRtvslib1Installed(result, _libPath);
        }

        [Test]
        [Category.PackageManager]
        public async Task InstalledPackagesMultiLibsSamePackage() {
            // Install the same package in 2 different libraries
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalRepoAsync(eval, _repo1Path);
                await SetLocalLibsAsync(eval, _libPath);
                await InstallPackageAsync(eval, TestPackages.RtvsLib1Description.Package, _libPath);
                await SetLocalLibsAsync(eval, _lib2Path);
                await InstallPackageAsync(eval, TestPackages.RtvsLib1Description.Package, _lib2Path);
                await SetLocalLibsAsync(eval, _libPath, _lib2Path);
            }

            var result = await _workflow.Packages.GetInstalledPackagesAsync();
            ValidateRtvslib1Installed(result, _libPath);

            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalLibsAsync(eval, _lib2Path, _libPath);
            }

            result = await _workflow.Packages.GetInstalledPackagesAsync();
            ValidateRtvslib1Installed(result, _lib2Path);
        }

        private void ValidateRtvslib1Installed(IReadOnlyList<RPackage> pkgs, string libPath) {
            var rtvslib1Expected = TestPackages.RtvsLib1.Clone();
            rtvslib1Expected.LibPath = libPath.ToRPath();
            rtvslib1Expected.NeedsCompilation = null;

            var rtvslib1Actual = pkgs.Should().ContainSingle(pkg => pkg.Package == TestPackages.RtvsLib1Description.Package && pkg.LibPath == libPath.ToRPath()).Which;
            rtvslib1Actual.ShouldBeEquivalentTo(rtvslib1Expected);
        }

        [Test]
        [Category.PackageManager]
        public async Task InstallAndUninstallPackageSpecifiedLib() {
            var interactiveWindowComponentContainerFactory = _exportProvider.GetExportedValue<IInteractiveWindowComponentContainerFactory>();
            using (await UIThreadHelper.Instance.Invoke(() => _workflow.GetOrCreateVisualComponent(interactiveWindowComponentContainerFactory))) {
                _workflow.ActiveWindow.Should().NotBeNull();
                _workflow.RSession.IsHostRunning.Should().BeTrue();

                using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                    await SetLocalRepoAsync(eval, _repo1Path);
                }

                _workflow.Packages.InstallPackage(TestPackages.RtvsLib1Description.Package, _libPath);
                WaitForReplDoneExecuting();
                WaitForPackageInstalled(_libPath, TestPackages.RtvsLib1Description.Package);

                using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                    await SetLocalLibsAsync(eval, _libPath);
                }

                var installed = await _workflow.Packages.GetInstalledPackagesAsync();
                ValidateRtvslib1Installed(installed, _libPath);

                _workflow.Packages.UninstallPackage(TestPackages.RtvsLib1Description.Package, _libPath);
                WaitForReplDoneExecuting();

                installed = await _workflow.Packages.GetInstalledPackagesAsync();
                installed.Should().NotContain(pkg => pkg.Package == TestPackages.RtvsLib1Description.Package && pkg.LibPath == _libPath.ToRPath());
            }
        }

        [Test]
        [Category.PackageManager]
        public async Task InstallPackageDefaultLib() {
            var interactiveWindowComponentContainerFactory = _exportProvider.GetExportedValue<IInteractiveWindowComponentContainerFactory>();
            using (await UIThreadHelper.Instance.Invoke(() => _workflow.GetOrCreateVisualComponent(interactiveWindowComponentContainerFactory))) {
                _workflow.ActiveWindow.Should().NotBeNull();
                _workflow.RSession.IsHostRunning.Should().BeTrue();

                using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                    await SetLocalRepoAsync(eval, _repo1Path);
                    await SetLocalLibsAsync(eval, _libPath);
                }

                _workflow.Packages.InstallPackage(TestPackages.RtvsLib1Description.Package, null);
                WaitForReplDoneExecuting();
                WaitForPackageInstalled(_libPath, TestPackages.RtvsLib1Description.Package);

                var installed = await _workflow.Packages.GetInstalledPackagesAsync();
                ValidateRtvslib1Installed(installed, _libPath);
            }
        }

        [Test]
        [Category.PackageManager]
        public async Task LoadAndUnloadPackage() {
            var interactiveWindowComponentContainerFactory = _exportProvider.GetExportedValue<IInteractiveWindowComponentContainerFactory>();
            using (await UIThreadHelper.Instance.Invoke(() => _workflow.GetOrCreateVisualComponent(interactiveWindowComponentContainerFactory))) {
                _workflow.ActiveWindow.Should().NotBeNull();
                _workflow.RSession.IsHostRunning.Should().BeTrue();

                using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                    await SetLocalRepoAsync(eval, _repo1Path);
                    await SetLocalLibsAsync(eval, _libPath);
                    await InstallPackageAsync(eval, TestPackages.RtvsLib1Description.Package, _libPath);
                }

                await EvaluateCode("func1()", expectedError: "Error: could not find function \"func1\"");

                _workflow.Packages.LoadPackage(TestPackages.RtvsLib1Description.Package, null);
                WaitForReplDoneExecuting();

                await EvaluateCode("func1()", expectedResult: "func1");

                var loaded = await _workflow.Packages.GetLoadedPackagesAsync();
                loaded.Should().Contain("rtvslib1");

                _workflow.Packages.UnloadPackage(TestPackages.RtvsLib1Description.Package);
                WaitForReplDoneExecuting();

                await EvaluateCode("func1()", expectedError: "Error: could not find function \"func1\"");

                loaded = await _workflow.Packages.GetLoadedPackagesAsync();
                loaded.Should().NotContain("rtvslib1");
            }
        }

        [Test]
        [Category.PackageManager]
        public async Task GetLoadedPackages() {
            var interactiveWindowComponentContainerFactory = _exportProvider.GetExportedValue<IInteractiveWindowComponentContainerFactory>();
            using (await UIThreadHelper.Instance.Invoke(() => _workflow.GetOrCreateVisualComponent(interactiveWindowComponentContainerFactory))) {
                _workflow.ActiveWindow.Should().NotBeNull();
                _workflow.RSession.IsHostRunning.Should().BeTrue();

                using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                    await SetLocalRepoAsync(eval, _repo1Path);
                    await SetLocalLibsAsync(eval, _libPath);
                    await InstallPackageAsync(eval, TestPackages.RtvsLib1Description.Package, _libPath);
                }

                string[] results = await _workflow.Packages.GetLoadedPackagesAsync();
                results.Should().NotContain(new string[] {
                    "rtvslib1", ".GlobalEnv", "Autoloads",
                });
                results.Should().Contain(new string[] {
                    "stats", "graphics", "grDevices", "grDevices",
                    "utils", "datasets", "methods", "base",
                });
            }
        }

        [Test]
        [Category.PackageManager]
        public async Task LibraryPaths() {
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                await SetLocalRepoAsync(eval, _repo1Path);
                await SetLocalLibsAsync(eval, _libPath);
            }

            var result = await _workflow.Packages.GetLibraryPathsAsync();
            result[0].Should().Be(_libPath.ToRPath());
        }

        private async Task EvaluateCode(string code, string expectedResult = null, string expectedError = null) {
            using (var eval = await _workflow.RSession.BeginEvaluationAsync()) {
                var evalResult = await eval.EvaluateAsync("func1();", REvaluationKind.Normal);
                if (expectedResult != null) {
                    evalResult.StringResult.Trim().Should().Be(expectedResult.Trim());
                }
                if (expectedError != null) {
                    evalResult.Error.Trim().Should().Be(expectedError.Trim());
                }
            }
        }

        private async Task SetLocalRepoAsync(IRSessionEvaluation eval, string localRepoPath) {
            var code = string.Format("options(repos=list(LOCAL=\"file:///{0}\"))", localRepoPath.ToRPath());
            var evalResult = await eval.EvaluateAsync(code, REvaluationKind.Mutating);
        }

        private async Task SetLocalLibsAsync(IRSessionEvaluation eval, params string[] libPaths) {
            var paths = string.Join(",", libPaths.Select(p => p.ToRPath().ToRStringLiteral()));
            var code = string.Format(".libPaths(c({0}))", paths);
            var evalResult = await eval.EvaluateAsync(code, REvaluationKind.Normal);
        }

        private async Task InstallPackageAsync(IRSessionEvaluation eval, string packageName, string libPath) {
            var code = string.Format("install.packages(\"{0}\", verbose=FALSE, quiet=TRUE)", packageName);
            var evalResult = await eval.EvaluateAsync(code, REvaluationKind.Normal);
            WaitForPackageInstalled(libPath, packageName);
        }

        private async Task<IRInteractiveWorkflow> CreateWorkflowAsync() {
            var workflow = _workflowProvider.GetOrCreate();
            await workflow.RSession.StartHostAsync(new RHostStartupInfo {
                Name = _testMethod.Name,
                RBasePath = RToolsSettings.Current.RBasePath,
                RHostCommandLineArguments = RToolsSettings.Current.RCommandLineArguments,
                CranMirrorName = RToolsSettings.Current.CranMirror,
            }, 50000);
            return workflow;
        }

        private void WaitForReplDoneExecuting(int timeoutInSecs = 30) {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(timeoutInSecs);
            while (_workflow.ActiveWindow.IsRunning && DateTime.Now < (startTime + timeout)) {
                Thread.Sleep(100);
            }
            _workflow.ActiveWindow.IsRunning.Should().BeFalse();
        }

        private static void WaitForPackageInstalled(string libPath, string packageName) {
            WaitForAllFilesExist(new string[] {
                Path.Combine(libPath, packageName, "DESCRIPTION"),
                Path.Combine(libPath, packageName, "NAMESPACE"),
            });
        }

        private static void WaitForAllFilesExist(string[] filePaths, int timeoutMs = 5000) {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            while (!AllFilesExist(filePaths) && (DateTime.Now - startTime) < timeout) {
                Thread.Sleep(100);
            }

            AllFilesExist(filePaths).Should().BeTrue();
        }

        private static bool AllFilesExist(string[] filePaths) {
            foreach (var filePath in filePaths) {
                if (!File.Exists(filePath)) {
                    return false;
                }
            }
            return true;
        }

        public void Dispose() {
            (_exportProvider as IDisposable)?.Dispose();
        }

        public async Task InitializeAsync() {
            _workflow = await CreateWorkflowAsync();
        }

        public Task DisposeAsync() {
            return Task.CompletedTask;
        }
    }
}
