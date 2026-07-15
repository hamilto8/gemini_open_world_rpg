using Godot;
using Meridian.Audio;

namespace Meridian.Data;

[GlobalClass]
public partial class SoundCueResource : Resource, ISoundCueDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public AudioStream? Stream { get; set; }
    [Export] public string BusName { get; set; } = "SFX";
    [Export(PropertyHint.Range, "-80,12,0.1")] public float VolumeDb { get; set; }
    [Export(PropertyHint.Range, "0.1,4.0,0.01")] public float MinPitch { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.1,4.0,0.01")] public float MaxPitch { get; set; } = 1f;

    public string StreamPath => Stream?.ResourcePath ?? "";
}
