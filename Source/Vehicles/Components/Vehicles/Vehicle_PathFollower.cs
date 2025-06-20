﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;
using SmashTools;
using SmashTools.Performance;

namespace Vehicles
{
  // TODO 1.6 - Rename, must follow name convention
  public class Vehicle_PathFollower : IExposable
  {
    public const int MaxMoveTicks = 450;
    public const float SnowReductionFromWalking = 0.001f;
    public const int ClamorCellsInterval = 12;
    public const int MinCostWalk = 50;
    public const int MinCostAmble = 60;

    public const int MinCheckAheadNodes = 1;
    public const int MaxCheckAheadNodes = 5;
    public const int TicksWhileWaiting = 10;

    public const int CheckAheadNodesForCollisions = 3;
    public const int MaxCheckAheadNodesForCollisions = 8;
    protected VehiclePawn vehicle;

    private List<IntVec3> bumperCells;

    private bool moving = false;

    public IntVec3 nextCell;
    private IntVec3 lastCell;
    public IntVec3 lastPathedTargetPosition;
    private LocalTargetInfo destination;

    public float nextCellCostLeft;
    public float nextCellCostTotal = 1f;

    private int cellsUntilClamor;

    private int lastMovedTick = -999999;
    private int waitTicks = 0;

    public PawnPath curPath;

    private PawnPath
      pathToAssign; // Explicitly used for Thread Safety. TODO - check if this is actually necessary

    private PathEndMode peMode;
    private Rot8 endRot = Rot8.Invalid;

    [Obsolete]
    private List<CancellationTokenSource> tokenSources = new List<CancellationTokenSource>();

    private object pathLock = new object();
    private bool shouldStopClipping;

    private static readonly HashSet<IntVec3> collisionCells = new HashSet<IntVec3>();

    public Vehicle_PathFollower(VehiclePawn vehicle)
    {
      this.vehicle = vehicle;
      bumperCells = new List<IntVec3>();
      shouldStopClipping =
        vehicle.VehicleDef.size.x !=
        vehicle.VehicleDef.size.z; // If vehicle is not NxN, it may clip buildings at destination.
      CanEnterDoors = false; // vehicle.VehicleDef.size == IntVec2.One;

      LookAheadStartingIndex = Mathf.CeilToInt(vehicle.VehicleDef.Size.z / 2f);
      LookAheadDistance =
        MinCheckAheadNodes + LookAheadStartingIndex; // N cells away from vehicle's front;

      CollisionsLookAheadStartingIndex = Mathf.CeilToInt(vehicle.VehicleDef.Size.z / 2f);
      CollisionsLookAheadDistance =
        CheckAheadNodesForCollisions +
        CollisionsLookAheadStartingIndex; // N cells away from vehicle's front;
    }

    public bool CanEnterDoors { get; private set; }

    public int LookAheadDistance { get; private set; }

    public int LookAheadStartingIndex { get; private set; }

    public int CollisionsLookAheadDistance { get; private set; }

    public int CollisionsLookAheadStartingIndex { get; private set; }

    public bool CalculatingPath { get; internal set; }

    public LocalTargetInfo Destination => destination;

    public bool Moving => moving;

    public bool Waiting => waitTicks > 0;

    public IntVec3 LastPassableCellInPath
    {
      get
      {
        if (!Moving || curPath == null)
        {
          return IntVec3.Invalid;
        }

        if (!Destination.Cell.Impassable(vehicle.Map))
        {
          return Destination.Cell;
        }

        List<IntVec3> nodesReversed = curPath.NodesReversed;
        for (int i = 0; i < nodesReversed.Count; i++)
        {
          if (!nodesReversed[i].Impassable(vehicle.Map))
          {
            return nodesReversed[i];
          }
        }

        if (!vehicle.Position.Impassable(vehicle.Map))
        {
          return vehicle.Position;
        }

        return IntVec3.Invalid;
      }
    }

    public void RecalculatePermissions()
    {
      if (Moving && (!vehicle.CanMoveFinal || !vehicle.Drafted))
      {
        PatherFailed();
      }
    }

