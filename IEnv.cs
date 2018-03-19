﻿using System;
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
        Task<ISocket> Listen(int port, TimeSpan timeout);
    }

    public static class ExtendIEnv {
        public static Task<ISocket> Listen(this IEnv env, int port) {
            return env.Listen(port, Timeout.InfiniteTimeSpan);
        }
    }


    public interface IConn : IDisposable {
        Task Write(object message);
        Task<object> Read(TimeSpan timeout);
    }

     sealed class ClientConn : IConn {
        public ClientConn(SimConn conn, SimProc proc) {
            _conn = conn;
            _proc = proc;
        }
        SimConn _conn;
        SimProc _proc;
        public void Dispose() {
            _conn.Dispose();
        }

        public async Task Write(object message) {
            _proc.Instant(message.ToString());
            await _conn.Write(message);
        }

        public async Task<object> Read(TimeSpan timeout) {
            using (_proc.TraceScope("Read")) {
                return await _conn.Read(timeout);
            }
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