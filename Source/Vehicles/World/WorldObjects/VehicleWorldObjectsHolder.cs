﻿using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace Vehicles;

public class VehicleWorldObjectsHolder : WorldComponent
{
  private static List<AerialVehicleInFlight> aerialVehicles = new List<AerialVehicleInFlight>();

  private static List<VehicleCaravan> vehicleCaravans = new List<VehicleCaravan>();

  private static List<StashedVehicle> stashedVehicles = new List<StashedVehicle>();

  public VehicleWorldObjectsHolder(World world) : base(world)
  {
    aerialVehicles ??= new List<AerialVehicleInFlight>();
    vehicleCaravans ??= new List<VehicleCaravan>();
    stashedVehicles ??= new List<StashedVehicle>();
    aerialVehicles.RemoveAll(a => a is null);
    vehicleCaravans.RemoveAll(c => c is null);
    stashedVehicles.RemoveAll(b => b is null);
    Instance = this;
  }

  public static VehicleWorldObjectsHolder Instance { get; private set; }

  public List<AerialVehicleInFlight> AerialVehicles => aerialVehicles;

  public List<VehicleCaravan> VehicleCaravans => vehicleCaravans;

  public List<StashedVehicle> StashedVehicles => stashedVehicles;

  public AerialVehicleInFlight AerialVehicleObject(VehiclePawn vehicle)
  {
    return AerialVehicles.FirstOrDefault(a => a.vehicle == vehicle);
  }

  public VehicleCaravan VehicleCaravanObject(VehiclePawn vehicle)
  {
    return VehicleCaravans.FirstOrDefault(c => c.PawnsListForReading.Contains(vehicle));
  }

  public StashedVehicle StashedVehicleObject(VehiclePawn vehicle)
  {
    return StashedVehicles.FirstOrDefault(stash => stash.Vehicles.Contains(vehicle));
  }

  public void Recache()
  {
    aerialVehicles.Clear();
    vehicleCaravans.Clear();
    stashedVehicles.Clear();
  }

  public void AddToCache(WorldObject obj)
  {
    if (obj is AerialVehicleInFlight aerial)
    {
      aerialVehicles.Add(aerial);
    }
    else if (obj is VehicleCaravan caravan)
    {
      vehicleCaravans.Add(caravan);
    }
    else if (obj is StashedVehicle dockedBoat)
    {
      stashedVehicles.Add(dockedBoat);
    }
    return; //air defenses disabled for now
    //if (obj is Settlement) //TODO - Add check for what settlements can implement air defenses
    //{
    //	foreach (AntiAircraftDef antiAircraft in DefDatabase<AntiAircraftDef>.AllDefsListForReading)
    //	{
    //		if (!AirDefensePositionTracker.airDefenseCache.ContainsKey(obj))
    //		{
    //			AirDefense airDefense = new AirDefense(obj)
    //			{
    //				defenseBuildings = antiAircraft.properties.buildings.RandomInRange
    //			};
    //			AirDefensePositionTracker.airDefenseCache.Add(obj, airDefense);
    //		}
    //	}
    //}
  }

  public void RemoveFromCache(WorldObject obj)
  {
    if (obj is AerialVehicleInFlight aerial)
    {
      aerialVehicles.Remove(aerial);
    }
    else if (obj is VehicleCaravan caravan)
    {
      vehicleCaravans.Remove(caravan);
    }
    else if (obj is StashedVehicle dockedBoat)
    {
      stashedVehicles.Remove(dockedBoat);
    }

    AirDefensePositionTracker.airDefenseCache.Remove(obj);
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Collections.Look(ref aerialVehicles, nameof(aerialVehicles), LookMode.Reference);
    Scribe_Collections.Look(ref vehicleCaravans, nameof(vehicleCaravans), LookMode.Reference);
    Scribe_Collections.Look(ref stashedVehicles, nameof(stashedVehicles), LookMode.Reference);

    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
      aerialVehicles.RemoveAll(a => a is null);
      vehicleCaravans.RemoveAll(c => c is null);
      stashedVehicles.RemoveAll(b => b is null);
    }
  }
}