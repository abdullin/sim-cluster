﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimMach.Sim {

    class SimProc : IEnv, IDisposable {
        readonly int _procId;
        readonly TaskFactory _scheduler;
        public readonly ServiceId Id;
        readonly SimMachine _machine;

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
            _machine = machine;
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

        public TimeSpan Time => _machine.Time;

        public async Task<IConn> Connect(SimEndpoint endpoint) {
            return await _machine.Connect(this, endpoint);
        }

        public async Task<ISocket> Bind(ushort port, TimeSpan timeout) {
            return await _machine.Bind(this, port, timeout);
        }

        public void Halt(string message) {
            Debug(message);
            _machine.Runtime.Halt(message);
        }

        public string GetLocalFolder(string name) {
            return _machine.GetServiceFolder(Id.Service, name);
        }

        public void Debug(string l) {
            _machine.Debug($"  {Id.Machine,-13} {l}");
        }

        readonly Dictionary<ushort, SimSocket> _sockets = new Dictionary<ushort, SimSocket>();

        public void RegisterSocket(SimSocket proc) {
            _sockets.Add(proc.Endpoint.Port, proc);
        }

        public void ReleaseSocket(ushort port) {
            _machine.ReleaseSocket(port);
            _sockets.Remove(port);
        }

        public void Dispose() {
            foreach (var (_, sock) in _sockets) {
                ReleaseSocket(sock.Endpoint.Port);
            }
        }
    }
}