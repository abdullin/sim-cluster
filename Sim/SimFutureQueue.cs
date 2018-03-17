using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {
    
    class SimFutureQueue {
        readonly SortedList<long, List<(SimScheduler, object)>>
            _future = new SortedList<long, List<(SimScheduler, object)>>();

        readonly Dictionary<SimFutureTask, (SimScheduler,long)> _cancellable = new Dictionary<SimFutureTask, (SimScheduler,long)>();


        
        
        public void Schedule(SimScheduler id, long pos, object message) {

            if (!_future.TryGetValue(pos, out var list)) {
                list = new List<(SimScheduler, object)>();
                _future.Add(pos, list);
            }

            list.Add((id, message));

            if (message is SimFutureTask f) {
                // TODO: we can add cancel registration 
                // instead of manually searching
                _cancellable.Add(f, (id,pos));
            }
        }

        public void Erase(SimScheduler id) {
            foreach (var list in _future.Values) {
                foreach (var item in list.Where(t => t.Item1 == id)) {
                    if (item.Item2 is SimFutureTask f) {
                        _cancellable.Remove(f);
                    }
                }
                list.RemoveAll(t => t.Item1 == id);
            }
        }

        public bool TryGetFuture(out FutureItem item) {

            while (true) {
                if (_future.Count == 0) {
                    // no more future
                    item = default(FutureItem);
                    return false;
                }

                var list = _future.Values.First();
                if (list.Count == 0) {
                    // we are about to jump to the next time point

                    // check if there are any cancellable future tasks
                    var cancels = _cancellable
                        .Where(p => p.Key.Token.IsCancellationRequested)
                        .ToList();

                    // no, move forward
                    if (cancels.Count == 0) {
                        _future.RemoveAt(0);
                        continue;
                    }

                    // order by ID to have some order
                    foreach (var (future, (sched, _)) in cancels.OrderBy(p => p.Key.Id)) {
                        // move denied future to now
                        list.Add((sched, future));
                        _cancellable.Remove(future);
                        // we could technically also remove the task from the future
                        // however, since it will already be faulted, we don't care
                    }
                }

                var time = _future.Keys[0];
                var (scheduler, subject) = list.First();
                list.RemoveAt(0);
                item = new FutureItem(time, scheduler, subject);
                return true;
            }
        }
    }

    struct FutureItem {
        public readonly long Time;
        public readonly SimScheduler Scheduler;
        public readonly object Item;

        public FutureItem(long time, SimScheduler scheduler, object item) {
            Time = time;
            Scheduler = scheduler;
            Item = item;
        }
    }
}