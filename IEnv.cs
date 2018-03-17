using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach {
    public interface IEnv {
        void Debug(string starting);
        CancellationToken Token { get; }
        Task Delay(int i, CancellationToken token = default (CancellationToken));
        Task Delay(TimeSpan i, CancellationToken token = default (CancellationToken));
        Task SimulateWork(int ms, CancellationToken token = default (CancellationToken));
        
        TimeSpan Time { get; }
    }
    
    
    public interface ISimPlan {
        void StartServices(Predicate<ServiceId> selector = null);
        Task StopServices(Predicate<ServiceId> selector = null, int grace = 5000);
        void Debug(string message);
        Task Delay(int i);
        Task Delay(TimeSpan i);
    }
}