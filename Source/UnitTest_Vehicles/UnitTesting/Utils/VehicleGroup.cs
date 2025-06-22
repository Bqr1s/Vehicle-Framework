﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles.UnitTesting;

public class VehicleGroup : IDisposable
{
  public readonly VehiclePawn vehicle;
  public readonly List<Pawn> pawns = [];

  public VehicleGroup(VehiclePawn vehicle)
  {
    this.vehicle = vehicle;
  }

  public void Spawn()
  {
    DeSpawn();
    TestUtils.ForceSpawn(vehicle);
    BoardAll();
  }

  public void SpawnPawns()
  {
    Map map = Find.CurrentMap;
    foreach (Pawn pawn in pawns)
    {
      GenSpawn.Spawn(pawn, CellFinder.RandomSpawnCellForPawnNear(map.Center, map), map, Rot4.North);
      Assert.IsTrue(pawn.Spawned);
    }
  }

  public void DeSpawn()
  {
    if (vehicle.Spawned)
      vehicle.DeSpawn();
    Assert.IsFalse(vehicle.Spawned);

    foreach (Pawn pawn in pawns)
    {
      if (pawn.Spawned)
        pawn.DeSpawn();
      Assert.IsFalse(pawn.Spawned);
    }
  }

  public void DeSpawnPawns()
  {
    foreach (Pawn pawn in pawns)
    {
      if (pawn.Spawned)
        pawn.DeSpawn();
      Assert.IsFalse(pawn.Spawned);
    }
  }

  public void BoardOne()
  {
    Pawn pawn = pawns.First();
    Assert.IsTrue(vehicle.TryAddPawn(pawn));
    Assert.IsFalse(pawn.Spawned);
  }

  public void BoardAll()
  {
    foreach (Pawn pawn in pawns)
    {
      if (!pawn.IsInVehicle())
        Assert.IsTrue(vehicle.TryAddPawn(pawn));
    }
  }

  public Pawn DisembarkOne()
  {
    Pawn pawn = pawns.First();
    vehicle.DisembarkPawn(pawn);
    if (vehicle.Spawned)
      Assert.IsTrue(pawn.Spawned);
    else if (vehicle.InVehicleCaravan())
      Assert.IsTrue(pawn.InVehicleCaravan());
    else
      throw new NotImplementedException("Unhandled disembarking situation.");
    return pawn;
  }

  public void DisembarkAll()
  {
    vehicle.DisembarkAll();
    foreach (Pawn pawn in pawns)
    {
      if (vehicle.InVehicleCaravan())
        Assert.IsTrue(pawn.InVehicleCaravan());
      Assert.IsTrue(pawn.Spawned);
    }
  }

  public void Dispose()
  {
    foreach (Pawn pawn in pawns)
    {
      vehicle.RemovePawn(pawn);
      Assert.IsFalse(pawn.IsInVehicle());
      pawn.Destroy();
      Assert.IsTrue(pawn.Destroyed);
    }
    vehicle.Destroy();
    Assert.IsTrue(vehicle.Destroyed);
  }

  public static VehicleGroup CreateBasicVehicleGroup(MockSettings settings)
  {
    VehicleDef vehicleDef =
      TestDefGenerator.CreateTransientVehicleDef($"VehicleDef_MOCK_{Rand.Int}",
        settings.debugLabel);

    if (!settings.statModifiers.NullOrEmpty())
    {
      vehicleDef.vehicleStats = [.. settings.statModifiers];
    }
    else
    {
      // Default values to ensure vehicle is at least moveable if required
      vehicleDef.vehicleStats =
      [
        new VehicleStatModifier
        {
          statDef = VehicleStatDefOf.MoveSpeed,
          value = !settings.permissions.HasFlag(VehiclePermissions.Mobile) ? 0 : 10
        },
        new VehicleStatModifier
        {
          statDef = VehicleStatDefOf.CargoCapacity,
          value = 1,
        }
      ];
    }

    if (settings.passengers > 0)
    {
      vehicleDef.properties.roles =
      [
        new VehicleRole
        {
          key = "Passenger",
          slots = (int)(settings.passengers + settings.animals)
        }
      ];
    }

    Assert.IsTrue(settings.drivers > 0 ==
      !settings.permissions.HasFlag(VehiclePermissions.Autonomous));
    if (!settings.permissions.HasFlag(VehiclePermissions.Autonomous))
    {
      vehicleDef.properties.roles.Add(new VehicleRole
      {
        key = "Driver",
        slots = (int)settings.drivers,
        slotsToOperate = (int)settings.drivers,

        handlingTypes = HandlingType.Movement
      });
    }

    if (!settings.comps.NullOrEmpty())
    {
      foreach (CompProperties compProps in settings.comps)
      {
        compProps.ResolveReferences(vehicleDef);
        vehicleDef.comps.Add(compProps);
      }
    }

    // VehicleDef needs to be complete by this point for PostGeneration events
    VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, settings.faction);
    VehicleGroup group = new(vehicle);
    for (int i = 0; i < settings.drivers + settings.passengers; i++)
    {
      Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsNotNull(colonist);
      Assert.AreEqual(colonist.Faction, Faction.OfPlayer);
      group.pawns.Add(colonist);
    }
    for (int i = 0; i < settings.animals; i++)
    {
      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsNotNull(animal);
      Assert.AreEqual(animal.Faction, Faction.OfPlayer);
      group.pawns.Add(animal);
    }
    return group;
  }

  public class MockSettings
  {
    public string debugLabel;

    // Reverse mapping permissions to def restrictions for easy configuration
    public VehiclePermissions permissions;
    public uint drivers;
    public uint passengers;
    public uint animals;

    public Faction faction = Faction.OfPlayer;

    public List<VehicleStatModifier> statModifiers;
    public List<CompProperties> comps;
  }
}