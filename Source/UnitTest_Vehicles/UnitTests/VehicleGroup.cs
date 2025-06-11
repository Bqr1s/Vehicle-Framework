using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles.UnitTesting;

public class VehicleGroup
{
  public readonly VehiclePawn vehicle;
  public readonly List<Pawn> pawns = [];

  public VehicleGroup(VehiclePawn vehicle)
  {
    this.vehicle = vehicle;
  }

  public void Spawn()
  {
    TestUtils.ForceSpawn(vehicle);
    BoardAll();
  }

  public void DeSpawne()
  {
    vehicle.DeSpawn();
    Assert.IsFalse(vehicle.Spawned);

    foreach (Pawn pawn in pawns)
    {
      if (pawn.IsInVehicle())
        Assert.IsFalse(pawn.Spawned);
      else
        Assert.IsTrue(pawn.Spawned);
    }
  }

  public Pawn BoardOne()
  {
    Pawn pawn = pawns.First();
    Assert.IsTrue(vehicle.TryAddPawn(pawn));
    Assert.IsFalse(pawn.Spawned);
    return pawn;
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
      if (vehicle.Spawned)
        Assert.IsTrue(pawn.Spawned);
      else if (vehicle.InVehicleCaravan())
        Assert.IsTrue(pawn.InVehicleCaravan());
      else
        throw new NotImplementedException("Unhandled disembarking situation.");
      Assert.IsTrue(pawn.Spawned);
    }
  }
}