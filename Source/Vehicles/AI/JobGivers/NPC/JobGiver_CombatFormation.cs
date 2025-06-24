﻿using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles
{
  [StaticConstructorOnStartup]
  public abstract class JobGiver_CombatFormation : ThinkNode_JobGiver
  {
    protected static readonly Action<Pawn_MindState> Notify_EngagedTarget;

    protected bool humanlikesOnly = true;
    protected bool ignoreNonCombatants = false;

    static JobGiver_CombatFormation()
    {
      MethodInfo engagedTargetMethod =
        AccessTools.Method(typeof(Pawn_MindState), "Notify_EngagedTarget");
      Notify_EngagedTarget =
        (Action<Pawn_MindState>)Delegate.CreateDelegate(typeof(Action<Pawn_MindState>),
          engagedTargetMethod);
    }

    protected virtual IntRange ExpiryInterval => new IntRange(30, 30);

    protected virtual int TicksSinceEngageToLoseTarget => 400;

    protected virtual bool OnlyUseRanged => true;

    // Position to post up, may start shooting before / after arriving.
    protected abstract bool TryFindCombatPosition(VehiclePawn vehicle, out IntVec3 dest);

    protected virtual float TargetAcquireRadius(VehiclePawn vehicle)
    {
      return 56;
    }

    protected virtual bool CanRam(VehiclePawn vehicle)
    {
      return false;
    }

    // How far vehicle can wander from escortee / defense point
    protected virtual float GetFlagRadius(VehiclePawn vehicle)
    {
      return 999999f;
    }

    protected virtual IntVec3 GetFlagPosition(VehiclePawn vehicle)
    {
      return IntVec3.Invalid;
    }

    protected virtual bool ExtraTargetValidator(VehiclePawn vehicle, Thing target)
    {
      return target.Faction.HostileTo(vehicle.Faction) &&
        (!humanlikesOnly || target is not Pawn pawn || pawn.RaceProps.Humanlike);
    }

    public override ThinkNode DeepCopy(bool resolve = true)
    {
      JobGiver_CombatFormation jobGiver = (JobGiver_CombatFormation)base.DeepCopy(resolve);
      jobGiver.humanlikesOnly = humanlikesOnly;
      jobGiver.ignoreNonCombatants = ignoreNonCombatants;
      return jobGiver;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
      VehiclePawn vehicle = pawn as VehiclePawn;
      Assert.IsNotNull(vehicle, "Trying to assign vehicle job to non-vehicle pawn.");

      UpdateEnemyTarget(vehicle);
      if (vehicle.mindState.enemyTarget is not Thing enemyTarget)
      {
        return null;
      }
      if (enemyTarget is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible())
      {
        return null;
      }
      if (OnlyUseRanged)
      {
        if (!TryFindCombatPosition(vehicle, out IntVec3 cell))
        {
          return null;
        }
        Job job;
        if (cell == vehicle.Position)
        {
          job = JobMaker.MakeJob(JobDefOf_Vehicles.IdleVehicle, vehicle);
        }
        else
        {
          job = JobMaker.MakeJob(JobDefOf.Goto, cell);
        }
        job.expiryInterval = ExpiryInterval.RandomInRange;
        job.checkOverrideOnExpire = true;
        return job;
      }
      // TODO - Add special case for how vehicles should handle pawns being within melee range
      return null;
    }

    protected virtual bool ShouldLoseTarget(VehiclePawn vehicle)
    {
      Thing enemyTarget = vehicle.mindState.enemyTarget;
      float keepRadiusSqrd = Mathf.Pow(vehicle.VehicleDef.npcProperties.targetKeepRadius, 2);
      if (!enemyTarget.Destroyed && enemyTarget.Spawned &&
        Find.TickManager.TicksGame - vehicle.mindState.lastEngageTargetTick <=
        TicksSinceEngageToLoseTarget &&
        vehicle.CanReachVehicle(enemyTarget, PathEndMode.Touch, Danger.Deadly) &&
        (vehicle.Position - enemyTarget.Position).LengthHorizontalSquared <= keepRadiusSqrd)
      {
        return enemyTarget is IAttackTarget attackTarget && attackTarget.ThreatDisabled(vehicle);
      }
      return true;
    }

    protected abstract void UpdateEnemyTarget(VehiclePawn vehicle);
  }
}