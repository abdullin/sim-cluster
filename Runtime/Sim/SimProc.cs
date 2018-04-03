using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LightningDB;

namespace SimMach.Sim {

    public class SimProc : IEnv, IDisposable {
        readonly int _procId;
        readonly TaskFactory _scheduler;
        public readonly ServiceId Id;
        public readonly SimMachine Machine;

        readonly Stack<IDisposable> _simulationResources = new Stack<IDisposable>();

        readonly CancellationTokenSource _cts;
        public CancellationToken Token => _cts.Token;


        public Task Delay(TimeSpan i, CancellationToken token) {
            return SimDelayTask.Delay(i, token);
        }

        public SimFuture<T> Promise<T>(TimeSpan timeout, CancellationToken token) {
            return new SimFuture<T>(timeout, token);
        }

        public async Task SimulateWork(TimeSpan ms, CancellationToken token) {
            //_machine.RecordActivity();
            await SimDelayTask.Delay(ms, token);
            //_machine.RecordActivity();
        }

        public SimProc(ServiceId id, SimMachine machine, int procId, TaskFactory scheduler) {
            _cts = new CancellationTokenSource();
            Id = id;
            Machine = machine;
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

        public TimeSpan Time => Machine.Time;

        public async Task<IConn> Connect(SimEndpoint endpoint) {
            return await Machine.Connect(this, endpoint);
        }

        public async Task<ISocket> Bind(ushort port, TimeSpan timeout) {
            return await Machine.Bind(this, port, timeout);
        }

        public void Halt(string message, Exception error) {
            DebugInner(LogType.Error, message);
            Machine.Runtime.Halt(message, error);
        }

        public void Error(string message, Exception error) {
            DebugInner(LogType.Error, message + ": " + error.Demystify().Message);
        }
        
        
        
        public void Debug(string l) {
            DebugInner(LogType.Info,l);
        }
        
        
        public void Warning(string l) {
            DebugInner(LogType.Warning,l);
        }

        public LightningEnvironment GetDatabase(string name) {
            var folder = Machine.GetServiceFolder(Id.Service, name);
            var db = new LightningEnvironment(folder);
            _simulationResources.Push(db);
            return db;
        }




        void DebugInner(LogType logType, string l) {
            Machine.Debug(logType, $"  {Id.Machine,-13} {l}");
        }

        readonly Dictionary<ushort, SimSocket> _sockets = new Dictionary<ushort, SimSocket>();

        public void RegisterSocket(SimSocket proc) {
            _sockets.Add(proc.Endpoint.Port, proc);
        }

        public void ReleaseSocket(ushort port) {
            Machine.ReleaseSocket(port);
            _sockets.Remove(port);
        }

        public void Dispose() {
            foreach (var (port,_) in _sockets) {
                Machine.ReleaseSocket(port);
            }
            _sockets.Clear();

            while (_simulationResources.TryPop(out var disposable)) {
                disposable.Dispose();
            }
        }
    }
}