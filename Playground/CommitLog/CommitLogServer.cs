using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Playground.CommitLog {
    public sealed class CommitLogServer : IEngine {

        readonly IEnv _env;
        readonly ushort _port;

        public CommitLogServer(IEnv env, ushort port) {
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

        public Task Dispose() {
            return Task.CompletedTask;
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
                        case CommitRequest cr:
                            await _env.SimulateWork(5.Ms());

                            foreach (var e in cr.Events) {
                                _buffer.Enqueue(e);    
                            }

                            
                            ScheduleStore();
                            await conn.Write("OK");
                            return;
                        default:
                            conn.Write($"Unknown request {req}");
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
}