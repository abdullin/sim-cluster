using System;
using System.Collections.Generic;
using System.Linq;
using LightningDB;

namespace SimMach.Playground.Backend {


    public sealed class Db {
        readonly LightningEnvironment _le;
        readonly LightningDatabase _ld;

        public Db(LightningEnvironment le, LightningDatabase ld) {
            _le = le;
            _ld = ld;
        }

        public void SetItemQuantity(long id, decimal amount) {

            using (var tx = _le.BeginTransaction()) {
                tx.Put(_ld, BitConverter.GetBytes(id), GetBytes(amount));
                tx.Commit();
            }
            
            
        }

        public decimal GetItemQuantity(long id) {
            using (var tx = _le.BeginTransaction(TransactionBeginFlags.ReadOnly)) {
                var val = tx.Get(_ld, BitConverter.GetBytes(id));
                if (val == null) {
                    return 0;
                }

                return ToDecimal(val);
            }
            
        }


        public decimal Count() {
            decimal total = 0M;
            using (var tx = _le.BeginTransaction(TransactionBeginFlags.ReadOnly)) {
                using (var ctx = tx.CreateCursor(_ld)) {
                    foreach (var v in ctx) {
                        total += ToDecimal(v.Value);
                    }
                }
            }
            
            return total;
        }
        
        
        public static decimal ToDecimal(byte[] bytes)
        {
            int[] bits = new int[4];
            bits[0] = ((bytes[0] | (bytes[1] << 8)) | (bytes[2] << 0x10)) | (bytes[3] << 0x18); //lo
            bits[1] = ((bytes[4] | (bytes[5] << 8)) | (bytes[6] << 0x10)) | (bytes[7] << 0x18); //mid
            bits[2] = ((bytes[8] | (bytes[9] << 8)) | (bytes[10] << 0x10)) | (bytes[11] << 0x18); //hi
            bits[3] = ((bytes[12] | (bytes[13] << 8)) | (bytes[14] << 0x10)) | (bytes[15] << 0x18); //flags

            return new decimal(bits);
        }

        public static byte[] GetBytes(decimal d)
        {
            byte[] bytes = new byte[16];

            int[] bits = decimal.GetBits(d);
            int lo = bits[0];
            int mid = bits[1];
            int hi = bits[2];
            int flags = bits[3];

            bytes[0] = (byte)lo;
            bytes[1] = (byte)(lo >> 8);
            bytes[2] = (byte)(lo >> 0x10);
            bytes[3] = (byte)(lo >> 0x18);
            bytes[4] = (byte)mid;
            bytes[5] = (byte)(mid >> 8);
            bytes[6] = (byte)(mid >> 0x10);
            bytes[7] = (byte)(mid >> 0x18);
            bytes[8] = (byte)hi;
            bytes[9] = (byte)(hi >> 8);
            bytes[10] = (byte)(hi >> 0x10);
            bytes[11] = (byte)(hi >> 0x18);
            bytes[12] = (byte)flags;
            bytes[13] = (byte)(flags >> 8);
            bytes[14] = (byte)(flags >> 0x10);
            bytes[15] = (byte)(flags >> 0x18);

            return bytes;
        }
    }
}