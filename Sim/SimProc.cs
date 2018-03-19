using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {

   
    
    class SimProc : IEnv {
        readonly int _procId;
        readonly TaskFactory _scheduler;
        public readonly ServiceId Id;
        readonly SimRuntime Runtime;
        

        readonly CancellationTokenSource _cts;

        public CancellationToken Token => _cts.Token;




       
        
        
        public Task Delay(int i, CancellationToken token ) {
            return SimDelayTask.Delay(i, token);
        }
        public Task Delay(TimeSpan i, CancellationToken token) {
            return SimDelayTask.Delay(i, token);
        }

        public SimFuture<T> Promise<T>(TimeSpan timeout, CancellationToken token) {
            return new SimFuture<T>(timeout, token);
        }

        public async Task SimulateWork(string name,TimeSpan ms, CancellationToken token) {
            Runtime.RecordActivity();
            using (TraceScope(name)) {
                await SimDelayTask.Delay(ms, token);
            }

            Runtime.RecordActivity();
        }

        public SimProc(ServiceId id, SimRuntime runtime, int procId, TaskFactory scheduler) {
            _cts = new CancellationTokenSource();
            Id = id;
            Runtime = runtime;
            _procId = procId;
            _scheduler = scheduler;
        }

        public void Schedule(Func<Task> action) {
            _scheduler.StartNew(action);
        }

        public void Cancel() {
            // issues a soft cancel token
            _cts.Cancel();
        }

        bool _killed;

        public void Kill() {
            _killed = true;
        }

        public TimeSpan Time => Runtime.Time;
        
        public async Task<IConn> Connect(string endpoint, int port) {
            var server = new SimEndpoint(endpoint, port);

            using (TraceScope($"Connect to {server}")) {
                return await Runtime.Connect(this, server);
            }

        }

        public async Task<ISocket> Listen(int port, TimeSpan timeout) {
            using (Runtime.Tracer.SyncScope(_procId, "Bind", "proc")) {
                return await Runtime.Bind(this, port, timeout);
            }
        }


        public void Debug(string l) {
            Runtime.Debug($"  {Id.Machine,-13} {Id.Service,-20} {l}");
        }

        public void Instant(string message) {
            Runtime.Tracer.Instant(_procId, message, "proc");
        }

        public void FlowStart(string flow, string id) {
            Runtime.Tracer.FlowStart(_procId, flow, "proc", id);
        }
        
        public void FlowEnd(string flow, string id) {
            Runtime.Tracer.FlowEnd(_procId, flow, "proc", id);
        }
        
        public TracePoint TraceScope(string name) {
            return Runtime.Tracer.SyncScope(_procId, name, "proc");
        }

    }
}