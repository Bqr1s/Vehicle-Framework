﻿using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles
{
  public class JobGiver_GotoNearestHostile : ThinkNode_JobGiver
  {
    private const int ExpiryInterval = 360;

    private LocomotionUrgency urgency = LocomotionUrgency.Jog;
    private bool ignoreNonCombatants;
    private bool humanlikesOnly;
    private int overrideExpiryInterval = -1;
    private int overrideInstancedExpiryInterval = -1;

    public override ThinkNode DeepCopy(bool resolve = true)
    {
      JobGiver_GotoNearestHostile jobGiver_AIGotoNearestHostile =
        (JobGiver_GotoNearestHostile)base.DeepCopy(resolve);
      jobGiver_AIGotoNearestHostile.ignoreNonCombatants = ignoreNonCombatants;
      jobGiver_AIGotoNearestHostile.humanlikesOnly = humanlikesOnly;
      jobGiver_AIGotoNearestHostile.overrideExpiryInterval = overrideExpiryInterval;
      jobGiver_AIGotoNearestHostile.overrideInstancedExpiryInterval =
        overrideInstancedExpiryInterval;
      return jobGiver_AIGotoNearestHostile;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
      VehiclePawn vehicle = pawn as VehiclePawn;

      // Should never be in any think trees for non-vehicle pawns
      Assert.IsNotNull(vehicle);

      float minDist = float.MaxValue;
      Thing target = null;
      List<IAttackTarget> potentialTargetsFor =
        vehicle.Map.attackTargetsCache.GetPotentialTargetsFor(vehicle);
      for (int i = 0; i < potentialTargetsFor.Count; i++)
      {
        IAttackTarget attackTarget = potentialTargetsFor[i];
        // Threat Disabled
        if (attackTarget.ThreatDisabled(vehicle)) continue;
        // Dormant / Non-targetable
        if (!AttackTargetFinder.IsAutoTargetable(attackTarget)) continue;
        // Humanlikes-only
        if (humanlikesOnly && attackTarget.Thing is Pawn targetPawn &&
          !targetPawn.RaceProps.Humanlike) continue;
        // No line of sight
        //if (attackTarget.Thing is Pawn innerTargetPawn && (innerTargetPawn.IsCombatant() || !ignoreNonCombatants) 
        //	&& !GenSight.LineOfSightToThing(vehicle.Position, innerTargetPawn, vehicle.Map, false, null)) continue;

        Thing thing = (Thing)attackTarget;
        int dist = thing.Position.DistanceToSquared(vehicle.Position);
        if (dist < minDist && vehicle.CanReachVehicle(thing.Position, PathEndMode.Touch,
          Danger.Deadly, TraverseMode.ByPawn))
        {
          minDist = dist;
          target = thing;
        }
      }
      if (target != null)
      {
        float radius = target is VehiclePawn targetVehicle ?
          Mathf.Max(targetVehicle.VehicleDef.Size.x, targetVehicle.VehicleDef.Size.z) * 2 :
          10;
        if (!PathingHelper.TryFindNearestStandableCell(vehicle, target.Position, out IntVec3 result,
          radius: radius))
        {
          Log.Error($"Couldn't find standable cell near {target}");
          return null;
        }
        Job job = JobMaker.MakeJob(JobDefOf.Goto, result);
        job.locomotionUrgency = urgency;
        job.checkOverrideOnExpire = true;
        if (overrideInstancedExpiryInterval > 0)
        {
          job.instancedExpiryInterval = overrideInstancedExpiryInterval;
        }
        else
        {
          job.expiryInterval =
            (overrideExpiryInterval > 0) ? overrideExpiryInterval : ExpiryInterval;
        }
        job.collideWithPawns = true;
        return job;
      }
      return null;
    }
  }
}