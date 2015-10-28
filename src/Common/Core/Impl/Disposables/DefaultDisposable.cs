using System;

namespace Microsoft.Common.Core.Disposables {
    /// <summary>
	/// Represents a disposable that does nothing on disposal.
	/// Implementation is copied from System.Reactive.Core.dll
	/// </summary>
	internal sealed class DefaultDisposable : IDisposable {
        /// <summary>
        /// Singleton default disposable.
        /// </summary>
        public static readonly DefaultDisposable Instance = new DefaultDisposable();

        private DefaultDisposable() { }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Dispose() { }
    }
}