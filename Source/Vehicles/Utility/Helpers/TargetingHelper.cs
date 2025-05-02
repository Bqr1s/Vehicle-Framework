﻿using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles
{
  /// <summary>
  /// Target util for finding best attack target for VehicleTurret
  /// </summary>
  public static class TargetingHelper
  {
    /// <summary>
    /// Find best attack target for VehicleTurret
    /// </summary>
    /// <seealso cref="BestAttackTarget(VehicleTurret, TargetScanFlags, Predicate{Thing}, float, float, IntVec3, float, bool, bool)"/>
    public static bool TryGetTarget(this VehicleTurret turret, out LocalTargetInfo targetInfo,
      TargetScanFlags? additionalFlags = null)
    {
      targetInfo = LocalTargetInfo.Invalid;
      TargetScanFlags targetScanFlags = turret.turretDef.targetScanFlags;
      if (additionalFlags != null)
      {
        targetScanFlags |= additionalFlags.Value;
      }
      Thing thing = (Thing)BestAttackTarget(turret, targetScanFlags,
        delegate(Thing thing) { return TargetMeetsRequirements(turret, thing); },
        canTakeTargetsCloserThanEffectiveMinRange: false);
      if (thing != null)
      {
        targetInfo = new LocalTargetInfo(thing);
        return true;
      }
      return false;
    }

    /// <summary>
    /// Best attack target for VehicleTurret
    /// </summary>
    public static IAttackTarget BestAttackTarget(VehicleTurret turret, TargetScanFlags flags,
      Predicate<Thing> validator = null,
      float minDist = 0f, float maxDist = 9999f, IntVec3 locus = default,
      float maxTravelRadiusFromLocus = float.MaxValue,
      bool canTakeTargetsCloserThanEffectiveMinRange = true)
    {
      VehiclePawn searcherPawn = turret.vehicle;

      float minDistSquared = minDist * minDist;
      float num = maxTravelRadiusFromLocus + turret.MaxRange;
      float maxLocusDistSquared = num * num;
      Func<IntVec3, bool> losValidator = null;
      if (flags.HasFlag(TargetScanFlags.LOSBlockableByGas))
      {
        losValidator = (pos) => pos.AnyGas(searcherPawn.Map, GasType.BlindSmoke);
      }
      Predicate<IAttackTarget> innerValidator = delegate(IAttackTarget t)
      {
        Thing thing = t.Thing;
        if (t == searcherPawn)
        {
          return false;
        }
        if (minDistSquared > 0f &&
          (searcherPawn.Position - thing.Position).LengthHorizontalSquared < minDistSquared)
        {
          return false;
        }
        if (!canTakeTargetsCloserThanEffectiveMinRange)
        {
          float num2 = turret.MinRange;
          if (num2 > 0f && (turret.vehicle.Position - thing.Position).LengthHorizontalSquared <
            num2 * num2)
          {
            return false;
          }
        }
        if (maxTravelRadiusFromLocus < 9999f &&
          (thing.Position - locus).LengthHorizontalSquared > maxLocusDistSquared)
        {
          return false;
        }
        if (!searcherPawn.HostileTo(thing))
        {
          return false;
        }
        if (validator != null && !validator(thing))
        {
          return false;
        }
        if ((flags & TargetScanFlags.NeedLOSToAll) != TargetScanFlags.None)
        {
          if (losValidator != null &&
            (!losValidator(searcherPawn.Position) || !losValidator(thing.Position)))
          {
            return false;
          }
          if (!searcherPawn.CanSee(thing, losValidator))
          {
            if (t is Pawn)
            {
              if ((flags & TargetScanFlags.NeedLOSToPawns) != TargetScanFlags.None)
              {
                return false;
              }
            }
            else if ((flags & TargetScanFlags.NeedLOSToNonPawns) != TargetScanFlags.None)
            {
              return false;
            }
          }
        }
        if (((flags & TargetScanFlags.NeedThreat) != TargetScanFlags.None ||
            (flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None) &&
          t.ThreatDisabled(searcherPawn))
        {
          return false;
        }
        if ((flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None &&
          !AttackTargetFinder.IsAutoTargetable(t))
        {
          return false;
        }
        if ((flags & TargetScanFlags.NeedActiveThreat) != TargetScanFlags.None &&
          !GenHostility.IsActiveThreatTo(t, searcherPawn.Faction))
        {
          return false;
        }
        Pawn pawn = t as Pawn;
        if ((flags & TargetScanFlags.NeedNonBurning) != TargetScanFlags.None && thing.IsBurning())
        {
          return false;
        }

        if (thing.def.size.x == 1 && thing.def.size.z == 1)
        {
          if (thing.Position.Fogged(thing.Map))
          {
            return false;
          }
        }
        else
        {
          bool flag2 = false;
          using (CellRect.Enumerator enumerator = thing.OccupiedRect().GetEnumerator())
          {
            while (enumerator.MoveNext())
            {
              if (!enumerator.Current.Fogged(thing.Map))
              {
                flag2 = true;
                break;
              }
            }
          }
          if (!flag2)
          {
            return false;
          }
        }
        return true;
      };

      List<IAttackTarget> tmpTargets = new List<IAttackTarget>();
      tmpTargets.AddRange(searcherPawn.Map.attackTargetsCache.GetPotentialTargetsFor(searcherPawn));

      bool flag = false;
      for (int i = 0; i < tmpTargets.Count; i++)
      {
        IAttackTarget attackTarget = tmpTargets[i];
        if (attackTarget.Thing.Position.InHorDistOf(searcherPawn.Position, maxDist) &&
          innerValidator(attackTarget) && VehicleTurret.TryFindShootLineFromTo(
            searcherPawn.Position, new LocalTargetInfo(attackTarget.Thing),
            out ShootLine resultingLine))
        {
          flag = true;
          break;
        }
      }
      IAttackTarget result;
      if (flag)
      {
        tmpTargets.RemoveAll((IAttackTarget x) =>
          !x.Thing.Position.InHorDistOf(searcherPawn.Position, maxDist) || !innerValidator(x));
        result = GetRandomShootingTargetByScore(tmpTargets, searcherPawn);
      }
      else
      {
        Predicate<Thing> validator2;
        if ((flags & TargetScanFlags.NeedReachableIfCantHitFromMyPos) != TargetScanFlags.None &&
          (flags & TargetScanFlags.NeedReachable) == TargetScanFlags.None)
        {
          validator2 = ((Thing t) => innerValidator((IAttackTarget)t) &&
            VehicleTurret.TryFindShootLineFromTo(searcherPawn.Position, new LocalTargetInfo(t),
              out ShootLine resultingLine));
        }
        else
        {
          validator2 = ((Thing t) => innerValidator((IAttackTarget)t));
        }
        result = (IAttackTarget)GenClosest.ClosestThing_Global(searcherPawn.Position, tmpTargets,
          maxDist, validator2, null);
      }
      tmpTargets.Clear();
      return result;
    }

    /// <summary>
    /// Get random target by weight
    /// </summary>
    /// <param name="targets"></param>
    /// <param name="searcher"></param>
    public static IAttackTarget GetRandomShootingTargetByScore(List<IAttackTarget> targets,
      VehiclePawn searcher)
    {
      if (GetAvailableShootingTargetsByScore(targets, searcher).TryRandomElementByWeight(
        (Pair<IAttackTarget, float> x) => x.Second, out Pair<IAttackTarget, float> pair))
      {
        return pair.First;
      }
      return null;
    }

    /// <summary>
    /// Get all available targets ordered by weight
    /// </summary>
    /// <param name="rawTargets"></param>
    /// <param name="searcher"></param>
    /// <returns></returns>
    public static List<Pair<IAttackTarget, float>> GetAvailableShootingTargetsByScore(
      List<IAttackTarget> rawTargets, VehiclePawn searcher)
    {
      List<Pair<IAttackTarget, float>> availableShootingTargets =
        new List<Pair<IAttackTarget, float>>();
      List<float> tmpTargetScores = new List<float>();
      List<bool> tmpCanShootAtTarget = new List<bool>();
      if (rawTargets.Count == 0)
      {
        return availableShootingTargets;
      }
      tmpTargetScores.Clear();
      tmpCanShootAtTarget.Clear();
      float num = 0f;
      IAttackTarget attackTarget = null;
      for (int i = 0; i < rawTargets.Count; i++)
      {
        tmpTargetScores.Add(float.MinValue);
        tmpCanShootAtTarget.Add(false);
        if (rawTargets[i] != searcher)
        {
          bool flag = VehicleTurret.TryFindShootLineFromTo(searcher.Position,
            new LocalTargetInfo(rawTargets[i].Thing), out ShootLine shootLine);
          tmpCanShootAtTarget[i] = flag;
          if (flag)
          {
            float shootingTargetScore = GetShootingTargetScore(rawTargets[i], searcher);
            tmpTargetScores[i] = shootingTargetScore;
            if (attackTarget == null || shootingTargetScore > num)
            {
              attackTarget = rawTargets[i];
              num = shootingTargetScore;
            }
          }
        }
      }
      if (num < 1f)
      {
        if (attackTarget != null)
        {
          availableShootingTargets.Add(new Pair<IAttackTarget, float>(attackTarget, 1f));
        }
      }
      else
      {
        float num2 = num - 30f;
        for (int j = 0; j < rawTargets.Count; j++)
        {
          if (rawTargets[j] != searcher && tmpCanShootAtTarget[j])
          {
            float num3 = tmpTargetScores[j];
            if (num3 >= num2)
            {
              float second = Mathf.InverseLerp(num - 30f, num, num3);
              availableShootingTargets.Add(new Pair<IAttackTarget, float>(rawTargets[j], second));
            }
          }
        }
      }
      return availableShootingTargets;
    }

    /// <summary>
    /// Get target score
    /// </summary>
    /// <param name="target"></param>
    /// <param name="searcher"></param>
    private static float GetShootingTargetScore(IAttackTarget target,
      IAttackTargetSearcher searcher)
    {
      float num = 60f;
      num -= Mathf.Min((target.Thing.Position - searcher.Thing.Position).LengthHorizontal, 40f);
      if (target.TargetCurrentlyAimingAt == searcher.Thing)
      {
        num += 10f;
      }
      if (searcher.LastAttackedTarget == target.Thing &&
        Find.TickManager.TicksGame - searcher.LastAttackTargetTick <= 300)
      {
        num += 40f;
      }
      num -= CoverUtility.CalculateOverallBlockChance(target.Thing.Position,
        searcher.Thing.Position, searcher.Thing.Map) * 10f;
      Pawn pawn = target as Pawn;
      if (pawn != null && pawn.RaceProps.Animal && pawn.Faction != null && !pawn.IsFighting())
      {
        num -= 50f;
      }
      //num += _  - add additional cost based on how close to friendly fire
      return num * target.TargetPriorityFactor;
    }

    public static bool TargetMeetsRequirements(VehicleTurret turret, LocalTargetInfo target)
    {
      if (target == turret.vehicle)
      {
        return false;
      }
      Map map = turret.vehicle.Map;
      if (map == null)
      {
        return false;
      }
      if (!turret.InRange(target))
      {
        return false;
      }
      if (!turret.AngleBetween(target.CenterVector3))
      {
        return false;
      }
      if (turret.ProjectileDef.projectile.flyOverhead)
      {
        return true; //Skip LOS check
      }
      if (target.HasThing && !TargetValidator(turret, map, target))
      {
        return false;
      }
      TargetScanFlags scanFlags = turret.turretDef.targetScanFlags;
      if (target.HasThing && (scanFlags.HasFlag(TargetScanFlags.NeedLOSToAll) ||
        (scanFlags.HasFlag(TargetScanFlags.NeedLOSToPawns) && target.Thing is Pawn) ||
        (scanFlags.HasFlag(TargetScanFlags.NeedLOSToNonPawns) && target.Thing is not Pawn)))
      {
        if (!target.Thing.Spawned || target.Thing.Destroyed)
        {
          return false;
        }
        if (!turret.vehicle.CanSee(target.Thing,
          validator: cell => LOSValidator(turret, map, target, cell)))
        {
          return false;
        }
      }
      return true;
    }

    private static bool LOSValidator(VehicleTurret turret, Map map, LocalTargetInfo target,
      IntVec3 cell)
    {
      TargetScanFlags scanFlags = turret.turretDef.targetScanFlags;
      if (scanFlags.HasFlag(TargetScanFlags.LOSBlockableByGas) && !LOSThroughGas(map, cell))
      {
        return false;
      }
      if (scanFlags.HasFlag(TargetScanFlags.NeedNotUnderThickRoof) && !LOSUnderRoof(map, cell))
      {
        return false;
      }
      return true;
    }

    private static bool TargetValidator(VehicleTurret turret, Map map, LocalTargetInfo target)
    {
      Assert.IsTrue(target.HasThing,
        "non-Thing target passed to TargetValidator. Will always be false if any scan flags are set.");
      TargetScanFlags scanFlags = turret.turretDef.targetScanFlags;
      if (scanFlags.HasFlag(TargetScanFlags.NeedThreat) &&
        !LOSHasThreat(turret.vehicle, target.Thing))
      {
        return false;
      }
      if (scanFlags.HasFlag(TargetScanFlags.NeedActiveThreat) &&
        !LOSHasActiveThreat(turret.vehicle, target.Thing))
      {
        return false;
      }
      if (scanFlags.HasFlag(TargetScanFlags.NeedAutoTargetable) &&
        !LOSIsAutoTargetable(target.Thing))
      {
        return false;
      }
      if (scanFlags.HasFlag(TargetScanFlags.NeedNonBurning) && !LOSIsNonBurning(target.Thing))
      {
        return false;
      }
      return true;
    }

    private static bool LOSThroughGas(Map map, IntVec3 cell)
    {
      return !cell.AnyGas(map, GasType.BlindSmoke);
    }

    private static bool LOSUnderRoof(Map map, IntVec3 cell)
    {
      RoofDef roof = cell.GetRoof(map);
      if (roof != null && roof.isThickRoof)
      {
        return false;
      }
      return true;
    }

    // TODO - Needs further testing
    private static bool LOSHasThreat(VehiclePawn vehicle, Thing thing)
    {
      if (thing is not IAttackTarget target)
      {
        return false;
      }
      return !target.ThreatDisabled(vehicle);
    }

    // TODO - Needs further testing
    private static bool LOSHasActiveThreat(VehiclePawn vehicle, Thing thing)
    {
      if (thing is not IAttackTarget target)
      {
        return false;
      }
      return GenHostility.IsActiveThreatTo(target, vehicle.Faction);
    }

    private static bool LOSIsAutoTargetable(Thing thing)
    {
      if (thing is not IAttackTarget target)
      {
        return false;
      }
      return AttackTargetFinder.IsAutoTargetable(target);
    }

    private static bool LOSIsNonBurning(Thing thing)
    {
      return thing is null || !thing.IsBurning();
    }
  }
}