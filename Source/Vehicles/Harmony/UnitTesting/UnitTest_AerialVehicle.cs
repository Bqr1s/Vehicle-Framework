using System.Collections.Generic;
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
  internal class UnitTest_AerialVehicle : UnitTest_VehicleTest
  {
    private readonly List<AerialVehicleInFlight> aerialVehicles = [];

    [Prepare]
    private void GenerateVehicles()
    {
      World world = Find.World;
      Assert.IsNotNull(world);
      Map map = Find.CurrentMap;
      Assert.IsNotNull(map);

      aerialVehicles.Clear();

      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        if (vehicleDef.vehicleType != VehicleType.Air) continue;
        if (!vehicleDef.properties.roles.NotNullAndAny(role => role.SlotsToOperate > 0)) continue;

        VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
        AerialVehicleInFlight aerialVehicle = AerialVehicleInFlight.Create(vehicle, map.Tile);
        aerialVehicles.Add(aerialVehicle);
      }
    }

    [Test, ExecutionPriority(Priority.First)]
    private void AerialVehicleInit()
    {
      foreach (AerialVehicleInFlight aerialVehicle in aerialVehicles)
      {
        using Test.Group group = new(aerialVehicle.vehicle.def.defName);
        VehiclePawn vehicle = aerialVehicle.vehicle;
        Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Assert.IsNotNull(colonist);
        Assert.IsTrue(colonist.Faction == Faction.OfPlayer);
        Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
        Assert.IsNotNull(animal);
        Assert.IsTrue(animal.Faction == Faction.OfPlayer);

        VehicleHandler handler = vehicle.handlers.FirstOrDefault();
        Assert.IsNotNull(handler, "Testing with aerial vehicle which has no roles");
        Expect.IsTrue("AerailVehicle (Add Pawn)", vehicle.TryAddPawn(colonist, handler));
        Expect.IsTrue("AerialVehicle (Add Pet)", vehicle.inventory.innerContainer
         .TryAddOrTransfer(animal, canMergeWithExistingStacks: false));
        Expect.IsFalse("AerialVehicle (Vehicle Destroyed)", vehicle.Destroyed);
        Expect.IsFalse("AerialVehicle (Vehicle Discarded)", vehicle.Discarded);
      }
    }

    [Test]
    private void AerialVehicleGC()
    {
      foreach (AerialVehicleInFlight aerialVehicle in aerialVehicles)
      {
        using Test.Group group = new(aerialVehicle.vehicle.def.defName);

        VehiclePawn vehicle = aerialVehicle.vehicle;

        // Pass vehicle and passengers to world
        Find.WorldPawns.PassToWorld(vehicle);
        foreach (Pawn pawn in vehicle.AllPawnsAboard)
        {
          Expect.IsFalse("AerialVehicle (Pawn Destroyed)", pawn.Destroyed);
          Expect.IsFalse("AerialVehicle (Pawn Discarded)", pawn.Discarded);
          if (!pawn.IsWorldPawn())
          {
            Find.WorldPawns.PassToWorld(pawn);
          }
        }
        // Pass inventory pawns to world
        foreach (Thing thing in vehicle.inventory.innerContainer)
        {
          if (thing is Pawn pawn && !pawn.IsWorldPawn())
          {
            Expect.IsFalse("AerialVehicle (InvPawn Destroyed)", pawn.Destroyed);
            Expect.IsFalse("AerialVehicle (InvPawn Discarded)", pawn.Discarded);
            Find.WorldPawns.PassToWorld(pawn);
          }
        }
        Expect.IsTrue("AerialVehicle (ParentHolder)",
          vehicle.ParentHolder is AerialVehicleInFlight aerialWorldObject &&
          aerialWorldObject == aerialVehicle);

        Expect.IsTrue("AerialVehicle (Pawn ParentHolder)",
          vehicle.AllPawnsAboard.All(pawn => ThingInVehicle(vehicle, pawn)));
        Expect.IsTrue("AerialVehicle (Thing ParentHolder)",
          vehicle.inventory.innerContainer.All(pawn => ThingInVehicle(vehicle, pawn)));

        Find.WorldPawns.gc.CancelGCPass();
        _ = Find.WorldPawns.gc.PawnGCPass();

        Find.WorldPawns.gc.PawnGCDebugResults();
        Expect.IsFalse("AerialVehicle (Vehicle GC Destroyed)", vehicle.Destroyed);
        Expect.IsFalse("AerialVehicle (Vehicle GC Discarded)", vehicle.Discarded);
        Expect.IsTrue("AerialVehicle (Pawn GC Destroyed)",
          vehicle.AllPawnsAboard.All(pawn => !pawn.Destroyed));
        Expect.IsTrue("AerialVehicle (Pawn GC Discarded)",
          vehicle.AllPawnsAboard.All(pawn => !pawn.Discarded));
        Expect.IsTrue("AerialVehicle (Thing GC Destroyed)",
          vehicle.inventory.innerContainer.All(thing => !thing.Destroyed));
        Expect.IsTrue("AerialVehicle (Thing GC Discarded)",
          vehicle.inventory.innerContainer.All(thing => !thing.Discarded));
      }
      return;

      static bool ThingInVehicle(VehiclePawn vehicle, Thing thing)
      {
        if (thing is Pawn pawn)
        {
          return pawn.ParentHolder is Pawn_InventoryTracker inventoryTracker &&
            inventoryTracker.pawn == vehicle;
        }
        // ReSharper disable PossibleUnintendedReferenceComparison
        return thing.ParentHolder == vehicle.inventory;
      }
    }

    [CleanUp, ExecutionPriority(Priority.AboveNormal)]
    private void DestroyAll()
    {
      foreach (AerialVehicleInFlight aerialVehicle in aerialVehicles)
      {
        aerialVehicle.Destroy();
      }
    }
  }
}