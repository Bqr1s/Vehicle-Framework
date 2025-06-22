using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Vehicles
{
  public class LordToil_PrepareCaravan_BoardVehicles : LordToil, IDebugLordMeetingPoint
  {
    private IntVec3 meetingPoint;

    public LordToil_PrepareCaravan_BoardVehicles(IntVec3 meetingPoint)
    {
      this.meetingPoint = meetingPoint;
    }

    public IntVec3 MeetingPoint => meetingPoint;

    public override float? CustomWakeThreshold
    {
      get { return new float?(0.5f); }
    }

    public override bool AllowRestingInBed
    {
      get { return false; }
    }

    public override void UpdateAllDuties()
    {
      foreach (Pawn pawn in lord.ownedPawns)
      {
        pawn.mindState.duty = pawn is VehiclePawn ?
          new PawnDuty(DutyDefOf_Vehicles.PrepareVehicleCaravan_WaitVehicle) :
          new PawnDuty(DutyDefOf_Vehicles.PrepareVehicleCaravan_BoardVehicle)
          {
            locomotion = LocomotionUrgency.Jog
          };
      }
    }

    public override void LordToilTick()
    {
      const int CheckInterval = 200;

      if (Find.TickManager.TicksGame % CheckInterval == 0)
      {
        bool allAboard = true;
        List<Pawn> pawns = lord.ownedPawns.Where(pawn => pawn is not VehiclePawn).ToList();
        foreach (Pawn pawn in pawns)
        {
          LordJob_FormAndSendVehicles lordJob = lord.LordJob as LordJob_FormAndSendVehicles;
          Assert.IsNotNull(lordJob);
          AssignedSeat assignedSeat = lordJob.GetVehicleAssigned(pawn);
          if (assignedSeat.handler is null)
            continue;

          if (assignedSeat.Vehicle.AllPawnsAboard.Contains(pawn))
          {
            // Onboard pawns do not need to be governed by the lord toil, it would just be
            // unnecessary overhead as they are despawned and managed by the vehicle.
            lord.ownedPawns.Remove(pawn);
          }
          else
          {
            allAboard = false;
          }
        }

        if (allAboard)
          lord.ReceiveMemo(MemoTrigger.PawnsOnboard);
      }
    }
  }
}