    public void SetEndRotation(Rot8 rot)
    {
      endRot = rot;
    }

    public void ExposeData()
    {
      Scribe_Values.Look(ref moving, nameof(moving));
      Scribe_Values.Look(ref nextCell, nameof(nextCell));
      Scribe_Values.Look(ref nextCellCostLeft, nameof(nextCellCostLeft));
      Scribe_Values.Look(ref nextCellCostTotal, nameof(nextCellCostTotal));
      Scribe_Values.Look(ref peMode, nameof(peMode));
      Scribe_Values.Look(ref cellsUntilClamor, nameof(cellsUntilClamor));
      Scribe_Values.Look(ref lastMovedTick, nameof(lastMovedTick), -999999);
      if (moving)
      {
        Scribe_TargetInfo.Look(ref destination, nameof(destination));
      }

      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        vehicle.animator?.SetBool(PropertyIds.Moving, moving);
      }
    }

    public void StartPath(LocalTargetInfo dest, PathEndMode peMode, bool ignoreReachability = false)
    {
      if (!vehicle.Drafted)
      {
        PatherFailed();
        return;
      }

      dest = (LocalTargetInfo)GenPathVehicles.ResolvePathMode(vehicle.VehicleDef, vehicle.Map,
        dest.ToTargetInfo(vehicle.Map), ref peMode);
      if (dest.HasThing && dest.ThingDestroyed)
      {
        Log.Error(vehicle + " pathing to destroyed thing " + dest.Thing);
        PatherFailed();
        return;
      }

      // TODO - Add Building and Position Recoverable extras
      if (!GenGridVehicles.Walkable(vehicle.Position, vehicle.VehicleDef, vehicle.Map) &&
        !TryRecoverFromUnwalkablePosition(error: true))
      {
        PatherFailed();
        return;
      }

      if (Moving && curPath != null && destination == dest && this.peMode == peMode)
      {
        PatherFailed();
        return;
      }

      if (!ignoreReachability &&
        !vehicle.Map.GetCachedMapComponent<VehicleMapping>()[vehicle.VehicleDef]
         .VehicleReachability.CanReachVehicle(vehicle.Position, dest, peMode,
            TraverseParms.For(TraverseMode.ByPawn, Danger.Deadly, false)))
      {
        PatherFailed();
        return;
      }

      this.peMode = peMode;
      destination = dest;
      if (NextCellDoorToWaitForOrManuallyOpen() != null || nextCellCostLeft == nextCellCostTotal)
      {
        ResetToCurrentPosition();
      }

      PawnDestinationReservationManager.PawnDestinationReservation pawnDestinationReservation =
        vehicle.Map.pawnDestinationReservationManager.MostRecentReservationFor(vehicle);
      if (pawnDestinationReservation is not null &&
        ((Destination.HasThing && pawnDestinationReservation.target != Destination.Cell) ||
          (pawnDestinationReservation.job != vehicle.CurJob &&
            pawnDestinationReservation.target != Destination.Cell)))
      {
        vehicle.Map.pawnDestinationReservationManager.ObsoleteAllClaimedBy(vehicle);
      }

      if (AtDestinationPosition())
      {
        PatherArrived();
        return;
      }

      curPath?.ReleaseToPool();
      curPath = null;
      moving = true;
      vehicle.animator?.SetBool(PropertyIds.Moving, moving);
      vehicle.EventRegistry[VehicleEventDefOf.MoveStart].ExecuteEvents();
    }

    public void StopDead()
    {
      if (!vehicle.Spawned)
      {
        return;
      }

      if (curPath != null)
      {
        vehicle.EventRegistry[VehicleEventDefOf.MoveStop].ExecuteEvents();
        curPath.ReleaseToPool();
      }

      curPath = null;
      moving = false;
      vehicle.animator?.SetBool(PropertyIds.Moving, moving);
      nextCell = vehicle.Position;
    }

