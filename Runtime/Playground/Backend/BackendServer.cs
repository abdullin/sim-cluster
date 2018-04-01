using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LightningDB;
using NUnit.Framework.Constraints;
using SimMach.Playground.CommitLog;

namespace SimMach.Playground.Backend {
    class BackendServer : IEngine {
        readonly IEnv _env;
        readonly ushort _port;
        readonly CommitLogClient _client;
        
        
        readonly Store _store;
        readonly Projection _proj;

        readonly LightningEnvironment _le;
        readonly LightningDatabase _ld;


        public BackendServer(IEnv env, ushort port, CommitLogClient client) {
            _env = env;
            _client = client;
            _port = port;

            _le = env.GetDatabase("db");
            _le.Open(EnvironmentOpenFlags.MapAsync);

            using (var tx = _le.BeginTransaction()) {
                _ld = tx.OpenDatabase(configuration: new DatabaseConfiguration() {Flags = DatabaseOpenFlags.Create});
                tx.Commit();
            }
            
            _store = new Store(_le, _ld);
            _proj = new Projection(_store);
        }

        public Task Run() {
            return Task.WhenAll(EventLoop(), ProjectionThread());
        }

        async Task EventLoop() {

            try {
                using (var socket = await _env.Bind(_port)) {
                    while (!_env.Token.IsCancellationRequested) {
                        var conn = await socket.Accept();
                        HandleRequest(conn);
                    }
                }
            } catch (TaskCanceledException) {
                return;
            }
        }

        

        async Task ProjectionThread() {
            // TODO: deduplication
            while (!_env.Token.IsCancellationRequested) {

                try {
                    var position = _store.GetCounter();
                    var events = await _client.Read(position, int.MaxValue);

                    if (events.Count > 0) {
                        //_env.Debug($"Projecting {events.Count} events");
                        foreach (var e in events) {
                            _proj.Dispatch(e);
                        }

                        position += events.Count;
                        _store.SetCounter(position);
                    }

                    await _env.Delay(100.Ms());
                } catch (TaskCanceledException) {
                    // nothing
                    return;
                } catch (Exception ex) {
                    _env.Warning($"Projection error: {ex.Message}");
                }
            }
        }

        async Task Commit(params object[] events) {
            foreach (var e in events) {
                _proj.Dispatch(e);
            }

            await _client.Commit(events);

        }

        async Task HandleRequest(IConn conn) {

            using (conn) {
                var req = await conn.Read(5.Sec());
                try {
                    _env.Debug($"{req}");

                    await _env.SimulateWork(35.Ms());

                    switch (req) {
                        case AddItemRequest r:
                            await AddItem(conn, r);
                            break;
                        case MoveItemRequest r:
                            await MoveItem(conn, r);
                            break;
                        case CountRequest r:
                            var amount = _store.Count();
                            await conn.Write(new CountResponse(amount));
                            break;
                    }
                } catch (Exception ex) {
                    _env.Error($"Error while processing {req}", ex);
                }
            }
        }

        async Task MoveItem(IConn conn, MoveItemRequest moveItemRequest) {
            var wasFrom = _store.GetItemQuantity(moveItemRequest.FromItemID);
            var wasTo = _store.GetItemQuantity(moveItemRequest.ToItemID);
            if (wasFrom < moveItemRequest.Amount) {
                await conn.Write(new ArgumentException("Insufficient amount"));
                return;
            }

            await Commit(
                new ItemAdded(moveItemRequest.ToItemID, moveItemRequest.Amount, wasTo + moveItemRequest.Amount),
                new ItemRemoved(moveItemRequest.FromItemID, moveItemRequest.Amount, wasFrom - moveItemRequest.Amount));
            await conn.Write(new MoveItemResponse());
        }

        async Task AddItem(IConn conn, AddItemRequest addItemRequest) {
            var quantity = _store.GetItemQuantity(addItemRequest.ItemID);
            var total = quantity + addItemRequest.Amount;
            var evt = new ItemAdded(addItemRequest.ItemID, addItemRequest.Amount, total);
            await Commit(evt);
            await conn.Write(new AddItemResponse(addItemRequest.ItemID, addItemRequest.Amount, total));
        }

        public Task Dispose() {
            _ld.Dispose();
            _le.Dispose();
            
            return Task.CompletedTask;
            
        }
    }
}