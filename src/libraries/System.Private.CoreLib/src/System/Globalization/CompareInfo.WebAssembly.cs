// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Globalization
{
    public partial class CompareInfo
    {
        private static void AssertHybridOnWasm(CompareOptions options)
        {
            Debug.Assert(!GlobalizationMode.Invariant);
            Debug.Assert(!GlobalizationMode.UseNls);
            Debug.Assert(GlobalizationMode.Hybrid);
            Debug.Assert((options & (CompareOptions.Ordinal | CompareOptions.OrdinalIgnoreCase)) == 0);
        }

        private static void AssertComparisonSupported(CompareOptions options, string cultureName)
        {
            if (CompareOptionsNotSupported(options))
                throw new PlatformNotSupportedException(GetPNSE(options));

            if (CompareOptionsNotSupportedForCulture(options, cultureName))
                throw new PlatformNotSupportedException(GetPNSEForCulture(options, cultureName));
        }

        private static void AssertIndexingSupported(CompareOptions options, string cultureName)
        {
            if (IndexingOptionsNotSupported(options) || CompareOptionsNotSupported(options))
                throw new PlatformNotSupportedException(GetPNSE(options));

            if (CompareOptionsNotSupportedForCulture(options, cultureName))
                throw new PlatformNotSupportedException(GetPNSEForCulture(options, cultureName));
        }

        private unsafe int JsCompareString(ReadOnlySpan<char> string1, ReadOnlySpan<char> string2, CompareOptions options)
        {
            AssertHybridOnWasm(options);
            string cultureName = m_name;
            AssertComparisonSupported(options, cultureName);

            string exceptionMessage;
            int cmpResult;
            fixed (char* pString1 = &MemoryMarshal.GetReference(string1))
            fixed (char* pString2 = &MemoryMarshal.GetReference(string2))
            {
                cmpResult = Interop.JsGlobalization.CompareString(out exceptionMessage, cultureName, pString1, string1.Length, pString2, string2.Length, options);
            }

            if (!string.IsNullOrEmpty(exceptionMessage))
                throw new Exception(exceptionMessage);

            return cmpResult;
        }

        private unsafe bool JsStartsWith(ReadOnlySpan<char> source, ReadOnlySpan<char> prefix, CompareOptions options)
        {
            AssertHybridOnWasm(options);
            Debug.Assert(!prefix.IsEmpty);
            string cultureName = m_name;
            AssertIndexingSupported(options, cultureName);

            string exceptionMessage;
            bool result;
            fixed (char* pSource = &MemoryMarshal.GetReference(source))
            fixed (char* pPrefix = &MemoryMarshal.GetReference(prefix))
            {
                result = Interop.JsGlobalization.StartsWith(out exceptionMessage, cultureName, pSource, source.Length, pPrefix, prefix.Length, options);
            }

            if (!string.IsNullOrEmpty(exceptionMessage))
                throw new Exception(exceptionMessage);

            return result;
        }

        private unsafe bool JsEndsWith(ReadOnlySpan<char> source, ReadOnlySpan<char> prefix, CompareOptions options)
        {
            AssertHybridOnWasm(options);
            Debug.Assert(!prefix.IsEmpty);
            string cultureName = m_name;
            AssertIndexingSupported(options, cultureName);

            string exceptionMessage;
            bool result;
            fixed (char* pSource = &MemoryMarshal.GetReference(source))
            fixed (char* pPrefix = &MemoryMarshal.GetReference(prefix))
            {
                result = Interop.JsGlobalization.EndsWith(out exceptionMessage, cultureName, pSource, source.Length, pPrefix, prefix.Length, options);
            }

            if (!string.IsNullOrEmpty(exceptionMessage))
                throw new Exception(exceptionMessage);

            return result;
        }

        private static bool IndexingOptionsNotSupported(CompareOptions options) =>
            (options & CompareOptions.IgnoreSymbols) == CompareOptions.IgnoreSymbols;

        private static bool CompareOptionsNotSupported(CompareOptions options) =>
            (options & CompareOptions.IgnoreWidth) == CompareOptions.IgnoreWidth ||
            ((options & CompareOptions.IgnoreNonSpace) == CompareOptions.IgnoreNonSpace && (options & CompareOptions.IgnoreKanaType) != CompareOptions.IgnoreKanaType);

        private static string GetPNSE(CompareOptions options) =>
            SR.Format(SR.PlatformNotSupported_HybridGlobalizationWithCompareOptions, options);

        private static bool CompareOptionsNotSupportedForCulture(CompareOptions options, string cultureName) =>
            (options == CompareOptions.IgnoreKanaType &&
            (string.IsNullOrEmpty(cultureName) || cultureName.Split('-')[0] != "ja")) ||
            (options == CompareOptions.None &&
            (cultureName.Split('-')[0] == "ja"));

        private static string GetPNSEForCulture(CompareOptions options, string cultureName) =>
            SR.Format(SR.PlatformNotSupported_HybridGlobalizationWithCompareOptions, options, cultureName);
    }
}