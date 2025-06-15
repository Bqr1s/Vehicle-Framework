using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_StashedVehicle : UnitTest_VehicleTest
  {
    [Test]
    private void Recovery()
    {
      CameraJumper.TryShowWorld();
      Assert.IsTrue(WorldRendererUtility.WorldRenderedNow);
      Map map = Find.CurrentMap;
      Assert.IsNotNull(map);
      World world = Find.World;
      Assert.IsNotNull(world);

      VehicleDef vehicleDef =
        DefDatabase<VehicleDef>.AllDefsListForReading.RandomOrDefault(def =>
          def.vehicleType == VehicleType.Land);
      Assert.IsNotNull(vehicleDef);

      VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
      Assert.IsNotNull(vehicle);

      Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsNotNull(colonist);
      Assert.IsTrue(colonist.Faction == Faction.OfPlayer);
      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsNotNull(animal);
      Assert.IsTrue(animal.Faction == Faction.OfPlayer);

      VehicleHandler handler = vehicle.handlers.FirstOrDefault();
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

      Expect.IsTrue("Vehicle Stashed", stashedVehicle.Vehicles.Contains(vehicle));

      Assert.IsNotNull(caravan);
      Expect.IsTrue("Passenger Transferred", caravan.PawnsListForReading.Contains(colonist));
      Expect.IsTrue("Animal Transferred", caravan.PawnsListForReading.Contains(animal));
      Expect.IsTrue("Vehicle DisembarkAll", vehicle.AllPawnsAboard.NullOrEmpty());
      Expect.IsFalse("Animal Not Itemized", vehicle.inventory.innerContainer.Contains(animal));

      Find.WorldPawns.gc.CancelGCPass();
      _ = Find.WorldPawns.gc.PawnGCPass();

      Expect.IsFalse("Vehicle GC Destroyed", vehicle.Destroyed);
      Expect.IsFalse("Vehicle GC Discarded", vehicle.Discarded);

      foreach (Pawn pawn in caravan.PawnsListForReading)
      {
        Expect.IsFalse("Pawn GC Destroyed", pawn.Destroyed);
        Expect.IsFalse("Pawn GC Discarded", pawn.Discarded);
      }

      VehicleCaravan mergedVehicleCaravan = stashedVehicle.Notify_CaravanArrived(caravan);
      Assert.IsNotNull(mergedVehicleCaravan);
      Expect.IsFalse("Caravan GC Destroyed", caravan.Destroyed);
      Expect.IsFalse("StashedVehicle GC Discarded", stashedVehicle.Destroyed);
      Expect.IsTrue("Vehicle Merged Into Caravan", mergedVehicleCaravan.ContainsPawn(vehicle));

      Find.WorldPawns.gc.CancelGCPass();
      _ = Find.WorldPawns.gc.PawnGCPass();

      Expect.IsFalse("Vehicle GC Destroyed", vehicle.Destroyed);
      Expect.IsFalse("Vehicle GC Discarded", vehicle.Discarded);

      foreach (Pawn pawn in mergedVehicleCaravan.PawnsListForReading)
      {
        Expect.IsFalse("Pawn GC Destroyed", pawn.Destroyed);
        Expect.IsFalse("Pawn GC Discarded", pawn.Discarded);
      }

      mergedVehicleCaravan.Destroy();
      Assert.IsTrue(mergedVehicleCaravan.Destroyed);
      Find.WorldPawns.RemoveAndDiscardPawnViaGC(vehicle);
      Assert.IsFalse(Find.WorldPawns.Contains(vehicle));
    }
  }
}