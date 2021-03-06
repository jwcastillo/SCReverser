﻿using System;

namespace SCReverser.Core.Enums
{
    [Flags]
    public enum InstructionFlag : byte
    {
        #region Object state
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// UnusableCode
        /// </summary>
        UnusableCode = 1,
        #endregion
    }
}