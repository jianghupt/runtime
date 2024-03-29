// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System
{
    [Serializable]
    [TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class NotFiniteNumberException : ArithmeticException
    {
        private readonly double _offendingNumber;

        public NotFiniteNumberException()
            : base(SR.Arg_NotFiniteNumberException)
        {
            _offendingNumber = 0;
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        public NotFiniteNumberException(double offendingNumber)
        {
            _offendingNumber = offendingNumber;
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        public NotFiniteNumberException(string? message)
            : base(message ?? SR.Arg_NotFiniteNumberException)
        {
            _offendingNumber = 0;
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        public NotFiniteNumberException(string? message, double offendingNumber)
            : base(message ?? SR.Arg_NotFiniteNumberException)
        {
            _offendingNumber = offendingNumber;
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        public NotFiniteNumberException(string? message, Exception? innerException)
            : base(message ?? SR.Arg_NotFiniteNumberException, innerException)
        {
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        public NotFiniteNumberException(string? message, double offendingNumber, Exception? innerException)
            : base(message ?? SR.Arg_NotFiniteNumberException, innerException)
        {
            _offendingNumber = offendingNumber;
            HResult = HResults.COR_E_NOTFINITENUMBER;
        }

        [Obsolete(Obsoletions.LegacyFormatterImplMessage, DiagnosticId = Obsoletions.LegacyFormatterImplDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected NotFiniteNumberException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _offendingNumber = info.GetDouble("OffendingNumber"); // Do not rename (binary serialization)
        }

        [Obsolete(Obsoletions.LegacyFormatterImplMessage, DiagnosticId = Obsoletions.LegacyFormatterImplDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("OffendingNumber", _offendingNumber, typeof(double)); // Do not rename (binary serialization)
        }

        public double OffendingNumber => _offendingNumber;
    }
}
