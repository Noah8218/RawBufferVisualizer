using System;
using System.Globalization;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public static class VisualStudioDebugFrameContext
    {
        private static readonly object Gate = new object();
        private static IDebugThread2? _currentThread;

        public static void SetCurrentThread(IDebugThread2? thread)
        {
            lock (Gate)
            {
                _currentThread = thread;
            }
        }

        public static bool TryReadArrayBytes(
            string frameFunctionName,
            string arrayExpressionText,
            string firstElementExpression,
            string dataTypeName,
            int byteCount,
            out byte[] buffer,
            out string error)
        {
            buffer = Array.Empty<byte>();
            error = string.Empty;
            if (byteCount <= 0)
            {
                error = "Managed array byte count must be positive.";
                return false;
            }

            IDebugThread2? thread;
            lock (Gate)
            {
                thread = _currentThread;
            }

            if (thread == null)
            {
                error = "The native debugger frame is not available yet. Break again or use Scan Now.";
                return false;
            }

            IDebugStackFrame2? frame;
            if (!TryFindFrame(thread, frameFunctionName, out frame, out error) || frame == null)
            {
                return false;
            }

            IDebugExpressionContext2 expressionContext;
            var hr = frame.GetExpressionContext(out expressionContext);
            if (ErrorHandler.Failed(hr) || expressionContext == null)
            {
                error = "The debugger did not provide an expression context for the selected frame.";
                return false;
            }

            IDebugProperty2? arrayProperty;
            if (TryEvaluateProperty(expressionContext, arrayExpressionText, out arrayProperty, out error)
                && arrayProperty != null
                && TryEnumerateArrayBytes(arrayProperty, dataTypeName, byteCount, out buffer, out error))
            {
                return true;
            }

            IDebugExpression2 expression;
            string parseError;
            uint errorIndex;
            hr = expressionContext.ParseText(
                firstElementExpression,
                enum_PARSEFLAGS.PARSE_EXPRESSION,
                10,
                out expression,
                out parseError,
                out errorIndex);
            if (ErrorHandler.Failed(hr) || expression == null)
            {
                error = string.IsNullOrWhiteSpace(parseError)
                    ? "The debugger could not parse the first managed-array element expression."
                    : parseError;
                return false;
            }

            IDebugProperty2 property;
            const enum_EVALFLAGS evalFlags = enum_EVALFLAGS.EVAL_NOSIDEEFFECTS
                | enum_EVALFLAGS.EVAL_NOFUNCEVAL
                | enum_EVALFLAGS.EVAL_NOEVENTS;
            hr = expression.EvaluateSync(evalFlags, 2000, null, out property);
            if (ErrorHandler.Failed(hr) || property == null)
            {
                error = "The debugger could not evaluate the first managed-array element without function evaluation.";
                return false;
            }

            IDebugMemoryBytes2 memoryBytes;
            IDebugMemoryContext2 memoryContext;
            hr = property.GetMemoryBytes(out memoryBytes);
            if (ErrorHandler.Failed(hr) || memoryBytes == null)
            {
                error = "The debugger does not expose readable memory for this managed array.";
                return false;
            }

            hr = property.GetMemoryContext(out memoryContext);
            if (ErrorHandler.Failed(hr) || memoryContext == null)
            {
                error = "The debugger does not expose the first managed-array element address.";
                return false;
            }

            var bytes = new byte[byteCount];
            uint bytesRead;
            uint unreadableBytes = 0;
            hr = memoryBytes.ReadAt(memoryContext, (uint)byteCount, bytes, out bytesRead, ref unreadableBytes);
            if (ErrorHandler.Failed(hr) || bytesRead != (uint)byteCount || unreadableBytes != 0)
            {
                error = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Managed array memory read was incomplete ({0}/{1} bytes, {2} unreadable).",
                    bytesRead,
                    byteCount,
                    unreadableBytes);
                return false;
            }

            buffer = bytes;
            return true;
        }

        private static bool TryEvaluateProperty(
            IDebugExpressionContext2 expressionContext,
            string expressionText,
            out IDebugProperty2? property,
            out string error)
        {
            property = null;
            error = string.Empty;
            IDebugExpression2 expression;
            string parseError;
            uint errorIndex;
            var hr = expressionContext.ParseText(
                expressionText,
                enum_PARSEFLAGS.PARSE_EXPRESSION,
                10,
                out expression,
                out parseError,
                out errorIndex);
            if (ErrorHandler.Failed(hr) || expression == null)
            {
                error = string.IsNullOrWhiteSpace(parseError)
                    ? "The debugger could not parse the managed-array expression."
                    : parseError;
                return false;
            }

            // The mapped array member can be an ordinary property. Permit that explicit
            // getter while retaining no-side-effects/no-events evaluation; arbitrary
            // method expressions are never constructed by the inspector.
            const enum_EVALFLAGS evalFlags = enum_EVALFLAGS.EVAL_NOSIDEEFFECTS
                | enum_EVALFLAGS.EVAL_NOEVENTS;
            hr = expression.EvaluateSync(evalFlags, 2000, null, out property);
            if (ErrorHandler.Failed(hr) || property == null)
            {
                error = "The debugger could not evaluate the mapped managed-array property.";
                return false;
            }

            return true;
        }

        private static bool TryEnumerateArrayBytes(
            IDebugProperty2 arrayProperty,
            string dataTypeName,
            int byteCount,
            out byte[] buffer,
            out string error)
        {
            buffer = Array.Empty<byte>();
            error = string.Empty;
            var elementSize = GetElementSize(dataTypeName);
            if (elementSize == 0 || byteCount <= 0 || byteCount % elementSize != 0)
            {
                error = "The managed-array element layout is unsupported.";
                return false;
            }

            var requiredElements = byteCount / elementSize;
            var filter = Guid.Empty;
            IEnumDebugPropertyInfo2 children;
            const enum_DEBUGPROP_INFO_FLAGS fields = enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME
                | enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE
                | enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            var hr = arrayProperty.EnumChildren(
                fields,
                10,
                ref filter,
                enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE,
                null,
                5000,
                out children);
            if (ErrorHandler.Failed(hr) || children == null)
            {
                error = "The native debugger could not enumerate managed-array elements.";
                return false;
            }

            uint count;
            hr = children.GetCount(out count);
            if (ErrorHandler.Failed(hr) || count < requiredElements)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "The native debugger returned {0} of {1} required managed-array elements.",
                    count,
                    requiredElements);
                return false;
            }

            buffer = new byte[byteCount];
            var writtenElements = 0;
            var batch = new DEBUG_PROPERTY_INFO[Math.Min(256, requiredElements)];
            while (writtenElements < requiredElements)
            {
                var requested = Math.Min(batch.Length, requiredElements - writtenElements);
                uint fetched = 0;
                hr = children.Next((uint)requested, batch, out fetched);
                if (ErrorHandler.Failed(hr) || fetched == 0)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Managed-array enumeration stopped at element {0} of {1}.",
                        writtenElements,
                        requiredElements);
                    buffer = Array.Empty<byte>();
                    return false;
                }

                for (var i = 0; i < fetched && writtenElements < requiredElements; i++)
                {
                    if (!TryWriteElement(
                        buffer,
                        writtenElements * elementSize,
                        dataTypeName,
                        batch[i].bstrValue ?? string.Empty))
                    {
                        error = string.Format(
                            CultureInfo.InvariantCulture,
                            "Managed-array element {0} has an unreadable value: {1}",
                            writtenElements,
                            batch[i].bstrValue);
                        buffer = Array.Empty<byte>();
                        return false;
                    }

                    writtenElements++;
                }
            }

            return true;
        }

        private static int GetElementSize(string dataTypeName)
        {
            if (dataTypeName.EndsWith("Byte[]", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (dataTypeName.EndsWith("UInt16[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("UShort[]", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (dataTypeName.EndsWith("Single[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("Float[]", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 0;
        }

        private static bool TryWriteElement(
            byte[] destination,
            int offset,
            string dataTypeName,
            string rawValue)
        {
            var value = (rawValue ?? string.Empty).Trim();
            if (dataTypeName.EndsWith("Byte[]", StringComparison.OrdinalIgnoreCase))
            {
                byte parsed;
                if (!TryParseByte(value, out parsed))
                {
                    return false;
                }

                destination[offset] = parsed;
                return true;
            }

            if (dataTypeName.EndsWith("UInt16[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("UShort[]", StringComparison.OrdinalIgnoreCase))
            {
                ushort parsed;
                if (!TryParseUInt16(value, out parsed))
                {
                    return false;
                }

                var bytes = BitConverter.GetBytes(parsed);
                Buffer.BlockCopy(bytes, 0, destination, offset, bytes.Length);
                return true;
            }

            if (dataTypeName.EndsWith("Single[]", StringComparison.OrdinalIgnoreCase)
                || dataTypeName.EndsWith("Float[]", StringComparison.OrdinalIgnoreCase))
            {
                float parsed;
                value = value.TrimEnd('f', 'F');
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return false;
                }

                var bytes = BitConverter.GetBytes(parsed);
                Buffer.BlockCopy(bytes, 0, destination, offset, bytes.Length);
                return true;
            }

            return false;
        }

        private static bool TryParseByte(string value, out byte parsed)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(
                    LeadingToken(value.Substring(2)),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out parsed);
            }

            return byte.TryParse(
                LeadingToken(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed);
        }

        private static bool TryParseUInt16(string value, out ushort parsed)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(
                    LeadingToken(value.Substring(2)),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out parsed);
            }

            return ushort.TryParse(
                LeadingToken(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed);
        }

        private static string LeadingToken(string value)
        {
            var separator = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return separator < 0 ? value : value.Substring(0, separator);
        }

        private static bool TryFindFrame(
            IDebugThread2 thread,
            string frameFunctionName,
            out IDebugStackFrame2? frame,
            out string error)
        {
            frame = null;
            error = string.Empty;
            IEnumDebugFrameInfo2 frameEnumerator;
            const enum_FRAMEINFO_FLAGS flags = enum_FRAMEINFO_FLAGS.FIF_FRAME
                | enum_FRAMEINFO_FLAGS.FIF_FUNCNAME;
            var hr = thread.EnumFrameInfo(flags, 10, out frameEnumerator);
            if (ErrorHandler.Failed(hr) || frameEnumerator == null)
            {
                error = "The debugger could not enumerate stack frames.";
                return false;
            }

            IDebugStackFrame2? firstFrame = null;
            var info = new FRAMEINFO[1];
            while (true)
            {
                uint fetched = 0;
                hr = frameEnumerator.Next(1, info, ref fetched);
                if (ErrorHandler.Failed(hr) || fetched == 0)
                {
                    break;
                }

                if (firstFrame == null)
                {
                    firstFrame = info[0].m_pFrame;
                }

                if (info[0].m_pFrame != null
                    && FrameNamesMatch(frameFunctionName, info[0].m_bstrFuncName))
                {
                    frame = info[0].m_pFrame;
                    return true;
                }
            }

            frame = firstFrame;
            if (frame != null)
            {
                return true;
            }

            error = "The debugger did not return a readable stack frame.";
            return false;
        }

        private static bool FrameNamesMatch(string dteName, string nativeName)
        {
            if (string.IsNullOrWhiteSpace(dteName) || string.IsNullOrWhiteSpace(nativeName))
            {
                return false;
            }

            return dteName.IndexOf(nativeName, StringComparison.OrdinalIgnoreCase) >= 0
                || nativeName.IndexOf(dteName, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
