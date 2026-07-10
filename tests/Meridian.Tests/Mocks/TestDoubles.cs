using System.Collections.Generic;
using Meridian.Combat;
using Meridian.Core;
using Meridian.Data;
using Meridian.Quests;

// Test doubles for the domain interfaces. These live in the test project (not shipping assemblies)
// and are declared in their interface's namespace so tests reference them without extra usings (M14).

namespace Meridian.Combat
{
    /// <summary>Pure C# IWeaponDefinition used by weapon/combat tests.</summary>
    public class BasicWeaponDefinition : IWeaponDefinition
    {
        public string Id { get; set; } = "pistol";
        public string DisplayName { get; set; } = "Pistol";
        public float BaseDamage { get; set; } = 25.0f;
        public string DamageTypeId { get; set; } = "physical";
        public float FireRate { get; set; } = 5.0f;
        public float MaxRange { get; set; } = 50.0f;
        public string AmmoTypeId { get; set; } = "ammo_9mm";
        public int MagazineSize { get; set; } = 12;
        public float ReloadTime { get; set; } = 1.5f;
    }
}

namespace Meridian.Data
{
    /// <summary>Pure C# IVehicleHandlingProfile used by vehicle tests.</summary>
    public class BasicVehicleHandlingProfile : IVehicleHandlingProfile
    {
        public string Id { get; set; } = "default_car";
        public float MaxSpeed { get; set; } = 20.0f;
        public float Acceleration { get; set; } = 8.0f;
        public float SteeringLimit { get; set; } = 40.0f;
        public float BrakingStrength { get; set; } = 15.0f;
        public float FuelBurnRate { get; set; } = 2.0f;
        public float Wheelbase { get; set; } = 2.6f;
        public float MaxLateralAcceleration { get; set; } = 9.0f;
    }

    /// <summary>Pure C# ILootTableDefinition used by registry/validator tests.</summary>
    public class BasicLootTableDefinition : ILootTableDefinition
    {
        public string Id { get; set; } = "";
        public IReadOnlyList<string> ItemIds { get; set; } = new List<string>();

        public BasicLootTableDefinition(string id, params string[] itemIds)
        {
            Id = id;
            ItemIds = new List<string>(itemIds);
        }
    }

    /// <summary>Pure C# IRegionDefinition used by registry/validator tests.</summary>
    public class BasicRegionDefinition : IRegionDefinition
    {
        public string Id { get; set; } = "";
        public IReadOnlyList<string> CellScenePaths { get; set; } = new List<string>();

        public BasicRegionDefinition(string id, params string[] cellScenePaths)
        {
            Id = id;
            CellScenePaths = new List<string>(cellScenePaths);
        }
    }

    /// <summary>Pure C# IWeatherProfile used by registry/validator tests.</summary>
    public class BasicWeatherProfile : IWeatherProfile
    {
        public string Id { get; set; } = "clear";

        public BasicWeatherProfile(string id) => Id = id;
    }

    /// <summary>Pure C# IMovementProfile used by registry/validator tests.</summary>
    public class BasicMovementProfile : IMovementProfile
    {
        public string Id { get; set; } = "";

        public BasicMovementProfile(string id) => Id = id;
    }
}

namespace Meridian.Core
{
    /// <summary>Pure C# IProgressionProfile used by progression tests.</summary>
    public class BasicProgressionProfile : IProgressionProfile
    {
        public int BaseXpRequired { get; set; } = 100;
        public float XpExponent { get; set; } = 1.5f;
        public int MaxLevel { get; set; } = 50;
        public int SkillPointsPerLevel { get; set; } = 2;
    }
}

namespace Meridian.Quests
{
    /// <summary>Pure C# IQuestDefinition used by quest tests.</summary>
    public class BasicQuestDefinition : IQuestDefinition
    {
        public string QuestId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public IReadOnlyList<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        public IReadOnlyList<QuestReward> Rewards { get; set; } = new List<QuestReward>();
    }
}
