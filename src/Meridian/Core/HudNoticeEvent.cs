namespace Meridian.Core;

/// <summary>
/// Transient player-facing notice ("Pack is full", "Health already full") published on the EventBus
/// and rendered by the HUD as a short-lived toast (Section 14.3/14.4). Gameplay code publishes this
/// instead of reaching into UI nodes, keeping the "event across" rule intact.
/// </summary>
public readonly record struct HudNoticeEvent(string Message);
