using System;

namespace SimMach.Playground.Backend {
    public sealed class Projection {
        readonly Store _store;

        public Projection(Store store) {
            _store = store;
        }

        public void Dispatch(object e) {
            switch (e) {
                case ItemAdded x:
                    _store.SetItemQuantity(x.ItemID, x.Total);
                    return;
                case ItemRemoved x:
                    _store.SetItemQuantity(x.ItemID, x.Total);
                    return;
                default:
                    throw new InvalidOperationException($"Unknown event {e}");
                       
            }
        }
    }
}