    public void PatherTick()
    {
      if (pathToAssign != null)
      {
        lock (pathLock)
        {
          curPath?.ReleaseToPool(); //Release previous PawnPath for reassignment
          curPath = pathToAssign;
          pathToAssign =
            null; //Dedicated thread will only ever assign a valid path or PawnPath.NotFound, never null. MainThread handles invalidation
          CalculatingPath = false;
        }
      }

      //Fail path last minute if necessary
      if ((!vehicle.Drafted || !vehicle.CanMoveFinal) && curPath != null)
      {
        PatherFailed();
        return;
      }

      if (vehicle.stances.stunner.Stunned)
      {
        return; // TODO - apply deceleration
      }

      if (VehicleMod.settings.debug.debugDrawBumpers)
      {
        GenDraw.DrawFieldEdges(bumperCells);
      }

      // Transition between cells
      lastMovedTick = Find.TickManager.TicksGame;
      if (nextCellCostLeft > 0f)
      {
        float costsToPayTick = CostToPayThisTick();
        nextCellCostLeft -= costsToPayTick;
        return;
      }

      // Attempt setup for next cell transition
      if (moving)
      {
        TryEnterNextPathCell();
      }
    }

    public void TryResumePathingAfterLoading()
    {
      if (moving)
      {
        // Paths resumed post-load can be assumed to already be reachable. RegionGrid at this point will
        // be suspended anyways so it is not possible to do a reachability check.
        StartPath(destination, peMode, ignoreReachability: true);
      }
    }

    // Breaking name convention here to mimic RimWorld since VehiclePawn::Notify_Teleported hides
    // non-virtual parent method.
    public void Notify_Teleported()
    {
      StopDead();
      ResetToCurrentPosition();
    }

    public void ResetToCurrentPosition()
    {
      nextCell = vehicle.Position;
      nextCellCostLeft = 0f;
      nextCellCostTotal = 1f;
    }

    public Building BuildingBlockingNextPathCell()
    {
      Building edifice = nextCell.GetEdifice(vehicle.Map);
      if (edifice != null && edifice.BlocksPawn(vehicle))
      {
        return edifice;
      }

      return null;
    }

    private bool AtDestinationPosition()
    {
      return VehicleReachabilityImmediate.CanReachImmediateVehicle(vehicle, destination, peMode);
    }

    public Building_Door NextCellDoorToWaitForOrManuallyOpen()
    {
      Building_Door building_Door = vehicle.Map.thingGrid.ThingAt<Building_Door>(nextCell);
      if (building_Door != null && building_Door.SlowsPawns &&
        (!building_Door.Open || building_Door.TicksTillFullyOpened > 0) &&
        building_Door.PawnCanOpen(vehicle))
      {
        return building_Door;
      }

      return null;
    }

    public void PatherDraw()
    {
      if (curPath != null && (vehicle.Faction == Faction.OfPlayer || DebugViewSettings.drawPaths)
        && Find.Selector.IsSelected(vehicle))
      {
        curPath.DrawPath(vehicle);
      }
    }

    public bool MovedRecently(int ticks)
    {
      return Find.TickManager.TicksGame - lastMovedTick <= ticks;
    }

    public bool TryRecoverFromUnwalkablePosition(bool error = true)
    {
      bool recovered = false;
      for (int i = 0; i < GenRadial.RadialPattern.Length; i++)
      {
        IntVec3 nearestAvailableCell = vehicle.Position + GenRadial.RadialPattern[i];
        if (!vehicle.Drivable(nearestAvailableCell))
        {
          if (nearestAvailableCell == vehicle.Position)
          {
            return true;
          }

          if (error)
          {
            Log.Warning(
              $"{vehicle} on impassable cell {vehicle.Position}. Teleporting to {nearestAvailableCell}");
          }

          vehicle.Position = nearestAvailableCell;
          vehicle.Notify_Teleported();
          recovered = true;
          break;
        }
      }

      if (!recovered)
      {
        Log.Error(
          $"{vehicle} on impassable cell {vehicle.Position}. Cound not find nearby position to teleport to. Ejecting all pawns and destroying.");
        vehicle.DisembarkAll();
        vehicle.Destroy(DestroyMode.Vanish);
      }

      return recovered;
    }

