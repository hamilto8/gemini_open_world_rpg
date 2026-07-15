using System;
using System.Collections.Generic;

namespace Meridian.Core.Save;

/// <summary>
/// Persistent vehicle registry. Streamed vehicles may register after load; their pending saved state is
/// applied at registration. Missing definitions remain quarantined in subsequent saves.
/// </summary>
public sealed class VehiclePersistenceService : ISaveParticipant
{
    private readonly Dictionary<string, IPersistentVehicle> _vehicles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VehicleStateDto> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string> _currentRegionId;
    private readonly Func<string?> _possessedVehicleId;
    private readonly Action<string>? _logger;

    public VehiclePersistenceService(
        Func<string> currentRegionId,
        Func<string?> possessedVehicleId,
        Action<string>? logger = null)
    {
        _currentRegionId = currentRegionId ?? throw new ArgumentNullException(nameof(currentRegionId));
        _possessedVehicleId = possessedVehicleId ?? throw new ArgumentNullException(nameof(possessedVehicleId));
        _logger = logger;
    }

    public string ParticipantId => "PersistentVehicles";
    public int RestoreOrder => SaveRestoreOrder.WorldObjects + 1;
    public Type StateType => typeof(VehicleFleetStateDto);

    public void Register(IPersistentVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicle.PersistentVehicleId);
        _vehicles[vehicle.PersistentVehicleId] = vehicle;
        if (_pending.Remove(vehicle.PersistentVehicleId, out VehicleStateDto? state))
        {
            vehicle.RestoreVehicleState(state);
        }
    }

    public void Unregister(IPersistentVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        if (_vehicles.TryGetValue(vehicle.PersistentVehicleId, out var registered)
            && ReferenceEquals(registered, vehicle))
        {
            _vehicles.Remove(vehicle.PersistentVehicleId);
        }
    }

    public object CaptureState()
    {
        var states = new Dictionary<string, VehicleStateDto>(_pending, StringComparer.OrdinalIgnoreCase);
        string regionId = _currentRegionId();
        string? possessedId = _possessedVehicleId();
        foreach (var (id, vehicle) in _vehicles)
        {
            states[id] = vehicle.CaptureVehicleState(
                regionId,
                id.Equals(possessedId, StringComparison.OrdinalIgnoreCase));
        }
        return new VehicleFleetStateDto(new List<VehicleStateDto>(states.Values));
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not VehicleFleetStateDto dto)
        {
            throw new ArgumentException("Expected vehicle fleet state.", nameof(stateDto));
        }

        _pending.Clear();
        foreach (VehicleStateDto state in dto.Vehicles ?? new List<VehicleStateDto>())
        {
            if (string.IsNullOrWhiteSpace(state.PersistentId))
            {
                continue;
            }
            if (_vehicles.TryGetValue(state.PersistentId, out var vehicle))
            {
                vehicle.RestoreVehicleState(state);
            }
            else
            {
                _pending[state.PersistentId] = state;
                _logger?.Invoke(
                    $"[VehicleSave] Vehicle '{state.PersistentId}' ({state.DefinitionId}) is not loaded; state retained pending registration.");
            }
        }
    }
}
