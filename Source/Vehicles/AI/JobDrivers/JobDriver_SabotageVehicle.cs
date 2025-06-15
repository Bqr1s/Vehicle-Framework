using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles;

public class JobDriver_SabotageVehicle : JobDriver_WorkVehicle
{
  private const int ChargeBaseTicks = 60;
  private const int MaxChargeTicks = 600;
  private const int ExplosionDamage = 100;
  private const float ArmorPenetration = 2; // Full penetration

  protected override JobDef JobDef => JobDefOf_Vehicles.SabotageVehicle;

  protected override StatDef Stat => StatDefOf.ConstructionSpeed;

  protected override float TotalWork => Vehicle.GetStatValue(VehicleStatDefOf.WorkToSabotage);

  protected override void WorkComplete(Pawn actor)
  {
    AttachExplosive(actor);
    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
  }

  private void AttachExplosive(Pawn culprit)
  {
    Vehicle.vehiclePather.StopDead();
    IntVec2 offset =
      VehicleStatHandler.AdjustFromVehiclePosition(Vehicle, Vehicle.Position.ToIntVec2);
    IntVec2 size = Vehicle.VehicleDef.Size;
    int explosionSize = Mathf.Min(size.x, size.z);
    Vehicle.AddTimedExplosion(new TimedExplosion.Data(offset,
      Mathf.Min(ChargeBaseTicks * size.Area, MaxChargeTicks),
      explosionSize, DamageDefOf.Bomb, ExplosionDamage, ArmorPenetration,
      notifyNearbyPawns: true));
  }
}