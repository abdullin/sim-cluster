using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SimMach.Sim {
    public sealed class CommitLog {



        readonly IEnv _env;
        readonly ushort _port;

        public CommitLog(IEnv env, ushort port) {
            _env = env;
            _port = port;
        }

        readonly List<object> _stored = new List<object>();
        readonly Queue<object> _buffer = new Queue<object>();
        
        public async Task Run() {
            using (var socket = await _env.Bind(_port)) {
                while (!_env.Token.IsCancellationRequested) {
                    var conn = await socket.Accept();
                    HandleAsync(conn);
                }
            }    
        }
        
        
        
        TimeSpan _scheduled = TimeSpan.MinValue;

        async Task HandleAsync(IConn conn) {
            try {
                using (conn) {
                    var req = await conn.Read(5.Sec());

                    switch (req) {
                        case DownloadRequest dr:
                            await conn.Write(_stored.Skip(dr.From).Take(dr.Count).ToList());
                            return;
                        default:
                            await _env.SimulateWork(5.Ms());

                            _buffer.Enqueue(req);
                            ScheduleStore();
                            await conn.Write("OK");

                            return;
                    }
                }
            } catch (Exception ex) {
                _env.Debug($"Error: {ex.Message}");
            }
        }
        
        
        async Task ScheduleStore() {
            var next = TimeSpan.FromSeconds(Math.Ceiling(_env.Time.TotalSeconds * 2) / 2);
            if (_scheduled != next) {
                _scheduled = next;
                await _env.Delay(_scheduled - _env.Time);
                if (_buffer.Count > 0) {
                    await _env.SimulateWork((5 * _buffer.Count + 10).Ms());
                    _stored.AddRange(_buffer);

                    _env.Debug($"Store {_buffer.Count} events");
                    _buffer.Clear();
                }
            }
        }
        
        
       
    }

    public sealed class CommitLogClient {
        IEnv _env;
        SimEndpoint _endpoint;

        public CommitLogClient(IEnv env, SimEndpoint endpoint) {
            _env = env;
            _endpoint = endpoint;
        }

        public async Task Commit(object e) {
            _env.Debug($"Commit '{e}' to {_endpoint}");
            using (var conn = await _env.Connect(_endpoint)) {
                await conn.Write(e);
                await conn.Read(5.Sec());
            }
        }

        public async Task<IList<object>> Read(int from, int count) {
            using (var conn = await _env.Connect(_endpoint)) {
                await conn.Write(new DownloadRequest(from, count));
                var result = await conn.Read(5.Sec());
                return (IList<object>) result;
            }
        }
    }
    public sealed class DownloadRequest {
        public readonly int From;
        public readonly int Count;
        public DownloadRequest(int f, int count) {
            From = f;
            Count = count;
        }

        public override string ToString() {
            return $"GET [{From}:{From + Count}]";
        }
    }
}