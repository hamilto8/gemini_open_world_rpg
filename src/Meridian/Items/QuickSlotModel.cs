using System;
using System.Collections.Generic;
using Meridian.Core.Save;

namespace Meridian.Items;

/// <summary>
/// Save-ready quick-access bar. Bindings are stable content ids rather than references to scene nodes
/// or inventory list indices, so inventory sorting and UI replacement do not invalidate them.
/// </summary>
public sealed class QuickSlotModel : ISaveParticipant
{
    private readonly Dictionary<int, string> _bindings = new();
    private readonly Func<string, bool>? _contentExists;
    private readonly Action<string>? _logger;

    public QuickSlotModel(Func<string, bool>? contentExists = null, Action<string>? logger = null)
    {
        _contentExists = contentExists;
        _logger = logger;
    }

    public IReadOnlyDictionary<int, string> Bindings => _bindings;
    public string ParticipantId => "PlayerQuickSlots";
    public int RestoreOrder => SaveRestoreOrder.Equipment + 1;
    public Type StateType => typeof(QuickSlotsStateDto);

    public void Bind(int slotIndex, string contentId)
    {
        if (slotIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        _bindings[slotIndex] = contentId;
    }

    public bool Clear(int slotIndex) => _bindings.Remove(slotIndex);

    public object CaptureState() => new QuickSlotsStateDto(new Dictionary<int, string>(_bindings));

    public void RestoreState(object stateDto)
    {
        if (stateDto is not QuickSlotsStateDto dto)
        {
            throw new ArgumentException("Expected quick-slot state.", nameof(stateDto));
        }

        _bindings.Clear();
        foreach (var (slot, contentId) in dto.ContentIds ?? new Dictionary<int, string>())
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(contentId))
            {
                continue;
            }
            _bindings[slot] = contentId;
            if (_contentExists != null && !_contentExists(contentId))
            {
                _logger?.Invoke($"[QuickSlotSave] Unknown content '{contentId}' preserved in slot {slot}.");
            }
        }
    }
}
