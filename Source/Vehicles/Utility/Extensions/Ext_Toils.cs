using System;
using System.Collections.Generic;
using System.Linq;
using SmashTools;
using Verse;
using Verse.AI;

namespace Vehicles
{
  public static class Ext_Toils
  {
    public static T FailOnMoving<T>(this T jobEndable, TargetIndex index) where T : IJobEndable
    {
      jobEndable.AddEndCondition(delegate()
      {
        VehiclePawn vehicle =
          jobEndable.GetActor().jobs.curJob.GetTarget(index).Thing as VehiclePawn;
        if (vehicle is null)
        {
          Trace.Fail("Null vehicle");
          return JobCondition.Errored;
        }

        return vehicle.vehiclePather.Moving ? JobCondition.InterruptForced : JobCondition.Ongoing;
      });
      return jobEndable;
    }
  }
}