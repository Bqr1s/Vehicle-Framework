using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using SmashTools;

namespace Vehicles;

public class ThinkNode_ExecuteAction : ThinkNode
{
  // First argument must be VehiclePawn instance if not
  // invoking instance method from VehiclePawn
  private ResolvedMethod<VehiclePawn> action;

  public override ThinkNode DeepCopy(bool resolve = true)
  {
    ThinkNode_ExecuteAction jobGiver = (ThinkNode_ExecuteAction)base.DeepCopy(resolve);
    jobGiver.action = action;
    return jobGiver;
  }

  public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
  {
    if (!pawn.Spawned) return ThinkResult.NoJob;
    if (pawn is not VehiclePawn vehicle)
    {
      Log.Error($"Trying to assign vehicle job to non-vehicle pawn {pawn}.");
      return ThinkResult.NoJob;
    }

    Assert.IsNotNull(action);
    Assert.IsTrue(action.method.IsStatic);
    action.Invoke(null, vehicle);

    return ThinkResult.NoJob;
  }
}