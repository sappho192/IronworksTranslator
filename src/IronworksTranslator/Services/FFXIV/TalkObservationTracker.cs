using Sharlayan.Models.ReadResults;

namespace IronworksTranslator.Services.FFXIV
{
    public sealed class TalkObservationTracker
    {
        private readonly object _syncRoot = new();
        private bool _hasSnapshot;
        private bool _wasCurrentVisible;
        private string _speaker = string.Empty;
        private string _text = string.Empty;

        public bool ShouldEnqueue(TalkResult snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            lock (_syncRoot)
            {
                if (!snapshot.IsAvailable)
                {
                    return false;
                }

                var isCurrentVisible = snapshot.Source == TalkSource.Current && snapshot.IsVisible;
                var isFirstSnapshot = !_hasSnapshot;
                var pairChanged = isFirstSnapshot
                    || !string.Equals(_speaker, snapshot.Name, StringComparison.Ordinal)
                    || !string.Equals(_text, snapshot.Text, StringComparison.Ordinal);
                var reopened = !isFirstSnapshot && isCurrentVisible && !_wasCurrentVisible;

                _hasSnapshot = true;
                _speaker = snapshot.Name;
                _text = snapshot.Text;
                _wasCurrentVisible = isCurrentVisible;

                if (string.IsNullOrEmpty(snapshot.Text) || !isCurrentVisible)
                {
                    return false;
                }

                return isFirstSnapshot || pairChanged || reopened;
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _hasSnapshot = false;
                _wasCurrentVisible = false;
                _speaker = string.Empty;
                _text = string.Empty;
            }
        }
    }
}
