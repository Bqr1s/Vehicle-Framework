using RimWorld;
using SmashTools;
using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestVehicleHandler : UnitTestMapTest
  {
    public override string Name => "VehicleHandler";

    protected override bool RefreshGrids => false;

    protected override bool ShouldTest(VehicleDef vehicleDef)
    {
      return vehicleDef.properties.roles.NotNullAndAny(role => role.Slots > 0);
    }

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      CameraJumper.TryJump(vehicle.Position, TestMap, mode: CameraJumper.MovementMode.Cut);

      UTResult result = new();

      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"VehicleHandler_{vehicle.VehicleDef} (Spawned)", vehicle.Spawned);
      result.Add($"VehicleHandler (Position)", vehicle.Position == root);

      // Colonists can board
      int total = vehicle.SeatsAvailable;
      for (int i = 0; i < total; i++)
      {
        Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Assert.IsTrue(colonist.Faction == Faction.OfPlayer, "Unable to generate colonist");
        result.Add($"VehicleHandler (Colonist_{i})", vehicle.TryAddPawn(colonist));
      }

      // Colonist cannot board full vehicle
      Pawn failColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsTrue(failColonist.Faction == Faction.OfPlayer, "Unable to generate colonist");
      result.Add("VehicleHandler (Full Capacity)", !vehicle.TryAddPawn(failColonist));
      failColonist.Destroy();

      vehicle.DestroyPawns();

      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsTrue(animal.Faction == Faction.OfPlayer, "Unable to generate pet");
      result.Add("VehicleHandler (Pet)", vehicle.TryAddPawn(animal));

      vehicle.DestroyPawns();

      if (ModsConfig.BiotechActive)
      {
        Pawn mechanoid = PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Warqueen, Faction.OfPlayer);
        Assert.IsTrue(mechanoid.Faction == Faction.OfPlayer, "Unable to generate mech");
        result.Add("VehicleHandler (Mech)", vehicle.TryAddPawn(mechanoid));
      }

      return result;
    }
  }
}