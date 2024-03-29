// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System
{
    // TypeAccessException derives from TypeLoadException rather than MemberAccessException because in
    // pre-v4 releases of the runtime TypeLoadException was used in lieu of a TypeAccessException.
    [Serializable]
    [TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class TypeAccessException : TypeLoadException
    {
        public TypeAccessException()
            : base(SR.Arg_TypeAccessException)
        {
            HResult = HResults.COR_E_TYPEACCESS;
        }

        public TypeAccessException(string? message)
            : base(message ?? SR.Arg_TypeAccessException)
        {
            HResult = HResults.COR_E_TYPEACCESS;
        }

        public TypeAccessException(string? message, Exception? inner)
            : base(message ?? SR.Arg_TypeAccessException, inner)
        {
            HResult = HResults.COR_E_TYPEACCESS;
        }

        [Obsolete(Obsoletions.LegacyFormatterImplMessage, DiagnosticId = Obsoletions.LegacyFormatterImplDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected TypeAccessException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
