using System;
using System.Threading.Tasks;
using SimMach.Playground.CommitLog;

namespace SimMach.Playground.Backend {
    class BackendServer {
        readonly IEnv _env;
        readonly ushort _port;
        readonly CommitLogClient _client;


        public BackendServer(IEnv env, ushort port, CommitLogClient client) {
            _env = env;
            _client = client;
            _port = port;
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
            while (!_env.Token.IsCancellationRequested) {

                var events = await _client.Read(_position, int.MaxValue);

                if (events.Count > 0) {
                    _env.Debug($"Projecting {events.Count} events");
                    _position += events.Count;
                }

                await _env.Delay(100.Ms());
            }
        }

        async Task HandleRequest(IConn conn) {

            using (conn) {
                var req = await conn.Read(5.Sec());
                try {
                    _env.Debug($"{req}");

                    await _env.SimulateWork(35.Ms());

                    switch (req) {
                        case AddItemRequest r:
                            var evt = new ItemAdded(r.ItemID, r.Amount, 0);
                            await _client.Commit(evt);
                            await conn.Write(new AddItemResponse(r.ItemID, r.Amount, 0));
                            break;
                        case MoveItemRequest r:
                            await _client.Commit(
                                new ItemAdded(r.ToItemID, r.Amount, 0),
                                new ItemRemoved(r.FromItemID, r.Amount, 0));
                            await conn.Write(new MoveItemResponse());
                            break;
                        case CountRequest r:
                            await conn.Write(new CountResponse(0));
                            break;


                    }
                } catch (Exception ex) {
                    _env.Debug($"Error while processing {req}: {ex.Message}");
                }
            }
        }
    }
}