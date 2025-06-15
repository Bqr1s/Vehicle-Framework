using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Vehicles;

public class LordToil_AssaultColonyArmored : LordToil
{
  public override bool ForceHighStoryDanger => true;

  public override bool AllowSatisfyLongNeeds => false;

  public override void Init()
  {
    base.Init();
    LessonAutoActivator.TeachOpportunity(ConceptDefOf.Drafting, OpportunityType.Critical);
  }

  public override void UpdateAllDuties()
  {
    foreach (Pawn pawn in lord.ownedPawns)
    {
      if (pawn is VehiclePawn vehicle)
      {
        vehicle.mindState.duty = new PawnDuty(DutyDefOf_Vehicles.VF_RangedAggressive);
      }
      else
      {
        pawn.mindState.duty = new PawnDuty(DutyDefOf.Follow);
      }
    }
  }
}