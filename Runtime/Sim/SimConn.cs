using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SimMach.Sim {
    public class SimConn  {

        // maintain and send connection ID;
        readonly SimSocket _socket;
        public readonly SimEndpoint RemoteAddress;
        readonly SimProc _proc;
        uint _sequenceNumber;
        uint _ackNumber;
        SimFuture<SimPacket> _pendingRead;
        

        readonly Queue<SimPacket> _readBuffer = new Queue<SimPacket>();
        readonly SortedList<uint, SimPacket> _outOfOrder = new SortedList<uint, SimPacket>();
        

        public SimConn(SimSocket socket, SimEndpoint remoteAddress, SimProc proc, uint seq, uint ackNumber) {
            _socket = socket;
            RemoteAddress = remoteAddress;
            _proc = proc;

            _sequenceNumber = seq;
            _ackNumber = ackNumber;
        }

        public async Task Write(object message, SimFlag flag = SimFlag.None) {
            if (_closed) {
                throw new IOException("Socket closed");
            }
            var packet = new SimPacket(_socket.Endpoint, RemoteAddress,  message, _sequenceNumber, _ackNumber, flag);
            _socket.SendMessage(packet);
            _sequenceNumber++;
        }


        bool _closed;

        bool NextIsFin() {
            return _readBuffer.Count == 1 && _readBuffer.Peek().Flag == SimFlag.Fin;
        }


        void Close(string msg) {
            //_socket.Debug($"Close: {msg}");
            _closed = true;
        }


        public async Task<SimPacket> Read(TimeSpan timeout) {

            if (_closed) {
                throw new IOException("Socket closed");
            }

            if (_readBuffer.TryDequeue(out var tuple)) {

                if (tuple.Flag == SimFlag.Reset) {
                    Close("RESET");
                    throw new IOException("Connection reset");
                }

                if (NextIsFin()) {
                    Close("FIN");
                }

                return tuple;
            }

            _pendingRead = _proc.Promise<SimPacket>(timeout, _proc.Token);
            
            

            var msg = await _pendingRead.Task;

            if (msg.Flag == SimFlag.Reset) {
                Close("RESET");
                throw new IOException("Connection reset");
            }
            
            if (NextIsFin()) {
                Close("FIN");
            }
            
            return msg;

        }

        public void Dispose() {
            if (!_closed) {
                Write(null, SimFlag.Fin);
                Close("Dispose");
            }
            
            

            // drop socket on dispose
        }

        public void Deliver(SimPacket msg) {
            if (msg.SeqNumber != _ackNumber) {
                //_socket.Debug($"Out-of-order packet {msg.BodyString()} from {msg.Source} with Seq {msg.SeqNumber}. Ours {_ackNumber}");
                _outOfOrder.Add(msg.SeqNumber, msg);
                return;
            }

            HandOut(msg);

            // try to deliver out-of-order packets
            // maybe this packet unblocks them
            while (_outOfOrder.TryGetValue(_ackNumber, out var value)) {
                HandOut(value);
                _outOfOrder.Remove(_ackNumber);
            }
        }

        void HandOut(SimPacket msg) {
            _ackNumber = msg.NextSeqNumber;
            if (_pendingRead != null) {
                _pendingRead.SetResult(msg);
                _pendingRead = null;
            } else {
                _readBuffer.Enqueue(msg);
            }
        }
    }
}