    private void PatherArrived()
    {
      if (endRot.IsValid)
      {
        vehicle.FullRotation = endRot;
      }

      StopDead();
      if (vehicle.jobs.curJob != null)
      {
        vehicle.jobs.curDriver.Notify_PatherArrived();
      }
    }

    public void PatherFailed()
    {
      StopDead();
      SetEndRotation(Rot8.Invalid);
      vehicle.jobs?.curDriver?.Notify_PatherFailed();
      CalculatingPath = false;
    }

    public void EngageBrakes()
    {
      vehicle.EventRegistry[VehicleEventDefOf.Braking].ExecuteEvents();
      PatherFailed();
    }

    private void SetBumperCells()
    {
      Rot8 direction = Ext_Map.DirectionToCell(vehicle.Position, nextCell);
      if (!direction.IsValid)
      {
        direction = vehicle.FullRotation;
      }

      CellRect bumperRect;
      if (direction.IsDiagonal)
      {
        bumperRect = vehicle.MinRectShifted(new IntVec2(0, 2), direction);
      }
      else
      {
        bumperRect = vehicle.OccupiedRectShifted(new IntVec2(0, 2), direction);
      }

      bumperCells = bumperRect.ToList(); //.GetEdgeCells(direction).ToList();
    }

    private void TryEnterNextPathCell()
    {
      if (waitTicks > 0)
      {
        waitTicks--;
        return;
      }

      if (CalculatingPath)
      {
        return;
      }

      if (vehicle.beached)
      {
        vehicle.BeachShip();
        vehicle.Position =
          nextCell; // VehiclePawn::ReclaimPosition is called from set_Position patch
        vehicle.CalculateAngle();
        PatherFailed();
        return;
      }

      // No filth for now
      //if (vehicle.BodySize > 0.9f)
      //{
      //  vehicle.Map.snowGrid.AddDepth(vehicle.Position, -SnowReductionFromWalking); // TODO - add snow footprints / reduction
      //}

      PathRequest pathRequest = NeedNewPath();
      switch (pathRequest)
      {
        case PathRequest.None: // If no path request is made, continue with method
          break;
        case PathRequest.Fail: // Immediate cancellation of path, stop dead
          PatherFailed();
          return;
        case PathRequest.Wait: // Wait until path is cleared, do not cancel path
          waitTicks = TicksWhileWaiting;
          return;
        case PathRequest.NeedNew:
          TrySetNewPath_Threaded();
          break;
        default:
          throw new NotImplementedException("TryEnterNextPathCell.PathRequest");
      }

      // Wait for path to be calculated
      if (curPath == null) return;

      if (VehicleMod.settings.main.runOverPawns)
      {
        float costsToPayThisTick = CostToPayThisTick();
        float moveSpeed = 1 / (nextCellCostTotal / 60 / costsToPayThisTick);
        if (vehicle.FullRotation.IsDiagonal)
        {
          moveSpeed *= Ext_Math.Sqrt2;
        }

        WarnPawnsImpendingCollision();
        vehicle.CheckForCollisions(moveSpeed);
      }

      UpdateVehiclePosition();

      if (AtDestinationPosition())
      {
        PatherArrived();
        return;
      }

      SetupMoveIntoNextCell();
    }

    private void UpdateVehiclePosition()
    {
      if (vehicle.Position == nextCell) return;

      CellRect hitboxBeforeMoving = vehicle.OccupiedRect();
      lastCell = vehicle.Position;
      vehicle.Position = nextCell;
      vehicle.CalculateAngle();

      foreach (IntVec3 cell in hitboxBeforeMoving.AllCellsNoRepeat(vehicle.OccupiedRect()))
      {
        vehicle.Map.pathing.RecalculatePerceivedPathCostAt(cell);
      }
    }

