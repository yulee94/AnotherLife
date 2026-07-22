using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;

namespace AL.ChampionMode.Narrative
{
    public sealed class Nvs01ChampionEncounterAdapter
    {
        private INvs01QuestRuntime _runtime;
        private NvsEncounterRequest _request;
        private NvsEncounterResult _pendingResult;
        private bool _terminalCommitted;
        private Nvs01CommandDisposition _terminalDisposition;

        public bool IsQuestEncounter => _runtime != null && _request != null;
        public bool CanUseFreeRetry => !IsQuestEncounter;
        public NvsEncounterRequest Request => _request;

        public bool TryBind(
            INvs01QuestRuntime runtime,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            if (_pendingResult != null && !_terminalCommitted)
            {
                NvsEncounterRequest pendingRequest = null;
                if (ReferenceEquals(runtime, _runtime) && runtime != null &&
                    runtime.TryGetActiveEncounter(out pendingRequest) && pendingRequest != null &&
                    string.Equals(pendingRequest.CorrelationId, _request.CorrelationId, System.StringComparison.Ordinal))
                {
                    diagnostic = null;
                    return true;
                }

                diagnostic = new Nvs01RuntimeDiagnostic(
                    "EVENT-MISMATCH",
                    "adapter rebind",
                    _request.CorrelationId,
                    pendingRequest?.CorrelationId ?? string.Empty,
                    _request.StateId,
                    _pendingResult.EventId,
                    _request.CorrelationId);
                return false;
            }

            _runtime = null;
            _request = null;
            _pendingResult = null;
            _terminalCommitted = false;
            _terminalDisposition = null;
            diagnostic = null;

            if (runtime == null) return false;

            NvsEncounterRequest request;
            if (!runtime.TryGetActiveEncounter(out request) || request == null) return false;

            _runtime = runtime;
            _request = request;
            return true;
        }

        public Nvs01CommandDisposition PublishSuccess(
            string snapshotVersion = "",
            string snapshotReference = "")
        {
            return Publish(
                NvsEncounterOutcome.Success,
                snapshotVersion,
                snapshotReference);
        }

        public Nvs01CommandDisposition PublishFailure()
        {
            return Publish(NvsEncounterOutcome.Failure, string.Empty, string.Empty);
        }

        public Nvs01CommandDisposition PublishCancelled()
        {
            return Publish(NvsEncounterOutcome.Cancelled, string.Empty, string.Empty);
        }

        public Nvs01CommandDisposition PublishUnavailable()
        {
            return Publish(NvsEncounterOutcome.Unavailable, string.Empty, string.Empty);
        }

        private Nvs01CommandDisposition Publish(
            NvsEncounterOutcome outcome,
            string snapshotVersion,
            string snapshotReference)
        {
            if (_terminalCommitted) return _terminalDisposition;
            if (!IsQuestEncounter) return null;

            // Lock the first result intent even when the commit boundary fails. A
            // retry republishes that exact result; it can never swap failure for
            // success (or vice versa) after observing a transient error.
            if (_pendingResult == null)
            {
                _pendingResult = new NvsEncounterResult(
                    _request.ContractVersion,
                    _request.CorrelationId,
                    _request.QuestId,
                    _request.HookId,
                    _request.RealmId,
                    outcome,
                    _request.GetEventId(outcome),
                    snapshotVersion,
                    snapshotReference);
            }

            var disposition = _runtime.ApplyEncounterResult(_pendingResult);
            if (disposition == null) return null;

            if (disposition.Status == Nvs01CommandStatus.Committed ||
                disposition.Status == Nvs01CommandStatus.Duplicate)
            {
                _terminalDisposition = disposition;
                _terminalCommitted = true;
            }

            return disposition;
        }
    }
}
