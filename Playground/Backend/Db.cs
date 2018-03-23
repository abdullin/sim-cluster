using System.Collections.Generic;
using System.Linq;

namespace SimMach.Playground.Backend {


    public sealed class Db {
        readonly Dictionary<long, decimal> _store = new Dictionary<long, decimal>();

        public void SetItemQuantity(long id, decimal amount) {
            _store[id] = amount;
        }

        public decimal GetItemQuantity(long id) {
            return _store.TryGetValue(id, out var quantity) ? quantity : 0;
        }


        public decimal Count() {
            return _store.Sum(p => p.Value);
        }
    }
}