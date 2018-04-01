using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {


    public interface IFutureJump {
        bool FutureIsNow { get; }
        TimeSpan Deadline { get; }
        int Id { get; }
    }
    
    public class SimFutureQueue {
        readonly SortedList<long, List<(SimScheduler, object)>>
            _future = new SortedList<long, List<(SimScheduler, object)>>();

        readonly Dictionary<IFutureJump, (SimScheduler,long)> _jumps = new Dictionary<IFutureJump, (SimScheduler,long)>();



        public const int Never = -1;
        
        public void Schedule(SimScheduler id, long pos, object message) {

            if (pos != Never) {
                if (!_future.TryGetValue(pos, out var list)) {
                    list = new List<(SimScheduler, object)>();
                    _future.Add(pos, list);
                }

                list.Add((id, message));
            }

            if (message is IFutureJump f) {
                _jumps.Add(f, (id, pos));
            }
        }

        public void Erase(SimScheduler id) {
            foreach (var list in _future.Values) {
                foreach (var item in list.Where(t => t.Item1 == id)) {
                    if (item.Item2 is SimDelayTask f) {
                        _jumps.Remove(f);
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

                var list = _future.Values[0];
                var time = _future.Keys[0];
                
                if (list.Count == 0) {
                    // we are about to jump to the next time point

                    // check if there are any future tasks
                    var jumps = _jumps
                        .Where(p => p.Key.FutureIsNow)
                        .ToList();

                    // no, move forward
                    if (jumps.Count == 0) {
                        _future.RemoveAt(0);
                        continue;
                    }

                    // order by ID to have some order
                    foreach (var (jump, (sched, pos)) in jumps.OrderBy(p => p.Key.Id)) {
                        // move jumps to now
                        list.Add((sched, jump));
                        // remove from the jump list
                        _jumps.Remove(jump);
                        // remove from the future unless it is present
                        if (pos != time && pos != -1) {
                            if (_future.TryGetValue(pos, out var removal)) {
                                var removed = removal.RemoveAll(tuple => tuple.Item2 == jump);
                                if (removed == 0) {
                                    throw new InvalidOperationException($"Didn't find jump at pos {pos}");
                                }    
                            }
                            
                        }

                    }
                }

                
                var (scheduler, subject) = list.First();
                list.RemoveAt(0);
                item = new FutureItem(time, scheduler, subject);
                return true;
            }
        }
    }

    public struct FutureItem {
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