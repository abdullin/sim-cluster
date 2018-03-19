using System;
using System.Collections.Generic;
using System.IO;

namespace SimMach.Sim {
    public sealed class Tracer {

        int _traceId;

        readonly Func<long> _clock;
        StreamWriter _writer;

        public Tracer(Func<long> clock) {
            _clock = clock;
            
        }
        
        
        IDictionary<string, int> _procs = new Dictionary<string, int>();


        int GetProcID(string name) {
            int value;
            if (_procs.TryGetValue(name, out value)) {
                return value;
            }

            value = _procs.Count;
            _procs.Add(name, value);
            return value;
        }

        public void Instant(int procId, string name, string category) {
            if (_writer == null) {
                return;
            }
            Comma();
            var ts = Ts();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"i\",\"tid\":{procId},\"pid\":{procId},\"ts\":{ts},\"s\":\"p\"}}");

        }

        long Ts() {
            return _clock() / TicksPerMicrosecond;
        }

        public void FlowStart(int procId, string name, string category, string flowId) {
            if (_writer == null) {
                return;
            }
            Comma();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"s\",\"pid\":{procId},\"ts\":{Ts()},\"id\":\"{flowId}\"}}");
        }
        
        public void FlowEnd(int procId, string name, string category, string flowId) {
            if (_writer == null) {
                return;
            }
            Comma();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\", \"ph\":\"f\",\"pid\":{procId},\"ts\":{Ts()},\"id\":\"{flowId}\"}}");
        }


        public TracePoint TracePacket(SimEndpoint from, SimEndpoint to, SimPacket pack) {
            var route = from.ToString() + ":" + to.ToString();
            var id = GetProcID(route) + 1000;
            return AsyncScope(id, pack.Payload.ToString(), "net");
        }

        public void ProcessName(int procID, string name) {
            if (_writer == null) {
                return;
            }
            Comma();
            _writer.WriteLine($"{{\"name\":\"process_name\",\"ph\":\"M\",\"pid\":{procID},\"args\":{{\"name\":\"{name}\"}} }}");
        }

        public TracePoint SyncScope(int procId, string name, string category) {
            if (_writer == null) {
                return default(TracePoint);
            }

            var ts = _clock() / TicksPerMicrosecond;
            var traceId = ++_traceId;
            
            Comma();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"B\",\"pid\":{procId},\"ts\":{ts},\"id\":{traceId}}}");

            
            return new TracePoint(_clock(), procId, traceId, name, this, category, false);
        }
        
        public TracePoint AsyncScope(int procId, string name, string category) {
            if (_writer == null) {
                return default(TracePoint);
            }

            var ts = _clock() / TicksPerMicrosecond;
            var traceId = ++_traceId;
            
            Comma();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"b\",\"pid\":{procId},\"ts\":{ts},\"id\":{traceId}}}");

            
            return new TracePoint(_clock(), procId, traceId, name, this, category, true);
        }

        void Comma() {
            if (_count++ > 0) {
                _writer.Write(",");
            }
        }

        const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        public void Complete(TracePoint p) {
            
            
            var ts = _clock() / TicksPerMicrosecond;
            

            if (_count++ > 0) {
                _writer.Write(",");
            }

            var e = p.Async ? 'e' : 'E';
            _writer.WriteLine(
                $"{{\"cat\":\"{p.Category}\", \"name\":\"{p.Name}\",\"ph\":\"{e}\",\"pid\":{p.ProcId},\"ts\":{ts},\"id\":{p.TraceId}}}");
        }

        int _count;

        

        public void Start(Stream stream) {
            if (stream == null) {
                _writer = null;
                return;
            }

            _count = 0;
            
            _writer = new StreamWriter(stream);
            _writer.WriteLine("[");
        }

        public void Finish() {
            if (_writer != null) {
                _writer.WriteLine("]");
                _writer.Flush();
                _writer.Dispose();
            }
        }

        
    }
    
    public enum TracerCat{}
    
    public struct TracePoint : IDisposable {

        public static readonly TracePoint None = default(TracePoint);
        public readonly long Start;
        public readonly int ProcId;
        public readonly int TraceId;
        public readonly string Name;
        readonly Tracer _parent;
        public readonly string Category;
        public readonly bool Async;

        public TracePoint(long start, int procId, int traceId, string name, Tracer parent, string category, bool @async) {
            Start = start;
            ProcId = procId;
            TraceId = traceId;
            Name = name;
            _parent = parent;
            Category = category;
            Async = async;
        }

        public void Dispose() {
            _parent?.Complete(this);
        }
    }
}