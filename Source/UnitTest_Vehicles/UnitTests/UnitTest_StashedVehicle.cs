using System.Linq;
using DevTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_StashedVehicle : UnitTest_VehicleTest
{
  [Test]
  private void Recovery()
  {
    CameraJumper.TryShowWorld();
    Assert.IsTrue(WorldRendererUtility.WorldRendered);
    Map map = Find.CurrentMap;
    Assert.IsNotNull(map);
    World world = Find.World;
    Assert.IsNotNull(world);

    VehicleDef vehicleDef =
      DefDatabase<VehicleDef>.AllDefsListForReading.RandomOrDefault(def =>
        def.type == VehicleType.Land);
    Assert.IsNotNull(vehicleDef);

    VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
    Assert.IsNotNull(vehicle);

    Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
    Assert.IsNotNull(colonist);
    Assert.IsTrue(colonist.Faction == Faction.OfPlayer);
    Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
    Assert.IsNotNull(animal);
    Assert.IsTrue(animal.Faction == Faction.OfPlayer);

    VehicleRoleHandler handler = vehicle.handlers.FirstOrDefault();
    Assert.IsNotNull(handler);
    Assert.IsTrue(vehicle.TryAddPawn(colonist, handler));
    Assert.IsTrue(
      vehicle.inventory.innerContainer.TryAddOrTransfer(animal,
        canMergeWithExistingStacks: false));
    Assert.IsFalse(vehicle.Destroyed);
    Assert.IsFalse(vehicle.Discarded);

    VehicleCaravan vehicleCaravan =
      CaravanHelper.MakeVehicleCaravan([vehicle], Faction.OfPlayer, map.Tile, true);
    vehicleCaravan.Tile = map.Tile;

    StashedVehicle stashedVehicle = StashedVehicle.Create(vehicleCaravan, out Caravan caravan);

    Expect.IsTrue(stashedVehicle.Vehicles.Contains(vehicle), "Vehicle Stashed");

    Assert.IsNotNull(caravan);
    Expect.IsTrue(caravan.PawnsListForReading.Contains(colonist), "Passenger Transferred");
    Expect.IsTrue(caravan.PawnsListForReading.Contains(animal), "Animal Transferred");
    Expect.IsEmpty(vehicle.AllPawnsAboard, "Vehicle DisembarkAll");
    Expect.IsFalse(vehicle.inventory.innerContainer.Contains(animal), "Animal Not Itemized");

    Find.WorldPawns.gc.CancelGCPass();
    _ = Find.WorldPawns.gc.PawnGCPass();

    Expect.IsFalse(vehicle.Destroyed, "Vehicle GC Destroyed");
    Expect.IsFalse(vehicle.Discarded, "Vehicle GC Discarded");

    foreach (Pawn pawn in caravan.PawnsListForReading)
    {
      Expect.IsFalse(pawn.Destroyed, "Passenger GC Destroyed");
      Expect.IsFalse(pawn.Discarded, "Passenger GC Discarded");
    }

    VehicleCaravan mergedVehicleCaravan = stashedVehicle.Notify_CaravanArrived(caravan);
    Assert.IsNotNull(mergedVehicleCaravan);
    Expect.IsTrue(caravan.Destroyed, "Caravan Destroyed");
    Expect.IsTrue(stashedVehicle.Destroyed, "StashedVehicle Destroyed");
    Expect.IsTrue(mergedVehicleCaravan.ContainsPawn(vehicle), "Vehicle Merged Into Caravan");

    Find.WorldPawns.gc.CancelGCPass();
    _ = Find.WorldPawns.gc.PawnGCPass();

    Expect.IsFalse(vehicle.Destroyed, "Vehicle GC Destroyed");
    Expect.IsFalse(vehicle.Discarded, "Vehicle GC Discarded");

    foreach (Pawn pawn in mergedVehicleCaravan.PawnsListForReading)
    {
      Expect.IsFalse(pawn.Destroyed, "Passenger GC Destroyed");
      Expect.IsFalse(pawn.Discarded, "Passenger GC Discarded");
    }

    mergedVehicleCaravan.Destroy();
    Assert.IsTrue(mergedVehicleCaravan.Destroyed);
    Find.WorldPawns.RemoveAndDiscardPawnViaGC(vehicle);
    Assert.IsFalse(Find.WorldPawns.Contains(vehicle));
  }
}