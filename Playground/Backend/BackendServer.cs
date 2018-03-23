using System;
using System.Threading.Tasks;
using NUnit.Framework.Constraints;
using SimMach.Playground.CommitLog;

namespace SimMach.Playground.Backend {
    class BackendServer {
        readonly IEnv _env;
        readonly ushort _port;
        readonly CommitLogClient _client;
        
        
        readonly Db _db;
        readonly Projection _proj;


        public BackendServer(IEnv env, ushort port, CommitLogClient client) {
            _env = env;
            _client = client;
            _port = port;


            _db = new Db();
            _proj = new Projection(_db);
        }

        public Task Run() {
            return Task.WhenAll(EventLoop(), ProjectionThread());
        }

        async Task EventLoop() {

            using (var socket = await _env.Bind(_port)) {
                while (!_env.Token.IsCancellationRequested) {
                    var conn = await socket.Accept();
                    HandleRequest(conn);
                }
            }
        }

        int _position;

        async Task ProjectionThread() {
            // TODO: deduplication
            while (!_env.Token.IsCancellationRequested) {

                var events = await _client.Read(_position, int.MaxValue);
                
                if (events.Count > 0) {
                    _env.Debug($"Projecting {events.Count} events");
                    foreach (var e in events) {
                        _proj.Dispatch(e);
                    }    
                    _position += events.Count;
                }

                

                

                await _env.Delay(100.Ms());
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
                            var amount = _db.Count();
                            await conn.Write(new CountResponse(amount));
                            break;
                    }
                } catch (Exception ex) {
                    _env.Halt($"Error while processing {req}: {ex.Message}");
                }
            }
        }

        async Task MoveItem(IConn conn, MoveItemRequest moveItemRequest) {
            var wasFrom = _db.GetItemQuantity(moveItemRequest.FromItemID);
            var wasTo = _db.GetItemQuantity(moveItemRequest.ToItemID);
            if (wasFrom < moveItemRequest.Amount) {
                await conn.Write(new ArgumentException("Insufficient"));
                return;
            }

            await Commit(
                new ItemAdded(moveItemRequest.ToItemID, moveItemRequest.Amount, wasTo + moveItemRequest.Amount),
                new ItemRemoved(moveItemRequest.FromItemID, moveItemRequest.Amount, wasFrom - moveItemRequest.Amount));
            await conn.Write(new MoveItemResponse());
        }

        async Task AddItem(IConn conn, AddItemRequest addItemRequest) {
            var quantity = _db.GetItemQuantity(addItemRequest.ItemID);
            var total = quantity + addItemRequest.Amount;
            var evt = new ItemAdded(addItemRequest.ItemID, addItemRequest.Amount, total);
            await Commit(evt);
            await conn.Write(new AddItemResponse(addItemRequest.ItemID, addItemRequest.Amount, total));
        }
    }
}