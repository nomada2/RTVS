﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.Languages.Editor.Services;
using Microsoft.R.Components.ContentTypes;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.R.Editor.Signatures {
    [Export(typeof(ISignatureHelpSourceProvider))]
    [ContentType(RContentTypeDefinition.ContentType)]
    [Name("R Signature Source Provider")]
    [Order(Before = "default")]
    sealed class SignatureHelpSourceProvider : ISignatureHelpSourceProvider {
        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer) {
            var helpSource = ServiceManager.GetService<SignatureHelpSource>(textBuffer);
            if (helpSource == null) {
                helpSource = new SignatureHelpSource(textBuffer);
            }
            return helpSource;
        }
    }
}
