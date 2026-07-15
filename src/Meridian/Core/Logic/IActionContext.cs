namespace Meridian.Core.Logic;

/// <summary>
/// Mutation surface that <see cref="IGameAction"/> implementations act through (§3.6 item 2).
/// Kept engine-free — positions are passed as primitive floats rather than <c>Vector3</c> — so actions
/// and their fakes are headlessly testable. Production wiring is supplied by
/// <see cref="ServicesActionContext"/>; engine/scene-bound operations there route through injectable
/// delegates with safe no-op defaults.
/// </summary>
public interface IActionContext
{
    /// <summary>Gives the player <c>count</c> of an item. Returns false if the grant was refused.</summary>
    bool GiveItem(string id, int count);

    /// <summary>Removes <c>count</c> of an item from the player. Returns false if not enough were held.</summary>
    bool RemoveItem(string id, int count);

    /// <summary>Grants experience points to the player.</summary>
    void GrantXp(int amount);

    /// <summary>Sets a world flag (the consequence-memory store) to the given value.</summary>
    void SetWorldFlag(string id, bool value);

    /// <summary>Starts (accepts) a quest. Returns false if it could not be started.</summary>
    bool StartQuest(string questId);

    /// <summary>Plays a sound cue by id.</summary>
    void PlaySoundCue(string cueId);

    /// <summary>Shows a transient HUD notification to the player.</summary>
    void ShowNotification(string message);

    /// <summary>Teleports the player to the given world position.</summary>
    void TeleportPlayer(float x, float y, float z);

    /// <summary>Spawns a scene at the given world position. Returns false if it could not be spawned.</summary>
    bool SpawnScene(string scenePath, float x, float y, float z);

    /// <summary>Adjusts faction reputation. Returns false when the faction is unknown.</summary>
    bool ModifyFactionReputation(string factionId, int amount) => false;
}
