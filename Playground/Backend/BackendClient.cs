using System;
using System.IO;
using System.Threading.Tasks;
using SimMach.Sim;

namespace SimMach.Playground.Backend {
    public sealed class BackendClient {
        readonly IEnv _env;
        readonly SimEndpoint[] _endpoints;
        readonly TimeSpan[] _outages;
        static readonly TimeSpan _downtime = TimeSpan.FromSeconds(15);

        public BackendClient(IEnv env, params SimEndpoint[] endpoints) {
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