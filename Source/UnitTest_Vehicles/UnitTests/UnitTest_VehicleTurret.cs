using DevTools.UnitTesting;
using SmashTools;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_VehicleTurret : UnitTest_MapTest
{
  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    return vehicleDef.GetCompProperties<CompProperties_VehicleTurrets>() is { } compTurrets &&
      !compTurrets.turrets.NullOrEmpty();
  }

  [SetUp]
  [ExecutionPriority(Priority.Last)]
  private void AlignAllRotations()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      Assert.IsNotNull(vehicle.CompVehicleTurrets);
      Assert.IsFalse(vehicle.CompVehicleTurrets.turrets.NullOrEmpty());
      vehicle.Transform.rotation = 0;
      foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.turrets)
      {
        turret.TurretRotation = turret.defaultAngleRotated;
      }
    }
  }

  [Test]
  private void TurretLocation()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      GenSpawn.Spawn(vehicle, root, map);
      Assert.IsTrue(vehicle.Spawned);

      // TEST LIST
      // - Rotation Offsets
      // - Default Angles (Rotated)
      // - Turret Location Rotated (0~360, 10deg at a time) for each rotation
      foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.turrets)
      {
        using Test.Group tg2 = new(turret.key);
        vehicle.FullRotation = Rot8.North;
        Expect.ApproximatelyEqual(turret.TurretRotation, turret.defaultAngleRotated,
          "Default Angle");

        for (int i = 0; i < 8; i++)
        {
          Rot8 rot = new(i);
          vehicle.FullRotation = rot;
          // Can't use implicit conversion, y = forward in RimWorld
          Vector2 offset = turret.renderProperties.OffsetFor(rot);
          Vector3 turretLoc = new(offset.x, 0, offset.y);
          if (turret.def.graphicData != null)
          {
            turretLoc += turret.def.graphicData.DrawOffsetForRot(rot);
          }
          Vector3 curTurretLoc = turret.TurretLocation - vehicle.DrawPos;
          Expect.ApproximatelyEqual(curTurretLoc.x, turretLoc.x,
            $"DrawOffset.x_{rot.ToStringNamed()}");
          Expect.ApproximatelyEqual(curTurretLoc.z, turretLoc.z,
            $"DrawOffset.z_{rot.ToStringNamed()}");
          Expect.ApproximatelyEqual(turret.TurretRotation,
            turret.defaultAngleRotated + rot.AsAngle, $"DefaultAngle_{rot.ToStringNamed()}");
        }
      }

      vehicle.DeSpawn();
    }
  }
}