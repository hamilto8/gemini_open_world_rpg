using System;
using Meridian.Environment;
using Meridian.Items;
using Meridian.Quests;
using Meridian.World;

namespace Meridian.Core.Logic;

/// <summary>
/// Production <see cref="IConditionContext"/> that answers each query by pulling the relevant service
/// from the <see cref="Services"/> locator <em>lazily, per call</em> (never caching across calls, so a
/// service registered or swapped after construction is picked up immediately). A missing service yields
/// the safe default — conditions read false / 0 / null rather than throwing (§3.5, §3.6).
/// </summary>
/// <remarks>
/// Two seams are genuinely scene/engine-bound and are injected so this type stays headless-constructible:
/// <list type="bullet">
///   <item><description><c>statProbe</c> reads the possessed avatar's <c>StatBlock</c> (a Node lookup);
///   the default reads 0.</description></item>
///   <item><description><c>isInVehiclePredicate</c> classifies the possessed entity without coupling
///   Core to <c>Meridian.Vehicles</c>; the integration pass supplies <c>e =&gt; e is VehicleAvatar</c>,
///   the default returns false.</description></item>
/// </list>
/// </remarks>
public sealed class ServicesConditionContext : IConditionContext
{
    private readonly Func<string, float> _statProbe;
    private readonly Func<IPossessable?, bool> _isInVehiclePredicate;

    /// <summary>Creates a services-backed condition context.</summary>
    /// <param name="statProbe">Reads a stat by id from the possessed avatar; defaults to 0.</param>
    /// <param name="isInVehiclePredicate">Classifies the possessed entity as a vehicle; defaults to false.</param>
    public ServicesConditionContext(
        Func<string, float>? statProbe = null,
        Func<IPossessable?, bool>? isInVehiclePredicate = null)
    {
        _statProbe = statProbe ?? (static _ => 0f);
        _isInVehiclePredicate = isInVehiclePredicate ?? (static _ => false);
    }

    /// <inheritdoc />
    public int Hour => Services.TryGet<IWorldClock>(out var clock) && clock is not null ? clock.CurrentHour : 0;

    /// <inheritdoc />
    public string CurrentPhase =>
        Services.TryGet<IWorldClock>(out var clock) && clock is not null ? clock.CurrentPhase.ToString() : string.Empty;

    /// <inheritdoc />
    public string? CurrentWeatherId =>
        Services.TryGet<IWeatherSystem>(out var weather) && weather is not null ? weather.CurrentWeatherId : null;

    /// <inheritdoc />
    public bool GetWorldFlag(string id) =>
        Services.TryGet<IWorldFlags>(out var flags) && flags is not null && flags.GetFlag(id);

    /// <inheritdoc />
    public float GetStat(string id) => string.IsNullOrEmpty(id) ? 0f : _statProbe(id);

    /// <inheritdoc />
    public int GetItemCount(string id)
    {
        if (string.IsNullOrEmpty(id) || !Services.TryGet<IInventoryProvider>(out var provider) || provider is null)
        {
            return 0;
        }

        return provider.Inventory.GetItemCount(id);
    }

    /// <inheritdoc />
    public bool IsInVehicle
    {
        get
        {
            IPossessable? possessed = Services.TryGet<IPlayerController>(out var controller) && controller is not null
                ? controller.PossessedEntity
                : null;
            return _isInVehiclePredicate(possessed);
        }
    }

    /// <inheritdoc />
    public string? CurrentRegionId =>
        Services.TryGet<IWorldStreamer>(out var streamer) && streamer is not null ? streamer.CurrentRegionId : null;

    /// <inheritdoc />
    public string? GetQuestState(string questId)
    {
        if (string.IsNullOrEmpty(questId) || !Services.TryGet<QuestManager>(out var quests) || quests is null)
        {
            return null;
        }

        return quests.GetQuestState(questId).ToString();
    }
}
