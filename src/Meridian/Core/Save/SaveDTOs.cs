using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Meridian.Core.Save;

/// <summary>
/// Header information attached to every save file.
/// </summary>
public record SaveHeaderDto(
    int SaveVersion,
    string GameVersion,
    DateTime Timestamp,
    double PlaytimeSeconds,
    string LocationName,
    string ThumbnailPath
);

/// <summary>
/// Root container serialized to disk for a save game.
/// Holds modular JSON strings for each registered ISaveParticipant, ensuring forward compatibility.
/// </summary>
public record GameSaveData(
    SaveHeaderDto Header,
    Dictionary<string, string> ParticipantStatesJson
);

/// <summary>
/// Standard player state DTO.
/// </summary>
public record PlayerStateDto(
    string CurrentRegionId,
    float PositionX, float PositionY, float PositionZ,
    float RotationY,
    float Health,
    float Stamina,
    string PossessedGuid
);

/// <summary>
/// Standard world flags DTO for consequence memory.
/// </summary>
public record WorldFlagsDto(Dictionary<string, string> Flags);

/// <summary>
/// Standard time and weather state DTO.
/// </summary>
public record TimeWeatherDto(
    double TotalGameMinutes,
    int DayCounter,
    string CurrentWeatherId,
    float WeatherIntensity,
    float WeatherElapsed,
    int ForecastSeed
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GameSaveData))]
[JsonSerializable(typeof(SaveHeaderDto))]
[JsonSerializable(typeof(PlayerStateDto))]
[JsonSerializable(typeof(WorldFlagsDto))]
[JsonSerializable(typeof(TimeWeatherDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class SaveJsonContext : JsonSerializerContext
{
}
