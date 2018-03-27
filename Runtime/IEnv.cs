using System;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using LightningDB;
using SimMach.Sim;

namespace SimMach {
    public interface IEnv {
        void Debug(string starting);
        CancellationToken Token { get; }
        Task Delay(TimeSpan i, CancellationToken token = default(CancellationToken));
        Task SimulateWork(TimeSpan ms, CancellationToken token = default(CancellationToken));

        TimeSpan Time { get; }
        Task<IConn> Connect(SimEndpoint endpoint);
        Task<ISocket> Bind(ushort port, TimeSpan timeout);

        void Halt(string message, Exception error = null);

        LightningEnvironment GetDatabase(string name);
    }

    public static class ExtendIEnv {
        public static Task<ISocket> Bind(this IEnv env, ushort port) {
            return env.Bind(port, Timeout.InfiniteTimeSpan);
        }

        public static Task<IConn> Connect(this IEnv env,string server, ushort port) {
            return env.Connect(new SimEndpoint(server, port));
        }
    }


    public interface IConn : IDisposable {
        Task Write(object message);
        Task<object> Read(TimeSpan timeout);
        SimEndpoint RemoteAddress { get; }
    }


   

    public interface IFuture<T> {
        void SetResult(T result);
        void SetError(Exception ex);
        Task<T> Task { get; }
    }
    
    public interface ISimPlan {
        void StartServices(Predicate<ServiceId> selector = null);
        Task StopServices(Predicate<ServiceId> selector = null, TimeSpan? grace = null);
        void WipeStorage(string machine);
        void Debug(string message);
        Task Delay(int i);
        Task Delay(TimeSpan i);
        TimeSpan Time { get; }
    }
}