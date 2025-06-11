using System;
using System.Linq;
using System.Reflection;
using DevTools.UnitTesting;
using HarmonyLib;
using RimWorld;
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
    // GameEnder only applies after first 300 ticks to allow starter pods to land
    using MockGameTicks gameTicks = new(500);
    Game game = Current.Game;
    Assert.IsNotNull(game);
    GameEnder gameEnder = game.gameEnder;
    Assert.IsNotNull(gameEnder);
    Assert.IsFalse(gameEnder.gameEnding);

    manualVehicle.Spawn();
    Assert.IsTrue(manualVehicle.vehicle.Spawned);

    // Vehicle spawned with pawns on map
    using (new GameEnderBlock())
    {
      manualVehicle.DisembarkAll();
      gameEnder.CheckOrUpdateGameOver();
      Expect.IsFalse(gameEnder.gameEnding);
    }

    // Vehicle spawned with no pawns in map, has passengers
    using (new GameEnderBlock())
    {
      manualVehicle.BoardAll();
      gameEnder.CheckOrUpdateGameOver();
      Expect.IsFalse(gameEnder.gameEnding);
    }

    // Vehicle spawned with no pawns in map, no passengers
    using (new GameEnderBlock())
    {
      manualVehicle.BoardAll();
      manualVehicle.vehicle.DeSpawn();
      Assert.IsFalse(manualVehicle.vehicle.Spawned);

      gameEnder.CheckOrUpdateGameOver();
      Expect.IsTrue(gameEnder.gameEnding);
    }

    // Autonomous Vehicle spawned with no pawns in map, no passengers
    using (new GameEnderBlock())
    {
      manualVehicle.BoardAll();
      manualVehicle.vehicle.DeSpawn();
      Assert.IsFalse(manualVehicle.vehicle.Spawned);

      gameEnder.CheckOrUpdateGameOver();
      Expect.IsFalse(gameEnder.gameEnding);
    }

    // Vehicle in caravan with passengers

    // Aerial vehicle with passengers
  }

  [Test]
  private void EventMapHolding()
  {
    // Autonomous Vehicle in map with no passengers
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

  private readonly struct GameEnderBlock : IDisposable
  {
    private static readonly FieldInfo ticksToGameOverField;

    private readonly GameEnder gameEnder;

    static GameEnderBlock()
    {
      ticksToGameOverField = AccessTools.Field(typeof(GameEnder), "ticksToGameOver");
      Assert.IsNotNull(ticksToGameOverField);
    }

    public GameEnderBlock(GameEnder gameEnder)
    {
      this.gameEnder = gameEnder;
      gameEnder.gameEnding = false;
      ticksToGameOverField.SetValue(gameEnder, 0);
    }

    void IDisposable.Dispose()
    {
      gameEnder.gameEnding = false;
      ticksToGameOverField.SetValue(gameEnder, 0);
    }
  }
}