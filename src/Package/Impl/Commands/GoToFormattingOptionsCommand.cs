﻿using System;
using Microsoft.Languages.Editor;
using Microsoft.Languages.Editor.Controller.Command;
using Microsoft.VisualStudio.R.Package.Options.R.Editor;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.R.Package.Commands
{
    public sealed class GoToFormattingOptionsCommand : ViewCommand
    {
        public GoToFormattingOptionsCommand(ITextView textView, ITextBuffer textBuffer) :
            base(textView, GuidList.CmdSetGuid, RPackageCommandId.icmdGoToFormattingOptions, false)
        {
        }

        public override CommandStatus Status(Guid group, int id)
        {
            return CommandStatus.SupportedAndEnabled;
        }

        public override CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg)
        {
            IVsShell shell = AppShell.Current.GetGlobalService<IVsShell>(typeof(SVsShell));
            IVsPackage package;

            if (VSConstants.S_OK == shell.LoadPackage(GuidList.PackageGuid, out package))
            {
                ((Microsoft.VisualStudio.Shell.Package)package).ShowOptionPage(typeof(REditorOptionsDialog));
            }

            return CommandResult.Executed;
        }
    }
}