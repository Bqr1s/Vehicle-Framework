using System;
using System.Collections.Generic;
using System.Linq;
using DevTools;
using RimWorld;
using Verse.AI;
using Verse;
using SmashTools.Performance;
using Verse.AI.Group;

namespace Vehicles
{
  [NoProfiling]
  public class JobGiver_AIBreachWalls : JobGiver_VehicleAI
  {
    protected override bool TryFindShootingPosition(VehiclePawn vehicle, out IntVec3 position)
    {
      throw new NotImplementedException();
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
      const int RegionLookCount = 9;

      VehiclePawn vehicle = pawn as VehiclePawn;
      Assert.IsNotNull(vehicle);
      VehicleDef vehicleDef = vehicle.VehicleDef;
      IntVec3 cell = vehicle.mindState.duty.focus.Cell;
      if (cell.IsValid && cell.DistanceToSquared(vehicle.Position) < 100f &&
        VehicleRegionAndRoomQuery.RoomAtFast(cell, vehicle.Map, vehicleDef) ==
        VehicleRegionAndRoomQuery.RoomAtFast(vehicle.Position, vehicle.Map, vehicleDef) &&
        cell.WithinRegions(vehicle.Position, vehicle.Map, vehicleDef, RegionLookCount,
          TraverseMode.NoPassClosedDoors))
      {
        vehicle.GetLord().Notify_ReachedDutyLocation(vehicle);
        return null;
      }
      if (!cell.IsValid)
      {
        IAttackTarget attackTarget;
        if (!(from x in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn)
              where !x.ThreatDisabled(pawn) && x.Thing.Faction == Faction.OfPlayer &&
                pawn.CanReach(x.Thing, PathEndMode.OnCell, Danger.Deadly, false, false,
                  TraverseMode.PassAllDestroyableThings)
              select x).TryRandomElement(out attackTarget))
        {
          return null;
        }
        cell = attackTarget.Thing.Position;
      }
      if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly, false, false,
        TraverseMode.PassAllDestroyableThings))
      {
        return null;
      }
      //using (PawnPath pawnPath = pawn.Map.pathFinder.FindPath(pawn.Position, cell,
      //  TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings, false, false,
      //    false), PathEndMode.OnCell, null))
      //{
      //  IntVec3 cellBeforeBlocker;
      //  Thing thing = pawnPath.FirstBlockingBuilding(out cellBeforeBlocker, pawn);
      //  if (thing != null)
      //  {
      //    Job job = DigUtility.PassBlockerJob(pawn, thing, cellBeforeBlocker, this.canMineMineables,
      //      this.canMineNonMineables);
      //    if (job != null)
      //    {
      //      return job;
      //    }
      //  }
      //}
      //return JobMaker.MakeJob(JobDefOf.Goto, intVec, 500, true);
      return null;
    }
  }
}