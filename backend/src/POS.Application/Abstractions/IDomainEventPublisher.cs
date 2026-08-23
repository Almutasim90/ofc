using POS.Domain.Events;

namespace POS.Application.Abstractions;

public interface IDomainEventPublisher
{
    /// <summary>
    /// Invokes every registered IDomainEventHandler&lt;TEvent&gt; for this event type.
    /// With zero handlers registered (the common case until a Sprint 8-style extension is
    /// added), this is a safe no-op - the point is the extension point exists, not that
    /// anything currently listens.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
}
