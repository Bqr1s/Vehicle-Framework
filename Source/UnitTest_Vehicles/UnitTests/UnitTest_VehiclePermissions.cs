using System.Collections.Generic;
using System.Linq;
using DevTools.UnitTesting;
using RimWorld;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
[TestDescription(
  "Vehicle is valid \"ThingHolder\" target and will prevent map unloading, game ending conditions, etc.")]
internal sealed class UnitTest_VehiclePermissions : UnitTest_VehicleTest
{
  private VehicleGroup manualVehicle;
  private VehicleGroup autonomousVehicle;
  private VehicleGroup immobileVehicle;

  [SetUp]
  private void GenerateVehicle()
  {
    manualVehicle = GenerateVehicleFor(VehiclePermissions.DriverNeeded);
    autonomousVehicle = GenerateVehicleFor(VehiclePermissions.NoDriverNeeded);
    immobileVehicle = GenerateVehicleFor(VehiclePermissions.NotAllowed);
    return;

    static VehicleGroup GenerateVehicleFor(VehiclePermissions permissions)
    {
      VehicleDef vehicleDef =
        TestDefGenerator.CreateTransientVehicleDef($"VehicleDef_{permissions}");

      vehicleDef.vehicleStats =
      [
        new VehicleStatModifier
        {
          statDef = VehicleStatDefOf.MoveSpeed,
          value = permissions == VehiclePermissions.NotAllowed ? 0 : 10
        }
      ];

      vehicleDef.properties.roles =
      [
        new VehicleRole
        {
          key = "Passenger",
          slots = 2
        }
      ];
      if (permissions != VehiclePermissions.NoDriverNeeded)
      {
        vehicleDef.properties.roles.Add(new VehicleRole
        {
          key = "Driver",
          slots = 2,
          slotsToOperate = 2,

          handlingTypes = HandlingType.Movement
        });
      }
      // VehicleDef needs to be complete by this point for PostGeneration events
      VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
      VehicleGroup group = new(vehicle);
      for (int i = 0; i < vehicle.handlers.Sum(handler => handler.role.Slots); i++)
      {
        Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Assert.IsNotNull(colonist);
        group.pawns.Add(colonist);
      }
      return group;
    }
  }

  [Test] // Driver Required
  private void DriverPermissions_Manual()
  {
    Assert.IsNotNull(manualVehicle);

    manualVehicle.Spawn();
    Assert.IsTrue(manualVehicle.vehicle.Spawned);

    // Can move when role requirements satisifed
    Expect.IsTrue(manualVehicle.vehicle.CanMove);
    Expect.IsTrue(manualVehicle.vehicle.CanMoveWithOperators);

    // Cannot move when role requirements not satisfied
    manualVehicle.DisembarkAll();
    Expect.IsTrue(manualVehicle.vehicle.CanMove);
    Expect.IsFalse(manualVehicle.vehicle.CanMoveWithOperators);

    // Cannot move unless operator count is satisfied
    manualVehicle.BoardOne();

    Expect.IsTrue(manualVehicle.vehicle.CanMove);
    Expect.IsFalse(manualVehicle.vehicle.CanMoveWithOperators);

    manualVehicle.BoardAll();

    manualVehicle.vehicle.DeSpawn();
  }

  [Test] // Autonomous
  private void DriverPermissions_Autonomous()
  {
    Assert.IsNotNull(autonomousVehicle);

    autonomousVehicle.Spawn();
    Assert.IsTrue(autonomousVehicle.vehicle.Spawned);

    // Can move by default
    Expect.IsTrue(autonomousVehicle.vehicle.CanMove);
    Expect.IsTrue(autonomousVehicle.vehicle.CanMoveWithOperators);

    // Can move even without any passengers
    autonomousVehicle.DisembarkAll();
    Expect.IsTrue(autonomousVehicle.vehicle.CanMove);
    Expect.IsTrue(autonomousVehicle.vehicle.CanMoveWithOperators);

    // Boarding does not invalidate any movement permissions
    autonomousVehicle.BoardOne();
    Expect.IsTrue(autonomousVehicle.vehicle.CanMove);
    Expect.IsTrue(autonomousVehicle.vehicle.CanMoveWithOperators);

    autonomousVehicle.BoardAll();

    autonomousVehicle.vehicle.DeSpawn();
  }

  [Test] // Immobile
  private void DriverPermissions_Immobile()
  {
    Assert.IsNotNull(immobileVehicle);

    immobileVehicle.Spawn();
    Assert.IsTrue(immobileVehicle.vehicle.Spawned);

    // Cannot move by default
    Expect.IsFalse(immobileVehicle.vehicle.CanMove);
    Expect.IsFalse(immobileVehicle.vehicle.CanMoveWithOperators);

    // Disembarking does not enable movement permissions
    immobileVehicle.DisembarkAll();
    Expect.IsFalse(immobileVehicle.vehicle.CanMove);
    Expect.IsFalse(immobileVehicle.vehicle.CanMoveWithOperators);

    // Sanity check for single boarding event, should be the same as before
    immobileVehicle.BoardOne();
    Expect.IsFalse(immobileVehicle.vehicle.CanMove);
    Expect.IsFalse(immobileVehicle.vehicle.CanMoveWithOperators);

    immobileVehicle.BoardAll();

    immobileVehicle.vehicle.DeSpawn();
  }

  [Test]
  private void GameOverCondition()
  {
  }

  [Test]
  private void EventMapHolding()
  {
  }

  [TearDown]
  private void DestroyAll()
  {
    manualVehicle.vehicle.DestroyVehicleAndPawns();
    autonomousVehicle.vehicle.DestroyVehicleAndPawns();
    immobileVehicle.vehicle.DestroyVehicleAndPawns();

    manualVehicle = null;
    autonomousVehicle = null;
    immobileVehicle = null;
  }

  private class VehicleGroup
  {
    public readonly VehiclePawn vehicle;
    public readonly List<Pawn> pawns = [];

    public VehicleGroup(VehiclePawn vehicle)
    {
      this.vehicle = vehicle;
    }

    public void Spawn()
    {
      VehicleDef vehicleDef = vehicle.VehicleDef;
      Map map = Find.CurrentMap;
      IntVec3 spawnCell = map.Center;
      int maxSize = Mathf.Max(vehicleDef.Size.x, vehicleDef.Size.z);
      CellRect testArea = CellRect.CenteredOn(spawnCell, maxSize).ExpandedBy(5);

      TerrainDef terrainDef = DefDatabase<TerrainDef>.AllDefsListForReading
       .FirstOrDefault(def => VehiclePathGrid.PassableTerrainCost(vehicleDef, def, out _) &&
          def.affordances.Contains(vehicleDef.buildDef.terrainAffordanceNeeded));
      DebugHelper.DestroyArea(testArea, map, terrainDef);

      GenSpawn.Spawn(vehicle, spawnCell, map, vehicleDef.defaultPlacingRot);
      BoardAll();
    }

    public void BoardOne()
    {
      Assert.IsTrue(vehicle.TryAddPawn(pawns.First()));
    }

    public void BoardAll()
    {
      foreach (Pawn pawn in pawns)
      {
        if (!pawn.IsInVehicle())
          Assert.IsTrue(vehicle.TryAddPawn(pawn));
      }
    }

    public void DisembarkAll()
    {
      Assert.IsTrue(vehicle.Spawned);
      vehicle.DisembarkAll();
      foreach (Pawn pawn in pawns)
        Assert.IsTrue(pawn.Spawned);
    }
  }
}