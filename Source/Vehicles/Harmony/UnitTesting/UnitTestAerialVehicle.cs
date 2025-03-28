using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestAerialVehicle : UnitTest
  {
    public override TestType ExecuteOn => TestType.GameLoaded;

    public override string Name => "AerialVehicle";

    public override IEnumerable<UTResult> Execute()
    {
      World world = Find.World;
      Assert.IsNotNull(world);
      Map map = Find.CurrentMap;
      Assert.IsNotNull(map);

      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        if (vehicleDef.vehicleType != VehicleType.Air) continue;
        if (!vehicleDef.properties.roles.NotNullAndAny(role => role.SlotsToOperate > 0)) continue;

        // TODO - Add material pool watcher, and material pool unit tests
        VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
        AerialVehicleInFlight aerialVehicle = AerialVehicleInFlight.Create(vehicle, map.Tile);

        yield return TestAerialVehicleInit(aerialVehicle);
        yield return TestAerialVehicleGC(aerialVehicle);

        vehicle.Destroy();
        aerialVehicle.Destroy();
      }
    }

    private UTResult TestAerialVehicleInit(AerialVehicleInFlight aerialVehicle)
    {
      UTResult result = new();

      VehiclePawn vehicle = aerialVehicle.vehicle;
      Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsTrue(colonist != null && colonist.Faction == Faction.OfPlayer,
        "Unable to generate colonist");
      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsTrue(animal != null && animal.Faction == Faction.OfPlayer, "Unable to generate pet");

      VehicleHandler handler = vehicle.handlers.FirstOrDefault();
      Assert.IsNotNull(handler, "Testing with aerial vehicle which has no roles");
      result.Add("AerailVehicle (Add Pawn)", vehicle.TryAddPawn(colonist, handler));
      result.Add("AerialVehicle (Add Pet)", vehicle.inventory.innerContainer
       .TryAddOrTransfer(animal, canMergeWithExistingStacks: false));
      result.Add("AerialVehicle (Vehicle Destroyed)", !vehicle.Destroyed);
      result.Add("AerialVehicle (Vehicle Discarded)", !vehicle.Discarded);

      return result;
    }

    private UTResult TestAerialVehicleGC(AerialVehicleInFlight aerialVehicle)
    {
      UTResult result = new(onFail: Find.WorldPawns.gc.LogGC);

      VehiclePawn vehicle = aerialVehicle.vehicle;

      // Pass vehicle and passengers to world
      Find.WorldPawns.PassToWorld(vehicle);
      foreach (Pawn pawn in vehicle.AllPawnsAboard)
      {
        result.Add("AerialVehicle (Pawn Destroyed)", !pawn.Destroyed);
        result.Add("AerialVehicle (Pawn Discarded)", !pawn.Discarded);
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
          result.Add("AerialVehicle (InvPawn Destroyed)", !pawn.Destroyed);
          result.Add("AerialVehicle (InvPawn Discarded)", !pawn.Discarded);
          Find.WorldPawns.PassToWorld(pawn);
        }
      }
      result.Add("AerialVehicle (ParentHolder)",
        vehicle.ParentHolder is AerialVehicleInFlight aerialWorldObject &&
        aerialWorldObject == aerialVehicle);

      result.Add("AerialVehicle (Pawn ParentHolder)", vehicle.AllPawnsAboard.All(PawnInVehicle));
      result.Add("AerialVehicle (Thing ParentHolder)",
        vehicle.inventory.innerContainer.All(ThingInVehicle));

      Find.WorldPawns.gc.CancelGCPass();
      _ = Find.WorldPawns.gc.PawnGCPass();

      Find.WorldPawns.gc.PawnGCDebugResults();
      result.Add("AerialVehicle (Vehicle GC Destroyed)", !vehicle.Destroyed);
      result.Add("AerialVehicle (Vehicle GC Discarded)", !vehicle.Discarded);
      result.Add("AerialVehicle (Pawn GC Destroyed)",
        vehicle.AllPawnsAboard.All(pawn => !pawn.Destroyed));
      result.Add("AerialVehicle (Pawn GC Discarded)",
        vehicle.AllPawnsAboard.All(pawn => !pawn.Discarded));
      result.Add("AerialVehicle (Thing GC Destroyed)",
        vehicle.inventory.innerContainer.All(thing => !thing.Destroyed));
      result.Add("AerialVehicle (Thing GC Discarded)",
        vehicle.inventory.innerContainer.All(thing => !thing.Discarded));

      return result;

      bool PawnInVehicle(Pawn pawn)
      {
        return pawn.GetVehicle() == vehicle;
      }

      bool ThingInVehicle(Thing thing)
      {
        if (thing is Pawn pawn)
        {
          return pawn.ParentHolder is Pawn_InventoryTracker inventoryTracker &&
            inventoryTracker.pawn == vehicle;
        }
        return thing.ParentHolder == vehicle.inventory.innerContainer;
      }
    }
  }
}