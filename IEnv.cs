using System;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach {
    public interface IEnv {
        void Debug(string starting);
        CancellationToken Token { get; }
        Task Delay(int i, CancellationToken token = default(CancellationToken));
        Task Delay(TimeSpan i, CancellationToken token = default(CancellationToken));
        Task SimulateWork(int ms, CancellationToken token = default(CancellationToken));

        TimeSpan Time { get; }
        Task<IConn> Connect(string endpoint, int port);
        Task<IConn> Listen(int port, TimeSpan timeout);
    }

    public static class ExtendIEnv {
        public static Task<IConn> Listen(this IEnv env, int port) {
            return env.Listen(port, Timeout.InfiniteTimeSpan);
        }
    }


    public interface IConn : IDisposable {
        Task Write(object message);
        Task<object> Read(TimeSpan timeout);
    }

    public static class ExtendIConn {
        public static Task<object> Read(this IConn conn) {
            return conn.Read(Timeout.InfiniteTimeSpan);
        }

        public static IAsyncEnumerable<object> ReadStream(this IConn conn) {
            return new RequestStream(conn);
        }
        
        
    }


    public interface IAsyncEnumerable<T> : IDisposable{
        Task<bool> MoveNext();
        T Current { get; }
    }

    public sealed class RequestStream : IAsyncEnumerable<object> {
        readonly IConn _conn;

        public RequestStream(IConn conn) {
            _conn = conn;
        }

        public async Task<bool> MoveNext() {
            // TODO: timeout + cancel
            Current = await _conn.Read();
            if (Current == null) {
                Current = null;
                return false;
            }

            if (Current is SimHeaders h && h.EndStream) {
                Current = null;
                return false;
            }

            return true;
        }

        public object Current { get; private set; }

        public void Dispose() {
            
        }
    }


    public interface IFuture<T> {
        void SetResult(T result);
        void SetError(Exception ex);
        Task<T> Task { get; }
    }
    
    public interface ISimPlan {
        void StartServices(Predicate<ServiceId> selector = null);
        Task StopServices(Predicate<ServiceId> selector = null, int grace = 5000);
        void Debug(string message);
        Task Delay(int i);
        Task Delay(TimeSpan i);
        TimeSpan Time { get; }
    }
}