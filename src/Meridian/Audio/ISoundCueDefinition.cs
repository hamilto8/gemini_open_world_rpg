namespace Meridian.Audio;

public interface ISoundCueDefinition
{
    string Id { get; }
    string StreamPath { get; }
    string BusName { get; }
    float VolumeDb { get; }
    float MinPitch { get; }
    float MaxPitch { get; }
}
