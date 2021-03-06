﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Languages.Editor.Controller.Command;
using Microsoft.R.Components.ContentTypes;
using Microsoft.R.Components.Controller;
using Microsoft.R.Components.History;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.VisualStudio.R.Package.Commands;
using Microsoft.VisualStudio.R.Packages.R;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.R.Package.History.Commands {
    internal class SendHistoryToSourceCommand : ViewCommand {
        private readonly IRInteractiveWorkflow _interactiveWorkflow;
        private readonly IActiveWpfTextViewTracker _textViewTracker;
        private readonly IContentType _contentType;
        private readonly IRHistory _history;

        public SendHistoryToSourceCommand(ITextView textView, IRHistoryProvider historyProvider, IRInteractiveWorkflow interactiveWorkflow, IContentTypeRegistryService contentTypeRegistry, IActiveWpfTextViewTracker textViewTracker)
            : base(textView, RGuidList.RCmdSetGuid, RPackageCommandId.icmdSendHistoryToSource, false) {

            _textViewTracker = textViewTracker;
            _interactiveWorkflow = interactiveWorkflow;
            _contentType = contentTypeRegistry.GetContentType(RContentTypeDefinition.ContentType);
            _history = historyProvider.GetAssociatedRHistory(textView);
        }

        public override CommandStatus Status(Guid guid, int id) {
            return _interactiveWorkflow.ActiveWindow != null && (_history.HasSelectedEntries || !TextView.Selection.IsEmpty) && GetLastActiveRTextView() != null
                ? CommandStatus.SupportedAndEnabled
                : CommandStatus.Supported;
        }

        public override CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg) {
            var textView = GetLastActiveRTextView();
            if (textView != null) {
                _history.SendSelectedToTextView(textView);
            }

            return CommandResult.Executed;
        }

        private IWpfTextView GetLastActiveRTextView() {
            var textView = _textViewTracker.GetLastActiveTextView(_contentType);
            return textView != null && !textView.IsClosed && textView.VisualElement.IsVisible
                ? textView
                : null;
        }
    }
}