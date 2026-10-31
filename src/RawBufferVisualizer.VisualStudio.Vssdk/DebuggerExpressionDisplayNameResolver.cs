using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    /// <summary>
    /// Correlates an out-of-process debugger-visualizer handoff with the expression
    /// that is still present in the current stack frame. Visual Studio 17.9 does not
    /// expose OriginalVisualizedExpression, so bounded object-hash matching is the compatibility
    /// fallback for reference types. A shared address alone does not identify an object.
    /// </summary>
    internal static class DebuggerExpressionDisplayNameResolver
    {
        private const int EvaluationTimeoutMilliseconds = 100;
        private const int TotalBudgetMilliseconds = 200;
        private const int MaximumExpressionsPerScope = 64;
        private const int MaximumCompatibleExpressions = 4;

        public static string Resolve(EnvDTE80.DTE2? dte, VisualizerHandoffRequest request, Func<bool>? isCurrent = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null || request == null || request.ExpressionIdentityHash == 0)
            {
                return request == null ? string.Empty : request.DisplayName;
            }

            var expressionSourceType = string.IsNullOrWhiteSpace(request.ExpressionSourceType)
                ? request.SourceType : request.ExpressionSourceType;
            var displayRoot = request.DisplayName.Split('[')[0].Trim();
            var typeStem = GetTypeStem(expressionSourceType);
            var shortType = typeStem.Substring(typeStem.LastIndexOf('.') + 1);
            if (displayRoot.Length != 0 && !string.Equals(displayRoot, shortType, StringComparison.Ordinal))
                return request.DisplayName;

            var watch = Stopwatch.StartNew();
            bool CanContinue() => watch.ElapsedMilliseconds < TotalBudgetMilliseconds && (isCurrent == null || isCurrent());
            EnvDTE.Debugger? debugger;
            EnvDTE.StackFrame? frame;
            RawBufferVisualizerPackageLog.Write("Handoff naming frame read start");
            try
            {
                debugger = dte.Debugger;
                frame = debugger?.CurrentStackFrame;
                RawBufferVisualizerPackageLog.Write("Handoff naming frame read end");
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
            RawBufferVisualizerPackageLog.Write("Handoff naming locals read start");
            if (!CanContinue() || !AddCompatibleExpressions(candidates, TryGetExpressions(frame, true), expressionSourceType, CanContinue))
                return request.DisplayName;
            RawBufferVisualizerPackageLog.Write("Handoff naming locals read end");
            RawBufferVisualizerPackageLog.Write("Handoff naming arguments read start");
            if (!CanContinue() || !AddCompatibleExpressions(candidates, TryGetExpressions(frame, false), expressionSourceType, CanContinue))
                return request.DisplayName;
            RawBufferVisualizerPackageLog.Write("Handoff naming arguments read end; candidates="
                + candidates.Count.ToString(CultureInfo.InvariantCulture));
            if (candidates.Count == 0)
            {
                return request.DisplayName;
            }

            var identityMatches = new List<EnvDTE.Expression>();
            for (var index = 0; index < candidates.Count; index++)
            {
                if (!CanContinue()) return request.DisplayName;
                int identityHash;
                if (!TryReadIdentityHash(debugger, candidates[index], RemainingTimeout(watch), out identityHash) || !CanContinue())
                    return request.DisplayName;
                if (identityHash == request.ExpressionIdentityHash)
                {
                    identityMatches.Add(candidates[index]);
                }
            }

            var resolved = GetUniqueExpressionName(identityMatches);

            return !CanContinue() || string.IsNullOrWhiteSpace(resolved)
                ? request.DisplayName
                : ApplyExpressionName(resolved!, request.DisplayName);
        }

        private static int RemainingTimeout(Stopwatch watch)
        {
            return (int)Math.Max(1, Math.Min(EvaluationTimeoutMilliseconds, TotalBudgetMilliseconds - watch.ElapsedMilliseconds));
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

        private static bool AddCompatibleExpressions(
            List<EnvDTE.Expression> destination,
            EnvDTE.Expressions? expressions,
            string sourceType,
            Func<bool> canContinue)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (expressions == null)
            {
                return true;
            }

            int count;
            try
            {
                count = expressions.Count;
            }
            catch
            {
                return false;
            }

            if (count > MaximumExpressionsPerScope) return false;
            for (var index = 1; index <= count; index++)
            {
                if (!canContinue()) return false;
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
                    if (destination.Count > MaximumCompatibleExpressions) return false;
                }
            }
            return true;
        }

        private static bool TryReadIdentityHash(
            EnvDTE.Debugger debugger,
            EnvDTE.Expression expression,
            int timeoutMilliseconds,
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
                    timeoutMilliseconds);
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
