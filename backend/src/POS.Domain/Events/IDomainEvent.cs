namespace POS.Domain.Events;

/// <summary>Marker for events raised after a domain operation completes, so cross-cutting
/// listeners (printing, KDS, etc.) can react without the operation calling them directly.</summary>
public interface IDomainEvent;
