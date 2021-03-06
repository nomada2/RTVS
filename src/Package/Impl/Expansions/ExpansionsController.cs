// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.R.Components.Controller;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.R.Package.Expansions {
    /// <summary>
    /// Code expansions (aka snippets) command controller
    /// </summary>
    public class ExpansionsController : ICommandTarget {
        private ExpansionClient _expansionClient;
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        public ExpansionsController(ITextView textView, ITextBuffer textBuffer, IVsExpansionManager expansionManager, IExpansionsCache cache) {
            _textView = textView;
            _textBuffer = textBuffer;
            _expansionClient = new ExpansionClient(textView, textBuffer, expansionManager, cache);
        }

        internal IVsExpansionClient ExpansionClient {
            get { return _expansionClient; }
        }

        #region ICommandTarget
        public CommandStatus Status(Guid group, int id) {
            if (group == VSConstants.VSStd2K) {
                if (!_expansionClient.IsEditingExpansion()) {
                    switch ((VSConstants.VSStd2KCmdID)id) {
                        case VSConstants.VSStd2KCmdID.TAB:
                            return ShouldIgnoreStatementCompletionCommand() ? CommandStatus.NotSupported : CommandStatus.SupportedAndEnabled;

                        case VSConstants.VSStd2KCmdID.INSERTSNIPPET:
                        case VSConstants.VSStd2KCmdID.SURROUNDWITH:
                            return CommandStatus.SupportedAndEnabled;
                    }
                } else {
                    switch ((VSConstants.VSStd2KCmdID)id) {
                        case VSConstants.VSStd2KCmdID.TAB:
                        case VSConstants.VSStd2KCmdID.BACKTAB:
                        case VSConstants.VSStd2KCmdID.RETURN:
                        case VSConstants.VSStd2KCmdID.CANCEL:
                        case VSConstants.VSStd2KCmdID.INSERTSNIPPET:
                        case VSConstants.VSStd2KCmdID.SURROUNDWITH:
                            return CommandStatus.SupportedAndEnabled;
                    }
                }
            }
            return CommandStatus.NotSupported;
        }

        public CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg) {
            var hr = VSConstants.E_FAIL;
            if (group == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)id) {
                    case VSConstants.VSStd2KCmdID.INSERTSNIPPET:
                    case VSConstants.VSStd2KCmdID.SURROUNDWITH:
                        _expansionClient.InvokeInsertionUI(id);
                        return CommandResult.Executed;

                    case VSConstants.VSStd2KCmdID.TAB:
                        if (_expansionClient.IsEditingExpansion()) {
                            hr = _expansionClient.GoToNextExpansionField();
                        } else {
                            bool snippetInserted = false;

                            hr = _expansionClient.StartSnippetInsertion(out snippetInserted);
                            if (!snippetInserted)
                                return CommandResult.NotSupported;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.BACKTAB:
                        if (_expansionClient.IsEditingExpansion()) {
                            hr = _expansionClient.GoToPreviousExpansionField();
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.RETURN:
                        if (!ShouldIgnoreStatementCompletionCommand() && _expansionClient.IsEditingExpansion()) {
                            if (_expansionClient.IsCaretInsideSnippetFields()) {
                                // End the current expansion session and position the 
                                // edit caret according to the code snippet template.
                                _expansionClient.EndExpansionSession(false);
                                return CommandResult.Executed;
                            } else {
                                // Dev10 710692: if caret is not inside of one of the snippet fields then leave caret at the current position and let the 
                                //   editor handles the RETURN
                                _expansionClient.EndExpansionSession(true);
                            }
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.CANCEL:
                        if (!IsStatementCompletionWindowActive(_expansionClient.TextView) && _expansionClient.IsEditingExpansion()) {
                            _expansionClient.EndExpansionSession(true);
                            return CommandResult.Executed;
                        }
                        break;
                }
            }

            return hr == VSConstants.S_OK ? CommandResult.Executed : CommandResult.NotSupported;
        }

        public void PostProcessInvoke(CommandResult result, Guid group, int id, object inputArg, ref object outputArg) {
        }
        #endregion

        private bool ShouldIgnoreStatementCompletionCommand() {
            return IsStatementCompletionWindowActive(_expansionClient.TextView);
        }

        private static bool IsStatementCompletionWindowActive(ITextView textView) {
            bool result = false;
            if (textView != null) {
                ICompletionBroker completionBroker = VsAppShell.Current.ExportProvider.GetExport<ICompletionBroker>().Value;
                result = completionBroker.IsCompletionActive(textView);
            }
            return result;
        }
    }
}
