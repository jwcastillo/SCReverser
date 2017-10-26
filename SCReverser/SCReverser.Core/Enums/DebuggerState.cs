﻿using System;

namespace SCReverser.Core.Enums
{
    [Flags]
    public enum DebuggerState : byte
    {
        #region Object state
        None = 0,
        Disposed = 1,
        Initialized = 2,
        #endregion

        #region Debug session State
        BreakPoint = 4,
        Error = 8,
        Ended = 16,
        #endregion
    }
}