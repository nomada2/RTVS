﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.VisualStudio.ProjectSystem.Utilities;
using Microsoft.VisualStudio.R.Package.Commands;

namespace Microsoft.VisualStudio.R.Package.ProjectSystem.Commands {
    [ExportCommandGroup("AD87578C-B324-44DC-A12A-B01A6ED5C6E3")]
    [AppliesTo("RTools")]
    [OrderPrecedence(200)]
    internal sealed class OpenCommandPromptCommand : CommandPromptCommand {
        public OpenCommandPromptCommand() :
            base(RPackageCommandId.icmdOpenCmdPromptHere) { }

        protected override void SetFlags(ProcessStartInfo psi, string path) {
            psi.WorkingDirectory = path;
            psi.FileName = "cmd.exe";
            psi.UseShellExecute = false;
        }
    }
}
