using System;
using System.IO;
using System.Threading;

namespace RawBufferVisualizer.VisualStudio
{
    public sealed class ClaimedHandoffOpenResult
    {
        private ClaimedHandoffOpenResult(bool succeeded, string failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }

        public static ClaimedHandoffOpenResult Success()
        {
            return new ClaimedHandoffOpenResult(true, string.Empty);
        }

        public static ClaimedHandoffOpenResult Failure(string reason)
        {
            return new ClaimedHandoffOpenResult(
                false,
                string.IsNullOrWhiteSpace(reason)
                    ? "The handoff could not be opened."
                    : reason);
        }
    }

    public sealed class ClaimedHandoffOpenOutcome
    {
        internal ClaimedHandoffOpenOutcome(
            bool succeeded,
            VisualizerHandoffRequest? request,
            string failureReason,
            Exception? exception)
        {
            Succeeded = succeeded;
            Request = request;
            FailureReason = failureReason ?? string.Empty;
            Exception = exception;
        }

        public bool Succeeded { get; private set; }
        public VisualizerHandoffRequest? Request { get; private set; }
        public string FailureReason { get; private set; }
        public Exception? Exception { get; private set; }
    }

    public sealed class ClaimedHandoffOpenCoordinator
    {
        private const int ReadAttemptCount = 10;
        private const int ReadRetryDelayMilliseconds = 50;

        private readonly Func<string, VisualizerHandoffRequest> _readRequest;
        private readonly Func<string, string, bool> _acknowledgeRequest;
        private readonly Func<string, string, string, bool> _rejectRequest;
        private readonly Func<string, VisualizerHandoffRequestState> _getRequestState;
        private readonly Action<string> _log;

        public ClaimedHandoffOpenCoordinator(Action<string> log)
            : this(
                VisualizerHandoffInbox.ReadSnapshotRequestInfo,
                VisualizerHandoffInbox.TryAcknowledgeRequest,
                VisualizerHandoffInbox.TryRejectRequest,
                VisualizerHandoffInbox.GetRequestState,
                log)
        {
        }

        internal ClaimedHandoffOpenCoordinator(
            Func<string, VisualizerHandoffRequest> readRequest,
            Func<string, string, bool> acknowledgeRequest,
            Func<string, string, string, bool> rejectRequest,
            Func<string, VisualizerHandoffRequestState> getRequestState,
            Action<string> log)
        {
            _readRequest = readRequest ?? throw new ArgumentNullException("readRequest");
            _acknowledgeRequest = acknowledgeRequest ?? throw new ArgumentNullException("acknowledgeRequest");
            _rejectRequest = rejectRequest ?? throw new ArgumentNullException("rejectRequest");
            _getRequestState = getRequestState ?? throw new ArgumentNullException("getRequestState");
            _log = log ?? delegate { };
        }

        public ClaimedHandoffOpenOutcome Open(
            string requestPath,
            string processingPath,
            Func<VisualizerHandoffRequest, ClaimedHandoffOpenResult> openRequest)
        {
            if (openRequest == null)
            {
                throw new ArgumentNullException("openRequest");
            }

            VisualizerHandoffRequest? request = null;
            try
            {
                request = ReadRequestWithRetry(processingPath);
                var openResult = openRequest(request)
                    ?? ClaimedHandoffOpenResult.Failure(
                        "The handoff opener returned no result.");
                if (!openResult.Succeeded)
                {
                    TryRejectOrLog(requestPath, processingPath, openResult.FailureReason);
                    return new ClaimedHandoffOpenOutcome(
                        false,
                        request,
                        openResult.FailureReason,
                        null);
                }

                if (_acknowledgeRequest(requestPath, processingPath)
                    || _getRequestState(requestPath)
                        == VisualizerHandoffRequestState.Acknowledged)
                {
                    return new ClaimedHandoffOpenOutcome(
                        true,
                        request,
                        string.Empty,
                        null);
                }

                throw new IOException(
                    "The handoff opened, but its acknowledgement marker could not be published.");
            }
            catch (Exception ex)
            {
                TryRejectOrLog(requestPath, processingPath, ex.ToString());
                return new ClaimedHandoffOpenOutcome(
                    false,
                    request,
                    ex.Message,
                    ex);
            }
        }

        private VisualizerHandoffRequest ReadRequestWithRetry(string processingPath)
        {
            Exception? last = null;
            for (var attempt = 0; attempt < ReadAttemptCount; attempt++)
            {
                try
                {
                    return _readRequest(processingPath);
                }
                catch (Exception ex) when (
                    ex is IOException
                    || ex is UnauthorizedAccessException)
                {
                    last = ex;
                    if (attempt + 1 < ReadAttemptCount)
                    {
                        Thread.Sleep(ReadRetryDelayMilliseconds);
                    }
                }
            }

            throw last ?? new IOException("Handoff request could not be read.");
        }

        private void TryRejectOrLog(
            string requestPath,
            string processingPath,
            string reason)
        {
            if (_rejectRequest(requestPath, processingPath, reason))
            {
                return;
            }

            VisualizerHandoffRequestState state;
            try
            {
                state = _getRequestState(requestPath);
            }
            catch (Exception ex)
            {
                _log("Handoff rejection state read failed " + requestPath + " " + ex);
                return;
            }

            if (state != VisualizerHandoffRequestState.Rejected
                && state != VisualizerHandoffRequestState.Acknowledged)
            {
                _log(
                    "Handoff rejection marker publish failed "
                    + requestPath
                    + " state "
                    + state);
            }
        }
    }
}
