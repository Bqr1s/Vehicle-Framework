using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles
{
  public class JobGiver_AwaitOrders : ThinkNode_JobGiver
  {
    private int overrideExpiryInterval = -1;

    public override ThinkNode DeepCopy(bool resolve = true)
    {
      JobGiver_AwaitOrders jobGiver_AwaitOrders = (JobGiver_AwaitOrders)base.DeepCopy(resolve);
      jobGiver_AwaitOrders.overrideExpiryInterval = overrideExpiryInterval;
      return jobGiver_AwaitOrders;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
      VehiclePawn vehicle = pawn as VehiclePawn;
      Assert.IsNotNull(vehicle, "Trying to assign vehicle job to non-vehicle pawn.");

      if (vehicle.vehiclePather.Moving)
      {
        vehicle.vehiclePather.StopDead();
      }
      Job job = new Job(JobDefOf_Vehicles.IdleVehicle, vehicle)
      {
        checkOverrideOnExpire = true,
        expiryInterval = (overrideExpiryInterval > 0) ? overrideExpiryInterval : 180
      };
      return job;
    }
  }
}