using System;
using System.Collections.Generic;
using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using LightningDB;
using LightningDB.Native;

namespace SimMach.Playground.Backend {


    public sealed class Store {
        
        public enum Tables : byte { SysCounter, Quantity }
        
        readonly LightningEnvironment _le;
        readonly LightningDatabase _ld;

        public Store(LightningEnvironment le, LightningDatabase ld) {
            _le = le;
            _ld = ld;
        }


        public long GetCounter() {
            using (var tx = _le.BeginTransaction(TransactionBeginFlags.ReadOnly)) {
                var key = FdbTuple.Create((byte) Tables.SysCounter);
                var val = tx.Get(_ld, key.GetBytes());
                if (val == null) {
                    return 0;
                }

                return BitConverter.ToInt64(val, 0);
            }
        }
        
        
        public void SetCounter(long id) {
            using (var tx = _le.BeginTransaction()) {
                var key = FdbTuple.Create((byte) Tables.SysCounter);
                if (id == 0) {
                    tx.Delete(_ld, key.GetBytes());
                } else {
                    tx.Put(_ld, key.GetBytes(), BitConverter.GetBytes(id));
                }
                tx.Commit();
            }
        }

        
        public void SetItemQuantity(long id, decimal amount) {
            var key = FdbTuple.Create((byte) Tables.Quantity, id);
            using (var tx = _le.BeginTransaction()) {
                if (amount == 0) {
                    tx.TryDelete(_ld, key.GetBytes());
                } else {
                    tx.Put(_ld, key.GetBytes(), GetBytes(amount));
                }

                tx.Commit();
            }
        }

        public decimal GetItemQuantity(long id) {
            using (var tx = _le.BeginTransaction(TransactionBeginFlags.ReadOnly)) {
                var key = FdbTuple.Create((byte) Tables.Quantity, id);
                var val = tx.Get(_ld, key.GetBytes());
                if (val == null) {
                    return 0;
                }

                return ToDecimal(val);
            }
        }
        
        
         IEnumerable<TOut> InternalScan<TOut>(LightningTransaction lt, FdbKeyRange range,
            Func<Slice, byte[], TOut> convert) {

            ushort x = 0;
			
            using (var c = lt.CreateCursor(_ld)) {
                if (!c.MoveToFirstAfter(range.Begin.GetBytes())) {
                    
                    yield break;
                }

                var pair = c.Current;

                for (var i = 0; i < int.MaxValue; i++) {
                    var current = Slice.Create(pair.Key);
                    if (!range.Contains(current)) {
                        
                        break;
                    }
                    x += 1;
                    yield return convert(current, pair.Value);

                    if (!c.MoveNext()) {
                        break;
                    }
                    pair = c.Current;
                }
            }
			
        }


        public decimal Count() {
            decimal total = 0M;
            using (var tx = _le.BeginTransaction(TransactionBeginFlags.ReadOnly)) {
                var prefix = FdbTuple.Create((byte) Tables.Quantity).ToSlice();
                var range = FdbKeyRange.StartsWith(prefix);
                var nums = InternalScan(tx, range, (slice, bytes) => ToDecimal(bytes));

                foreach (var num in nums) {
                    total += num;
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

    public sealed class Db {
        LightningEnvironment _le;
        LightningDatabase _ld;
    }

    public static class ExtendLightningTransaction {
        public static void TryDelete(this LightningTransaction tx, LightningDatabase db, byte[] key) {
            try {
                tx.Delete(db,key);
                
            } catch (LightningException ex) {
                if (ex.StatusCode != Lmdb.MDB_NOTFOUND) {
                    throw;
                }
            }
        }
    }
}