using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    /// <summary>
    /// Correlates an out-of-process debugger-visualizer handoff with the expression
    /// that is still present in the current stack frame. Visual Studio 17.9 does not
    /// expose OriginalVisualizedExpression, so object identity is the compatibility
    /// fallback for reference types and pointer/type matching covers value wrappers.
    /// </summary>
    internal static class DebuggerExpressionDisplayNameResolver
    {
        private const int EvaluationTimeoutMilliseconds = 1000;

        public static string Resolve(EnvDTE80.DTE2? dte, VisualizerHandoffRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null || request == null || request.ExpressionIdentityHash == 0)
            {
                return request == null ? string.Empty : request.DisplayName;
            }

            EnvDTE.Debugger? debugger;
            EnvDTE.StackFrame? frame;
            try
            {
                debugger = dte.Debugger;
                frame = debugger?.CurrentStackFrame;
            }
            catch
            {
                return request.DisplayName;
            }

            if (debugger == null || frame == null)
            {
                return request.DisplayName;
            }

            var candidates = new List<EnvDTE.Expression>();
            var expressionSourceType = string.IsNullOrWhiteSpace(request.ExpressionSourceType)
                ? request.SourceType
                : request.ExpressionSourceType;
            AddCompatibleExpressions(candidates, TryGetExpressions(frame, true), expressionSourceType);
            AddCompatibleExpressions(candidates, TryGetExpressions(frame, false), expressionSourceType);
            if (candidates.Count == 0)
            {
                return request.DisplayName;
            }

            var identityMatches = new List<EnvDTE.Expression>();
            for (var index = 0; index < candidates.Count; index++)
            {
                int identityHash;
                if (TryReadIdentityHash(debugger, candidates[index], out identityHash)
                    && identityHash == request.ExpressionIdentityHash)
                {
                    identityMatches.Add(candidates[index]);
                }
            }

            var resolved = GetUniqueExpressionName(identityMatches);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                var pointerMatches = new List<EnvDTE.Expression>();
                for (var index = 0; index < candidates.Count; index++)
                {
                    if (MatchesPointer(debugger, candidates[index], request))
                    {
                        pointerMatches.Add(candidates[index]);
                    }
                }

                resolved = GetUniqueExpressionName(pointerMatches);
            }

            if (string.IsNullOrWhiteSpace(resolved) && candidates.Count == 1)
            {
                resolved = ReadName(candidates[0]);
            }

            return string.IsNullOrWhiteSpace(resolved)
                ? request.DisplayName
                : ApplyExpressionName(resolved!, request.DisplayName);
        }

        private static EnvDTE.Expressions? TryGetExpressions(EnvDTE.StackFrame frame, bool locals)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return locals ? frame.Locals : frame.Arguments;
            }
            catch
            {
                return null;
            }
        }

        private static void AddCompatibleExpressions(
            List<EnvDTE.Expression> destination,
            EnvDTE.Expressions? expressions,
            string sourceType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (expressions == null)
            {
                return;
            }

            int count;
            try
            {
                count = expressions.Count;
            }
            catch
            {
                return;
            }

            for (var index = 1; index <= count; index++)
            {
                EnvDTE.Expression? expression;
                try
                {
                    expression = expressions.Item(index);
                }
                catch
                {
                    continue;
                }

                if (expression == null || !IsCompatibleType(ReadType(expression), sourceType))
                {
                    continue;
                }

                var name = ReadName(expression);
                if (!string.IsNullOrWhiteSpace(name)
                    && !destination.Exists(candidate => string.Equals(
                        ReadName(candidate),
                        name,
                        StringComparison.Ordinal)))
                {
                    destination.Add(expression);
                }
            }
        }

        private static bool TryReadIdentityHash(
            EnvDTE.Debugger debugger,
            EnvDTE.Expression expression,
            out int identityHash)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            identityHash = 0;
            var name = ReadName(expression);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var evaluated = debugger.GetExpression(
                    "System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode((object)(" + name + "))",
                    true,
                    EvaluationTimeoutMilliseconds);
                return evaluated != null
                    && IsValid(evaluated)
                    && int.TryParse(
                        (evaluated.Value ?? string.Empty).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out identityHash);
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchesPointer(
            EnvDTE.Debugger debugger,
            EnvDTE.Expression expression,
            VisualizerHandoffRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (request.SourcePointerAddress == 0
                || !IsSimpleIdentifier(request.SourcePointerLabel))
            {
                return false;
            }

            var name = ReadName(expression);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var evaluated = debugger.GetExpression(
                    "(" + name + ")." + request.SourcePointerLabel,
                    true,
                    EvaluationTimeoutMilliseconds);
                long pointer;
                return evaluated != null
                    && IsValid(evaluated)
                    && TryParsePointer(evaluated.Value, out pointer)
                    && pointer == request.SourcePointerAddress;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSimpleIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !(char.IsLetter(value[0]) || value[0] == '_'))
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParsePointer(string? value, out long pointer)
        {
            pointer = 0;
            var text = (value ?? string.Empty).Trim();
            var marker = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
            {
                var end = marker + 2;
                while (end < text.Length && Uri.IsHexDigit(text[end]))
                {
                    end++;
                }

                ulong unsigned;
                if (ulong.TryParse(
                    text.Substring(marker + 2, end - marker - 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out unsigned))
                {
                    pointer = unchecked((long)unsigned);
                    return pointer != 0;
                }
            }

            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out pointer)
                && pointer != 0;
        }

        private static bool IsCompatibleType(string expressionType, string sourceType)
        {
            var left = GetTypeStem(expressionType);
            var right = GetTypeStem(sourceType);
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left, right, StringComparison.Ordinal);
        }

        private static string GetTypeStem(string value)
        {
            var text = (value ?? string.Empty)
                .Replace("global::", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();
            var cut = text.Length;
            var tick = text.IndexOf('`');
            var angle = text.IndexOf('<');
            var square = text.IndexOf('[');
            var comma = text.IndexOf(',');
            if (tick >= 0) cut = Math.Min(cut, tick);
            if (angle >= 0) cut = Math.Min(cut, angle);
            if (square >= 0) cut = Math.Min(cut, square);
            if (comma >= 0) cut = Math.Min(cut, comma);
            var stem = text.Substring(0, cut);
            return string.Equals(stem, "object", StringComparison.Ordinal)
                ? "System.Object"
                : stem;
        }

        private static string GetUniqueExpressionName(List<EnvDTE.Expression> expressions)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return expressions.Count == 1 ? ReadName(expressions[0]) : string.Empty;
        }

        private static string ApplyExpressionName(string expressionName, string currentDisplayName)
        {
            var current = (currentDisplayName ?? string.Empty).Trim();
            if (current.StartsWith("[", StringComparison.Ordinal))
            {
                return expressionName + current;
            }

            var bracket = current.IndexOf('[');
            if (bracket >= 0)
            {
                return expressionName + current.Substring(bracket);
            }

            return expressionName;
        }

        private static bool IsValid(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.IsValidValue;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadName(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return (expression.Name ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadType(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return (expression.Type ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
