// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Description: WinEventHandler implementation.
//

using System.Runtime.InteropServices;
using MS.Win32;

using MS.Internal;

namespace System.Windows.Documents
{
    internal class WinEventHandler : IDisposable
    {
        //------------------------------------------------------
        //
        //  Constructors
        //
        //------------------------------------------------------

        #region Constructors

        // ctor that takes a range of events
        internal WinEventHandler(int eventMin, int eventMax)
        {
            _eventMin = eventMin;
            _eventMax = eventMax;

            _winEventProc = new NativeMethods.WinEventProcDef(WinEventDefaultProc);
            // Keep the garbage collector from moving things around
            _gchThis = GCHandle.Alloc(_winEventProc);

            // Workaround for bug 150666.
            _shutdownListener = new WinEventHandlerShutDownListener(this);
        }

        ~WinEventHandler()
        {
            Clear();
        }

        #endregion Constructors

        //------------------------------------------------------
        //
        //  Internal Methods
        //
        //------------------------------------------------------

        #region Internal Methods

        public void Dispose()
        {
            // no need to call this finalizer.

            GC.SuppressFinalize(this);
            Clear();
        }

        // callback for the class inherited from this.
        internal virtual void WinEventProc(int eventId, IntPtr hwnd)
        {
        }

        internal void Clear()
        {
            // Make sure that the hooks is uninitialzied.
            Stop();

            // free GC handle.
            if (_gchThis.IsAllocated)
            {
                _gchThis.Free();
            }
        }

        // install WinEvent hook and start getting the callback.
        internal void Start()
        {
            if (_gchThis.IsAllocated)
            {
                _hHook = UnsafeNativeMethods.SetWinEventHook(_eventMin, _eventMax, IntPtr.Zero, _winEventProc,
                                                             0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
                if (_hHook == IntPtr.Zero )
                {
                    Stop();
                }
            }
        }

        // uninstall WinEvent hook.
        internal void Stop()
        {
            if (_hHook != IntPtr.Zero )
            {
                UnsafeNativeMethods.UnhookWinEvent(_hHook);
                _hHook = IntPtr.Zero ;
            }

            _shutdownListener?.StopListening();
            _shutdownListener = null;
        }

        #endregion Internal Methods

        //------------------------------------------------------
        //
        //  Private Methods
        //
        //------------------------------------------------------

        #region Private Methods

        private void WinEventDefaultProc(int winEventHook, int eventId, IntPtr hwnd, int idObject, int idChild, int eventThread, int eventTime)
        {
            WinEventProc(eventId , hwnd);
        }

        #endregion Private Methods

        #region Private Types

        private sealed class WinEventHandlerShutDownListener : ShutDownListener
        {
            public WinEventHandlerShutDownListener(WinEventHandler target)
                : base(target, ShutDownEvents.DispatcherShutdown)
            {
            }

            internal override void OnShutDown(object target, object sender, EventArgs e)
            {
                WinEventHandler winEventHandler = (WinEventHandler)target;
                winEventHandler.Stop();
            }
        }

        #endregion Private Types

        //------------------------------------------------------
        //
        //  Private Fields
        //
        //------------------------------------------------------

        #region Private Fields

        // min WinEvent.
        private int _eventMin;

        // max WinEvent.
        private int _eventMax;

        // hook handle
        private IntPtr _hHook;

        // the callback.
        private NativeMethods.WinEventProcDef _winEventProc;

        // GCHandle to keep the garbage collector from moving things around
        private GCHandle _gchThis;

        // shutdown listener
        private WinEventHandlerShutDownListener _shutdownListener;

        #endregion Private Fields
    }
}
