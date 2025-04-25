using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Vehicles
{
  public class ListerVehiclesRepairable : MapComponent
  {
    private readonly Dictionary<Faction, HashSet<VehiclePawn>> vehiclesToRepair = [];

    public ListerVehiclesRepairable(Map map) : base(map)
    {
    }

    private static HashSet<VehiclePawn> Empty { get; } = [];

    public HashSet<VehiclePawn> RepairsForFaction(Faction faction)
    {
      if (faction is null)
        return Empty;
      if (!vehiclesToRepair.TryGetValue(faction, out HashSet<VehiclePawn> vehicles))
      {
        vehiclesToRepair[faction] = vehicles = [];
      }
      return vehicles;
    }

    public void NotifyVehicleSpawned(VehiclePawn vehicle)
    {
      NotifyVehicleRepaired(vehicle);
      NotifyVehicleTookDamage(vehicle);
    }

    public void NotifyVehicleDespawned(VehiclePawn vehicle)
    {
      if (vehicle.Faction is null)
        return;
      if (vehiclesToRepair.TryGetValue(vehicle.Faction, out var vehicles))
      {
        vehicles.Remove(vehicle);
      }
    }

    public void NotifyVehicleTookDamage(VehiclePawn vehicle)
    {
      if (vehicle.Faction is null)
        return;

      if (vehicle.statHandler.NeedsRepairs &&
        !Mathf.Approximately(vehicle.GetStatValue(VehicleStatDefOf.BodyIntegrity), 0))
      {
        if (!vehiclesToRepair.TryGetValue(vehicle.Faction, out HashSet<VehiclePawn> vehicles))
        {
          vehiclesToRepair[vehicle.Faction] = vehicles = [];
        }
        if (vehicle.Spawned)
          vehicles.Add(vehicle);
        else
          vehicles.Remove(vehicle);
      }
    }

    public void NotifyVehicleRepaired(VehiclePawn vehicle)
    {
      if (vehicle.Faction == null)
        return;
      if (vehicle.statHandler.NeedsRepairs)
        return;

      if (vehiclesToRepair.TryGetValue(vehicle.Faction, out HashSet<VehiclePawn> vehicles))
      {
        vehicles.Remove(vehicle);
      }
    }
  }
}