using Domain.Common;
using Domain.Enums;

namespace Domain.Events
{
    /// <summary>Raised by every <see cref="Models.Order"/> state transition (not by order creation).</summary>
    public sealed class OrderStateChangedEvent : IDomainEvent
    {
        public int OrderId { get; }
        public OrderState FromState { get; }
        public OrderState ToState { get; }
        public string? ModifiedBy { get; }
        public DateTime OccurredOn { get; }

        public OrderStateChangedEvent(int orderId, OrderState fromState, OrderState toState, string? modifiedBy = null)
        {
            OrderId = orderId;
            FromState = fromState;
            ToState = toState;
            ModifiedBy = modifiedBy;
            OccurredOn = DateTime.UtcNow;
        }
    }
}
