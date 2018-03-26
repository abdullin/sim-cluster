namespace SimMach.Playground.Backend {
    public static class Interchange {
        
    }
    
    public sealed class AddItemRequest {
        public readonly long ItemID;
        public readonly decimal Amount;

        public AddItemRequest(long itemId, decimal amount) {
            ItemID = itemId;
            Amount = amount;
        }

        public override string ToString() {
            return $"Add {Amount} to {ItemID}";
        }
    }
    
    
    public sealed class MoveItemRequest {
        public readonly long FromItemID;
        public readonly long ToItemID;
        public readonly decimal Amount;

        public MoveItemRequest(long fromItemId, long toItemId, decimal amount) {
            FromItemID = fromItemId;
            ToItemID = toItemId;
            Amount = amount;
        }

        public override string ToString() {
            return $"Move {Amount} from {FromItemID} to {ToItemID}";
        }
    }

    public sealed class MoveItemResponse {
     
        
    }

    


    public sealed class AddItemResponse {
        public readonly long ItemID;
        public readonly decimal Amount;
        public readonly decimal Total;

        public AddItemResponse(long itemId, decimal amount, decimal total) {
            ItemID = itemId;
            Amount = amount;
            Total = total;
        }

        public override string ToString() {
            return $"ItemAdded: {Amount} to {ItemID}";
        }
    }

   
    


    public sealed class CountRequest {
        public override string ToString() {
            return $"Count request";
        }
    }

    public sealed class CountResponse {
        public decimal Count;
        public CountResponse(decimal count) {
            Count = count;
        }

        public override string ToString() {
            return $"Total count is {Count}";
        }
    }
   
}