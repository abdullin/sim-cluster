namespace SimMach.Playground.Backend {
    public static class Events {
        
    }
    
    
    public sealed class ItemAdded {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public ItemAdded(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }


        public override string ToString() {
            return $"ItemAdded: {Amount} to {ItemID}";
        }
    }

    public sealed class ItemRemoved {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public ItemRemoved(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }
        public override string ToString() {
            return $"ItemRemoved: {Amount} from {ItemID}";
        }
    }
}