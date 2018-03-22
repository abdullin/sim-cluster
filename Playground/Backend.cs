using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SimMach.Sim {
    class Backend {
        readonly IEnv _env;
        readonly ushort _port;
        readonly CommitLogClient _client;


        public Backend(IEnv env, ushort port, CommitLogClient client) {
            _env = env;
            _client = client;
            _port = port;
        }

        public Task Run() {
            return Task.WhenAll(EventLoop(), ProjectionThread());
        }

        public async Task EventLoop() {

            using (var socket = await _env.Bind(_port)) {
                while (!_env.Token.IsCancellationRequested) {
                    var conn = await socket.Accept();
                    HandleRequest(conn);
                }
            }
        }

        int _position;

        public async Task ProjectionThread() {
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

    public sealed class AddItemRequest {
        public readonly long ItemID;
        public readonly decimal Amount;

        public AddItemRequest(long itemId, decimal amount) {
            ItemID = itemId;
            Amount = amount;
        }

        public override string ToString() {
            return $"Add {Amount} to {ItemID}";
        }
    }
    
    
    public sealed class MoveItemRequest {
        public readonly long FromItemID;
        public readonly long ToItemID;
        public readonly decimal Amount;

        public MoveItemRequest(long fromItemId, long toItemId, decimal amount) {
            FromItemID = fromItemId;
            ToItemID = toItemId;
            Amount = amount;
        }
    }

    public sealed class MoveItemResponse {
     
        
    }

    


    public sealed class AddItemResponse {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public AddItemResponse(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }

        public override string ToString() {
            return $"ItemAdded: {Amount} to {ItemID}";
        }
    }

    public sealed class ItemAdded {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public ItemAdded(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }


        public override string ToString() {
            return $"ItemAdded: {Amount} to {ItemID}";
        }
    }

    public sealed class ItemRemoved {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public ItemRemoved(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }
        public override string ToString() {
            return $"ItemRemoved: {Amount} from {ItemID}";
        }
    }
    


    public sealed class CountRequest {
        
    }

    public sealed class CountResponse {
        public decimal Count;
        public CountResponse(decimal count) {
            Count = count;
        }
    }

    public sealed class ClientLib {
        readonly IEnv _env;
        readonly SimEndpoint[] _endpoints;
        readonly TimeSpan[] _outages;
        static readonly TimeSpan _downtime = TimeSpan.FromSeconds(15);

        public ClientLib(IEnv env, params SimEndpoint[] endpoints) {
            _env = env;
            _endpoints = endpoints;
            _outages = new TimeSpan[endpoints.Length];
        }
        
        public async Task<decimal> AddItem(long id, decimal amount) {
            var request = new AddItemRequest(id, amount);
            var response = await Unary<AddItemRequest,AddItemResponse>(request);
            return response.Amount;
        }

        public async Task MoveItem(long from, long to, decimal amount) {
            var req = new MoveItemRequest(from, to, amount);
            await Unary<MoveItemRequest, MoveItemResponse>(req);
        }

        public async Task<decimal> Count() {
            var req = new CountRequest();
            var resp = await Unary<CountRequest, CountResponse>(req);
            return resp.Count;
        }

        async Task<TResponse> Unary<TRequest, TResponse>(TRequest req) {
            var now = _env.Time;
            for (int i = 0; i < _endpoints.Length; i++) {
                var endpoint = _endpoints[i];
                if (_outages[i] > now) {
                    continue;
                }
                _env.Debug($"Send '{req}' to {endpoint}");

                try {
                    using (var conn = await _env.Connect(endpoint)) {
                        await conn.Write(req);
                        var res = await conn.Read(5.Sec());
                        return (TResponse) res;
                    }
                } catch (IOException ex) {
                    if (_outages[i] > now) {
                        _env.Debug($"! {ex.Message} for '{req}'. {endpoint} already DOWN");
                    } else {
                        _env.Debug($"! {ex.Message} for '{req}'. {endpoint} DOWN");
                        _outages[i] = now + _downtime;
                    }
                }
            }
            throw new IOException("No gateways active");
        }


        
    }
}