using System;
using System.Collections.Generic;
using System.Linq;

namespace SimMach {
    class Future {
        readonly SortedList<long, List<(Scheduler, object)>>
            _future = new SortedList<long, List<(Scheduler, object)>>();


        public void Schedule(Scheduler id, long pos, object message) {

            if (!_future.TryGetValue(pos, out var list)) {
                list = new List<(Scheduler, object)>();
                _future.Add(pos, list);
            }

            list.Add((id, message));
        }


        public bool TryGetFuture(out FutureItem item) {

            while (true) {
                if (_future.Count == 0) {
                    item = default(FutureItem);
                    return false;
                }

                var list = _future.Values.First();
                if (list.Count == 0) {
                    _future.RemoveAt(0);
                    continue;
                }

                var time = _future.Keys[0];
                var o = list.First();
                list.RemoveAt(0);

                item = new FutureItem(time, o.Item1, o.Item2);
                return true;
            }
        }
    }

    struct FutureItem {
        public readonly long Time;
        public readonly Scheduler Scheduler;
        public readonly object Item;

        public FutureItem(long time, Scheduler scheduler, object item) {
            Time = time;
            Scheduler = scheduler;
            Item = item;
        }
    }
}