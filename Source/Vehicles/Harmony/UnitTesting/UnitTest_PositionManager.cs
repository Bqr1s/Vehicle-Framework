using DevTools.UnitTesting;
using SmashTools;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_PositionManager : UnitTest_MapTest
  {
    [Test]
    private void Registration()
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        using VehicleTestCase vtc = new(vehicle, this);

        int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);

        IntVec3 reposition = root + new IntVec3(maxSize, 0, 0);

        VehiclePositionManager positionManager =
          map.GetCachedMapComponent<VehiclePositionManager>();
        GenSpawn.Spawn(vehicle, root, map);
        HitboxTester<VehiclePawn> positionTester = new(vehicle, root,
          positionManager.ClaimedBy,
          (claimant) => claimant == vehicle);
        positionTester.Start();

        // Validate spawned vehicle claims rect in position manager
        Expect.IsTrue("Position Manager (Spawn)", positionTester.Hitbox(true));

        // Validate position set updates valid claims
        vehicle.Position = reposition;
        Expect.IsTrue("Position Manager (set_Position)", positionTester.Hitbox(true));
        vehicle.Position = root;

        // Validate rotation set updates valid claims
        vehicle.Rotation = Rot4.East;
        Expect.IsTrue("Position Manager (set_Rotation)", positionTester.Hitbox(true));
        vehicle.Rotation = Rot4.North;

        // Validate despawning releases claim in position manager
        vehicle.DeSpawn();
        Expect.IsTrue("Position Manager (DeSpawn)", positionTester.All(false));
      }
    }
  }
}