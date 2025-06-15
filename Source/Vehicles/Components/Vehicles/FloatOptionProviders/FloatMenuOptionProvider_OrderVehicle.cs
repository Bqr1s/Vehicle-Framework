﻿using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using Verse;
using Verse.AI;

namespace Vehicles;

public class FloatMenuOptionProvider_OrderVehicle : FloatMenuOptionProvider_Vehicle
{
  private static readonly List<VehiclePawn> multiSelectVehicles = [];

  protected override bool SelectedVehicleValid(VehiclePawn vehicle, FloatMenuContext context)
  {
    return vehicle.CanMoveFinal;
  }

  protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
  {
    // TODO 1.6 - vehicle.Faction != Faction.OfPlayer needed?

    FloatMenuOption option = null;
    IntVec3 clickCell = context.ClickedCell;
    if (context.IsMultiselect)
    {
      multiSelectVehicles.Clear();
      foreach (Pawn pawn in context.ValidSelectedPawns)
      {
        if (pawn is VehiclePawn vehicle && VehicleCanGoto(vehicle, clickCell).Accepted)
          multiSelectVehicles.Add(vehicle);
      }
      if (multiSelectVehicles.Count == 0)
        return null;

      option = new FloatMenuOption("GoHere".Translate(),
        delegate
        {
          VehicleOrientationController.StartOrienting(multiSelectVehicles, clickCell, clickCell);
        }, MenuOptionPriority.GoHere);
    }
    else
    {
      Pawn pawn = context.FirstSelectedPawn;
      if (pawn is not VehiclePawn vehicle)
        return null;
      foreach (ThingComp comp in vehicle.AllComps)
      {
        if (comp is VehicleComp vehicleComp)
        {
          AcceptanceReport compReport = vehicleComp.CanMove(context);
          if (!compReport.Accepted)
          {
            Messages.Message(compReport.Reason, MessageTypeDefOf.RejectInput);
            return null;
          }
        }
      }
      if (PathingHelper.TryFindNearestStandableCell(vehicle, clickCell, out IntVec3 result))
      {
        option = new FloatMenuOption("GoHere".Translate(),
          delegate { VehicleOrientationController.StartOrienting(vehicle, result, clickCell); },
          MenuOptionPriority.GoHere);
      }
      else
      {
        AcceptanceReport gotoReport = VehicleCanGoto(vehicle, clickCell);
        if (!gotoReport.Accepted)
        {
          option = new FloatMenuOption("VF_CannotMoveToCell".Translate(vehicle.LabelCap), null);
        }
      }
    }
    if (option != null)
    {
      option.isGoto = true;
      option.autoTakeable = true;
      option.autoTakeablePriority = 10f;
    }
    return option;
  }

  private static AcceptanceReport VehicleCanGoto(VehiclePawn vehicle, IntVec3 gotoLoc)
  {
    return vehicle.CanReachVehicle(gotoLoc, PathEndMode.OnCell, Danger.Deadly) ?
      true :
      "VF_CannotMoveToCell".Translate(vehicle.LabelCap);
  }

  internal static void PawnGotoAction(IntVec3 clickCell, VehiclePawn vehicle, IntVec3 gotoLoc,
    Rot8 rot)
  {
    bool jobSuccess;
    if (vehicle.Position == gotoLoc)
    {
      jobSuccess = true;
      if (vehicle.CurJobDef == JobDefOf.Goto)
      {
        vehicle.jobs.EndCurrentJob(JobCondition.Succeeded);
      }
    }
    else
    {
      if (vehicle.CurJobDef == JobDefOf.Goto && vehicle.CurJob.targetA.Cell == gotoLoc)
      {
        jobSuccess = true;
      }
      else
      {
        Job job = new(JobDefOf.Goto, gotoLoc);
        bool isOnEdge = CellRect.WholeMap(vehicle.Map).IsOnEdge(clickCell, 3);
        bool exitCell = vehicle.Map.exitMapGrid.IsExitCell(clickCell);
        bool vehicleCellsOverlapExit = vehicle.InhabitedCellsProjected(clickCell, rot)
         .NotNullAndAny(cell => cell.InBounds(vehicle.Map) &&
            vehicle.Map.exitMapGrid.IsExitCell(cell));

        if (exitCell || vehicleCellsOverlapExit)
        {
          job.exitMapOnArrival = true;
        }
        else if (!vehicle.Map.IsPlayerHome && !vehicle.Map.exitMapGrid.MapUsesExitGrid &&
          isOnEdge &&
          vehicle.Map.Parent.GetComponent<FormCaravanComp>() is { } formCaravanComp &&
          MessagesRepeatAvoider.MessageShowAllowed(
            $"MessagePlayerTriedToLeaveMapViaExitGrid-{vehicle.Map.uniqueID}", 60f))
        {
          string text = formCaravanComp.CanFormOrReformCaravanNow ?
            "MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate() :
            "MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate();
          Messages.Message(text, vehicle.Map.Parent, MessageTypeDefOf.RejectInput, false);
        }
        if (vehicle.jobs.TryTakeOrderedJob(job, JobTag.Misc))
        {
          vehicle.vehiclePather.SetEndRotation(rot);
        }
        jobSuccess = vehicle.jobs.TryTakeOrderedJob(job);
      }
    }
    if (jobSuccess)
      FleckMaker.Static(gotoLoc, vehicle.Map, FleckDefOf.FeedbackGoto);
  }
}