    private void SetupMoveIntoNextCell()
    {
      if (curPath.NodesLeftCount <= 1)
      {
        Log.Error(
          $"{vehicle} at {vehicle.Position} ran out of path nodes while pathing to {destination}.");
        PatherFailed();
        return;
      }

      nextCell = curPath.ConsumeNextNode();
      if (!vehicle.DrivableFast(nextCell))
      {
        Log.Error($"{vehicle} entering {nextCell} which is impassable.");
        PatherFailed();
        return;
      }

      Rot4 nextRot = Ext_Map.DirectionToCell(vehicle.Position, nextCell);
      if (nextRot.IsValid && vehicle.PawnOccupiedCells(nextCell, nextRot)
       .Any(cell => !cell.InBounds(vehicle.Map)))
      {
        PatherFailed(); //Emergency fail if vehicle tries to go out of bounds
        return;
      }

      //Check ahead and stop prematurely if vehicle won't fit at final destination
      if (shouldStopClipping && curPath.NodesLeftCount < LookAheadStartingIndex &&
        vehicle.LocationRestrictedBySize(nextCell, vehicle.FullRotation))
      {
        PatherFailed();
        return;
      }

      float num = CostToMoveIntoCell(vehicle, vehicle.Position, nextCell);
      nextCellCostTotal = num;
      nextCellCostLeft = num;

      if (CanEnterDoors)
      {
        //Necessary for 1x1 vehicles
        Building_Door building_Door = vehicle.Map.thingGrid.ThingAt<Building_Door>(nextCell);
        if (building_Door != null)
        {
          building_Door.Notify_PawnApproaching(vehicle, num);
        }
      }

      SetBumperCells();
    }

    public static float MoveTicksAt(VehiclePawn vehicle, IntVec3 from, IntVec3 to)
    {
      float tickCost;
      if (to.x == from.x || to.z == from.z)
      {
        tickCost = vehicle.TicksPerMoveCardinal;
      }
      else
      {
        tickCost = vehicle.TicksPerMoveDiagonal;
      }

      return tickCost;
    }

    private static void LocomotionTicks(VehiclePawn vehicle, IntVec3 from, IntVec3 to,
      ref float tickCost)
    {
      Pawn locomotionUrgencySameAs = vehicle.jobs.curDriver.locomotionUrgencySameAs;
      if (locomotionUrgencySameAs is VehiclePawn locomotionVehicle &&
        locomotionUrgencySameAs != vehicle && locomotionUrgencySameAs.Spawned)
      {
        float tickCostOtherVehicle = CostToMoveIntoCell(locomotionVehicle, from, to);
        tickCost =
          Mathf.Max(tickCost, tickCostOtherVehicle); //Slow down to match other vehicle's speed
      }
      else
      {
        switch (vehicle.jobs.curJob.locomotionUrgency)
        {
          case LocomotionUrgency.Amble:
            tickCost *= 3;
            if (tickCost < MinCostAmble)
            {
              tickCost = MinCostAmble;
            }

            break;
          case LocomotionUrgency.Walk:
            tickCost *= 2;
            if (tickCost < MinCostWalk)
            {
              tickCost = MinCostWalk;
            }

            break;
          case LocomotionUrgency.Jog:
            break;
          case LocomotionUrgency.Sprint:
            tickCost = Mathf.RoundToInt(tickCost * 0.75f);
            break;
        }
      }
    }

    public static float CostToMoveIntoCell(VehiclePawn vehicle, IntVec3 from, IntVec3 to)
    {
      float tickCost = MoveTicksAt(vehicle, from, to);
      tickCost += vehicle.Map.GetCachedMapComponent<VehicleMapping>()[vehicle.VehicleDef]
       .VehiclePathGrid.PerceivedPathCostAt(to);
      // At minimum should take ~7.5 seconds per cell, any slower vehicle should be disabled
      tickCost = Mathf.Min(tickCost, MaxMoveTicks);
      if (vehicle.CurJob != null)
      {
        LocomotionTicks(vehicle, from, to, ref tickCost);
      }

      return Mathf.Max(tickCost, 1f);
    }

    private float CostToPayThisTick()
    {
      return Mathf.Max(1, nextCellCostTotal / MaxMoveTicks);
    }

    private bool TrySetNewPath()
    {
      // TODO - Should use task based approach, if the dedicated thread is busy with some large
      // grid changes, we don't want pathfinding to be blocked for literal seconds.
      PawnPath pawnPath = GenerateNewPath_Concurrent();
      if (pawnPath is null || !pawnPath.Found)
      {
        PatherFailed();
        Messages.Message("VF_NoPathForVehicle".Translate(), MessageTypeDefOf.RejectInput, false);
        return false;
      }

      curPath?.ReleaseToPool();
      curPath = pawnPath;
      return true;
    }

