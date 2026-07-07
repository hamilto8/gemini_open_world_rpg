using System;
using System.Collections.Generic;

namespace Meridian.Core;

/// <summary>
/// Domain manager tracking character XP, levels, and perk stat modifiers.
/// Decoupled from Godot for unit testing.
/// Enforces Section 17.1 and 17.3 requirements.
/// </summary>
public class ProgressionManager
{
    private readonly IProgressionProfile _profile;
    private readonly HashSet<string> _unlockedPerks = new(StringComparer.OrdinalIgnoreCase);

    private int _level = 1;
    private int _currentXp = 0;
    private int _skillPoints = 0;

    public int Level => _level;
    public int CurrentXp => _currentXp;
    public int SkillPoints => _skillPoints;
    public IReadOnlySet<string> UnlockedPerks => _unlockedPerks;

    public event Action<int>? LevelChanged;
    public event Action<int>? XpChanged;

    public ProgressionManager(IProgressionProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public int GetXpForNextLevel()
    {
        return (int)(_profile.BaseXpRequired * Math.Pow(_level, _profile.XpExponent));
    }

    public void AddXp(int amount)
    {
        if (amount <= 0 || _level >= _profile.MaxLevel) return;

        _currentXp += amount;
        XpChanged?.Invoke(_currentXp);

        // Check level up
        while (_currentXp >= GetXpForNextLevel() && _level < _profile.MaxLevel)
        {
            _currentXp -= GetXpForNextLevel();
            _level++;
            _skillPoints += _profile.SkillPointsPerLevel;

            LevelChanged?.Invoke(_level);

            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new LevelUpEvent(_level));
            }
        }
    }

    public bool UnlockPerk(string perkId, StatBlock stats)
    {
        ArgumentException.ThrowIfNullOrEmpty(perkId);
        ArgumentNullException.ThrowIfNull(stats);

        if (_skillPoints <= 0 || _unlockedPerks.Contains(perkId))
        {
            return false;
        }

        _skillPoints--;
        _unlockedPerks.Add(perkId);

        // Apply perk stat modifier (Section 17.3 progression perks)
        ApplyPerkModifier(perkId, stats);

        return true;
    }

    private void ApplyPerkModifier(string perkId, StatBlock stats)
    {
        if (perkId.Equals("fast_reload", StringComparison.OrdinalIgnoreCase))
        {
            // +15% reload speed. reload_speed has base 1.0, so PercentAdd 0.15 => 1.15 (H8 fraction convention).
            stats.AddModifier(new Modifier(
                targetStatId: "reload_speed",
                operation: ModifierOp.PercentAdd,
                value: 0.15f,
                sourceTag: $"perk_{perkId}"
            ));
        }
        else if (perkId.Equals("thick_skin", StringComparison.OrdinalIgnoreCase))
        {
            // +10 flat armor resistance
            stats.AddModifier(new Modifier(
                targetStatId: "armor",
                operation: ModifierOp.Add,
                value: 10.0f,
                sourceTag: $"perk_{perkId}"
            ));
        }
    }
}

public record struct LevelUpEvent(int NewLevel);
