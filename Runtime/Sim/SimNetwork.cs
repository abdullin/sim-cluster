using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SimMach.Sim {

  
    

    [Flags]
    public enum SimFlag : byte {
        None = 0x00,
        Fin = 0x01,
        Reset = 0x04,
        Ack = 0x10,
        Syn = 0x02,
    }

    public class SimPacket {
        public readonly SimEndpoint Source;
        public readonly SimEndpoint Destination;
        
        public readonly object Payload;
        public readonly uint SeqNumber;
        public readonly uint AckNumber;
        public readonly SimFlag Flag;

        public SimPacket(SimEndpoint source, SimEndpoint destination, object payload, 
            uint seqNumber,
            uint ackNumber,
            SimFlag flag) {
            Source = source;
            Destination = destination;
            Payload = payload;
            SeqNumber = seqNumber;
            AckNumber = ackNumber;
            Flag = flag;
        }

        public uint NextSeqNumber => SeqNumber + 1;
        
        

        public override string ToString() {
            return $"{Source}->{Destination}: {BodyString()}";
        }

        public string BodyString() {
            var body = Payload == null ? "" : Payload.ToString();
            if (Flag != SimFlag.None) {
                body += $" {Flag.ToString().ToUpperInvariant()}";
            }

            return body.Trim();
        }
    }

    sealed class ClientConn : IConn {
        public ClientConn(SimConn conn) {
            _conn = conn;
        }

        readonly SimConn _conn;

        public SimEndpoint RemoteAddress => _conn.RemoteAddress;
        
        public void Dispose() {
            _conn.Dispose();
        }

        public async Task Write(object message) {
            await _conn.Write(message);
        }

        public async Task<object> Read(TimeSpan timeout) {
            try {
                var msg = await _conn.Read(timeout);
                return msg.Payload;
            } catch (TimeoutException ex) {
                throw new IOException("Read timeout", ex);
            }
        }
    }


    public interface ISocket : IDisposable {
        Task<IConn> Accept();
    }
}