    /// <summary>
    /// Calculates and assigns new path to <see cref="pathToAssign"/> for reassignment from <see cref="PatherTick"/>. 
    /// This ensures <see cref="curPath"/> is only ever written to from main thread.
    /// </summary>
    internal void TrySetNewPath_Delayed()
    {
      PawnPath pawnPath = GenerateNewPath_Concurrent();
      if (pawnPath is null || !pawnPath.Found)
      {
        PatherFailed();
        Messages.Message("VF_NoPathForVehicle".Translate(), MessageTypeDefOf.RejectInput, false);
        return;
      }

      lock (pathLock)
      {
        pathToAssign?.ReleaseToPool(); //PawnPath still needs to be released back to object pool
        pathToAssign = pawnPath; //Should be null at this point in time however
      }
    }

    private void TrySetNewPath_Threaded()
    {
      CalculatingPath = true;
      VehicleMapping vehicleMapping = MapComponentCache<VehicleMapping>.GetComponent(vehicle.Map);
      if (vehicleMapping.ThreadAvailable)
      {
        AsyncPathFindAction asyncAction = AsyncPool<AsyncPathFindAction>.Get();
        asyncAction.Set(vehicle);
        vehicleMapping.dedicatedThread.Enqueue(asyncAction);
      }
      else
      {
        TrySetNewPath();
        CalculatingPath = false;
      }
    }

    public PawnPath GenerateNewPath_Concurrent()
    {
      return GenerateNewPath(CancellationToken.None);
    }

    private PawnPath GenerateNewPath(CancellationToken token)
    {
      lastPathedTargetPosition = destination.Cell;
      PawnPath pawnPath = vehicle.Map.GetCachedMapComponent<VehicleMapping>()[vehicle.VehicleDef]
       .VehiclePathFinder
       .FindVehiclePath(vehicle.Position, destination, vehicle, token, peMode: peMode);
      return pawnPath;
    }

