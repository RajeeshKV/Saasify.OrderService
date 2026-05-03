namespace Domain
{
    public class OrderMessage
    {
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public required string Description { get; set; }
        public required string CustomerEmail { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string MessageType { get; set; } = "OrderCreated";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public required string CorrelationId { get; set; }
    }

    public class OrderResponse
    {
        public int OrderId { get; set; }
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ExternalOrderId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class OrderStatusMessage
    {
        public int OrderId { get; set; }
        public int TenantId { get; set; }
        public required string Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public required string CorrelationId { get; set; }
        public required OrderMessage OriginalMessage { get; set; }
        public string? ExternalOrderId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
