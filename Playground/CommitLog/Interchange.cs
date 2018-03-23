using System.Collections.Generic;

namespace SimMach.Playground.CommitLog {
    public sealed class CommitRequest {
        public IList<object> Events;

        public CommitRequest(params object[] events) {
            Events = events;
        }
    }
    
    public sealed class DownloadRequest {
        public readonly int From;
        public readonly int Count;
        public DownloadRequest(int f, int count) {
            From = f;
            Count = count;
        }

        public override string ToString() {
            return $"GET [{From}:{From + Count}]";
        }
    }
}