    private PathRequest NeedNewPath()
    {
      if (CalculatingPath)
      {
        return PathRequest.None; //Delay till not calculating path
      }

      if (!destination.IsValid || curPath is null || !curPath.Found || curPath.NodesLeftCount == 0)
      {
        return PathRequest.NeedNew;
      }

      if (destination.HasThing && destination.Thing.Map != vehicle.Map)
      {
        return PathRequest.NeedNew;
      }

      foreach (IntVec3 cell in vehicle.VehicleRect(destination.Cell, Rot4.North,
        maxSizePossible: true))
      {
        if (PathingHelper.AnyVehicleBlockingPathAt(cell, vehicle) is VehiclePawn otherVehicle)
        {
          if (!otherVehicle.vehiclePather.Moving && !otherVehicle.vehiclePather.Waiting)
          {
            if (PathingHelper.TryFindNearestStandableCell(vehicle, destination.Cell,
              out IntVec3 result))
            {
              destination = result;
              return PathRequest.NeedNew;
            }

            return PathRequest.None;
          }
        }
      }

      if (vehicle.Position.InHorDistOf(curPath.LastNode, 15f) ||
        vehicle.Position.InHorDistOf(destination.Cell, 15f))
      {
        if (!VehicleReachabilityImmediate.CanReachImmediateVehicle(curPath.LastNode, destination,
          vehicle.Map, vehicle.VehicleDef, peMode))
        {
          return PathRequest.NeedNew;
        }
      }

      if (curPath.UsedRegionHeuristics && curPath.NodesConsumedCount >= 75)
      {
        return PathRequest.NeedNew;
      }

      if (lastPathedTargetPosition != destination.Cell)
      {
        float length = (vehicle.Position - destination.Cell).LengthHorizontalSquared;
        float minLengthForRecalc;
        if (length > 900f)
        {
          minLengthForRecalc = 10f;
        }
        else if (length > 289f)
        {
          minLengthForRecalc = 5f;
        }
        else if (length > 100f)
        {
          minLengthForRecalc = 3f;
        }
        else if (length > 49f)
        {
          minLengthForRecalc = 2f;
        }
        else
        {
          minLengthForRecalc = 0.5f;
        }

        if ((lastPathedTargetPosition - destination.Cell).LengthHorizontalSquared >
          (minLengthForRecalc * minLengthForRecalc))
        {
          return PathRequest.NeedNew;
        }
      }

      IntVec3 previous = IntVec3.Invalid;
      IntVec3 next;
      int nodeIndex = LookAheadStartingIndex;
      while (nodeIndex < LookAheadStartingIndex + MaxCheckAheadNodes &&
        nodeIndex < curPath.NodesLeftCount)
      {
        next = curPath.Peek(nodeIndex);
        Rot8 rot = Ext_Map.DirectionToCell(previous, next);
        if (!GenGridVehicles.Walkable(next, vehicle.VehicleDef, vehicle.Map))
        {
          return PathRequest.NeedNew;
        }

        //Should two vehicles be pathing into eachother directly, first to stop will be given a Wait request while the other will request a new path
        CellRect vehicleRect = vehicle.VehicleRect(next, rot);
        foreach (IntVec3 cell in vehicleRect)
        {
          if (PathingHelper.AnyVehicleBlockingPathAt(cell, vehicle) is VehiclePawn otherVehicle)
          {
            if (otherVehicle.vehiclePather.Moving && !otherVehicle.vehiclePather.Waiting)
            {
              return PathRequest.Wait;
            }

            return PathRequest.NeedNew;
          }
        }

        if (nodeIndex != 0 && next.AdjacentToDiagonal(previous))
        {
          //if (VehiclePathFinder.BlocksDiagonalMovement(vehicle, vehicle.Map.cellIndices.CellToIndex(next.x, previous.z)) || VehiclePathFinder.BlocksDiagonalMovement(vehicle, vehicle.Map.cellIndices.CellToIndex(previous.x, next.z)))
          //{
          //	Log.Message($"Diagonal Blocked");
          //	return PathRequest.NeedNew;
          //}
        }

        previous = next;
        nodeIndex++;
      }

      return PathRequest.None;
    }

    private void WarnPawnsImpendingCollision()
    {
      if (curPath == null) return;

      collisionCells.Clear();
      IntVec3 previous = IntVec3.Invalid;
      IntVec3 next;
      int nodeIndex = CollisionsLookAheadStartingIndex;
      while (nodeIndex < CollisionsLookAheadStartingIndex + MaxCheckAheadNodesForCollisions &&
        nodeIndex < curPath.NodesLeftCount)
      {
        next = curPath.Peek(nodeIndex);
        Rot8 rot = Ext_Map.DirectionToCell(previous, next);

        CellRect vehicleRect = vehicle.VehicleRect(next, rot).ExpandedBy(1);
        foreach (IntVec3 cell in vehicleRect)
        {
          if (!cell.InBounds(vehicle.Map) || !collisionCells.Add(cell)) continue;

          List<Thing> thingList = cell.GetThingList(vehicle.Map);
          //Reverse iterate in case a thing or pawn is destroyed from being run over
          for (int i = thingList.Count - 1; i >= 0; i--)
          {
            Thing thing = thingList[i];
            if (thing is not Pawn pawn) continue;

            Room room = RegionAndRoomQuery.RoomAt(cell, vehicle.Map, RegionType.Set_Passable);
            Room pawnRoom = RegionAndRoomQuery.GetRoom(pawn, RegionType.Set_Passable);
            if (pawnRoom == null || pawnRoom.CellCount == 1 || (room == pawnRoom
              && GenSight.LineOfSight(vehicle.Position, pawn.Position, vehicle.Map)))
            {
              pawn.Notify_DangerousVehiclePath(vehicle);
            }
          }
        }

        previous = next;
        nodeIndex++;
      }

      collisionCells.Clear();
    }

    public enum PathRequest
    {
      None,
      Wait,
      Fail,
      NeedNew
    }
  }
}