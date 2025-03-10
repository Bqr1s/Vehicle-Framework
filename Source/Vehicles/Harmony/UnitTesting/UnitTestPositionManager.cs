using SmashTools;
using SmashTools.Debugging;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestPositionManager : UnitTestMapTest
  {
    public override string Name => "PositionManager";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);

      UTResult result = new();
      IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);
      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];

      VehiclePositionManager positionManager =
        TestMap.GetCachedMapComponent<VehiclePositionManager>();
      GenSpawn.Spawn(vehicle, root, TestMap);
      HitboxTester<VehiclePawn> positionTester = new(vehicle, TestMap, root,
        positionManager.ClaimedBy,
        (claimant) => claimant == vehicle);
      positionTester.Start();

      // Validate spawned vehicle claims rect in position manager
      bool success = positionTester.Hitbox(true);
      result.Add("Position Manager (Spawn)", success);

      // Validate position set updates valid claims
      vehicle.Position = reposition;
      success = positionTester.Hitbox(true);
      vehicle.Position = root;
      result.Add("Position Manager (set_Position)", success);

      // Validate rotation set updates valid claims
      vehicle.Rotation = Rot4.East;
      success = positionTester.Hitbox(true);
      vehicle.Rotation = Rot4.North;
      result.Add("Position Manager (set_Rotation)", success);

      // Validate despawning releases claim in position manager
      vehicle.DeSpawn();
      success = positionTester.All(false);
      result.Add("Position Manager (DeSpawn)", success);
      return result;
    }
  }
}