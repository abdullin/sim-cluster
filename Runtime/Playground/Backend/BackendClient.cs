using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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


        IEnumerable<(int, TimeSpan, bool)> GetEndpoints() {
            var now = _env.Time;
            var attempts = 4;
            for (int i = 0; i < _endpoints.Length; i++) {
                var endpoint = _endpoints[i];
                if (_outages[i] > now) {
                    continue;
                }
                
                

                yield return (i, TimeSpan.Zero, false);
                yield return (i, TimeSpan.Zero, true);
            }
            


        } 
            
            
         async Task<TResponse> Unary<TRequest, TResponse>(TRequest req) {
            var now = _env.Time;

             foreach (var (i, ts, mark) in GetEndpoints()) {
                 var endpoint = _endpoints[i];
                 
                 _env.Debug($"Send '{req}' to {endpoint}");

                 if (ts != TimeSpan.Zero) {
                     await _env.Delay(ts, _env.Token);
                 }

                 try {
                     using (var conn = await _env.Connect(endpoint)) {
                         await conn.Write(req);
                         var res = await conn.Read(5.Sec());

                         if (res is ArgumentException ex) {
                             throw new ArgumentException(ex.Message);
                         }
                        
                         return (TResponse) res;
                     }
                 } catch (IOException ex) {
                     if (!mark) {
                         _env.Warning($"! {ex.Message} for '{req}'. Retrying {endpoint}");
                         continue;
                     }
                     
                     
                     if (_outages[i] > now) {
                         _env.Warning($"! {ex.Message} for '{req}'. {endpoint} already DOWN");
                     } else {
                         _env.Warning($"! {ex.Message} for '{req}'. {endpoint} DOWN");
                         _outages[i] = now + _downtime;
                     }
                 }
                 
             }
            
            // we've exhausted all gateways.
            
            
            throw new IOException("No gateways active");
        }
    }
}