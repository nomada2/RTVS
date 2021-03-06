﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.R.Components.PackageManager.Model;

namespace Microsoft.R.Components.PackageManager.ViewModel {
    public interface IRPackageViewModel {
        string Name { get; }
        string Title { get; }
        string Description { get; }
        string LatestVersion { get; }
        string InstalledVersion { get; }
        string Authors { get; }
        string License { get; }
        ICollection<string> Urls { get; }
        bool NeedsCompilation { get; }
        string LibraryPath { get; }
        string Repository { get; }
        string Built { get; }

        string Depends { get; }
        string Imports { get; }
        string Suggests { get; }

        bool IsInstalled { get; set; }
        bool IsUpdateAvailable { get; }
        bool HasDetails { get; }
        bool IsSelected { get; set; }
        void AddDetails(RPackage package, bool isInstalled);
    }
}
