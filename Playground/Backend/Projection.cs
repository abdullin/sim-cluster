using System;

namespace SimMach.Playground.Backend {
    public sealed class Projection {
        readonly Db _db;

        public Projection(Db db) {
            _db = db;
        }

        public void Dispatch(object e) {
            switch (e) {
                case ItemAdded x:
                    _db.SetItemQuantity(x.ItemID, x.Total);
                    return;
                case ItemRemoved x:
                    _db.SetItemQuantity(x.ItemID, x.Total);
                    return;
                default:
                    throw new InvalidOperationException($"Unknown event {e}");
                       
            }
        }
    }
}