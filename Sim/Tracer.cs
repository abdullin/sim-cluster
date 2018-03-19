using System;
using System.IO;

namespace SimMach.Sim {
    public sealed class Tracer {

        int _traceId;

        readonly Func<long> _clock;
        StreamWriter _writer;

        public Tracer(Func<long> clock) {
            _clock = clock;
            
        }

        public void Instant(int procId, string name, string category) {
            Comma();
            var ts = _clock() / TicksPerMicrosecond;
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"i\",\"pid\":{procId},\"ts\":{ts},\"s\":\"p\"}}");

        }

        public TracePoint Scope(int procId, string name, string category) {
            if (_writer == null) {
                return default(TracePoint);
            }

            var ts = _clock() / TicksPerMicrosecond;
            var traceId = ++_traceId;
            
            Comma();
            _writer.WriteLine(
                $"{{\"cat\":\"{category}\", \"name\":\"{name}\",\"ph\":\"B\",\"pid\":{procId},\"ts\":{ts},\"id\":{traceId}}}");

            
            return new TracePoint(_clock(), procId, traceId, name, this, category);
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
            _writer.WriteLine(
                $"{{\"cat\":\"{p.Category}\", \"name\":\"{p.Name}\",\"ph\":\"E\",\"pid\":{p.ProcId},\"ts\":{ts},\"id\":{p.TraceId}}}");
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
        public readonly long Start;
        public readonly int ProcId;
        public readonly int TraceId;
        public readonly string Name;
        readonly Tracer _parent;
        public readonly string Category;

        public TracePoint(long start, int procId, int traceId, string name, Tracer parent, string category) {
            Start = start;
            ProcId = procId;
            TraceId = traceId;
            Name = name;
            _parent = parent;
            Category = category;
        }

        public void Dispose() {
            _parent?.Complete(this);
        }
    }
}