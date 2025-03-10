﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace Vehicles
{
  [StaticConstructorOnStartup]
  public static class Ext_Vehicles
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRoofed(IntVec3 cell, Map map) => cell.Roofed(map);

    public static bool IsRoofRestricted(VehicleDef vehicleDef, IntVec3 cell, Map map)
    {
      var compProperties = vehicleDef.GetCompProperties<CompProperties_VehicleLauncher>();
      if (compProperties == null)
      {
        return true;
      }
      bool canRoofPunch = SettingsCache.TryGetValue(vehicleDef, typeof(CompProperties_VehicleLauncher),
                                                    nameof(CompProperties_VehicleLauncher.canRoofPunch),
                                                    compProperties.canRoofPunch);
      return IsRoofRestricted(cell, map, canRoofPunch);
    }

    public static bool IsRoofRestricted(IntVec3 cell, Map map, bool canRoofPunch)
    {
      if (!canRoofPunch)
      {
        return IsRoofed(cell, map);
      }
      RoofDef roofDef = cell.GetRoof(map);
      return roofDef != null && roofDef.isThickRoof;
    }

    /// <summary>
    /// Rotates <paramref name="cell"/> for vehicle rect.
    /// </summary>
    ///<remarks>Rotation is opposite of <paramref name="rot"/> ie. rotating 'east' will return a cell as if the cell were rotated counter-clockwise (or rotating based on the vehicle facing east). 
    ///Set <paramref name="reverseRotate"/> to true if you want the cell to be rotated in the direction of <paramref name="rot"/></remarks>
    /// <param name="cell"></param>
    /// <param name="rot"></param>
    /// <param name="size"></param>
    public static IntVec2 RotatedBy(this IntVec2 cell, Rot4 rot, IntVec2 size, bool reverseRotate = false)
    {
      if (size.x == 1 && size.z == 1)
      {
        return cell;
      }
      switch (rot.AsInt)
      {
        case 0:
          return cell;
        case 1:
          IntVec2 east = new IntVec2(-cell.z, cell.x);
          if (reverseRotate)
          {
            east.x *= -1;
            east.z *= -1;
          }
          return east;
        case 2:
          IntVec2 south = new IntVec2(-cell.x, -cell.z);
          if (size.x.IsEven())
          {
            south.x++;
          }
          if (size.z.IsEven())
          {
            south.z++;
          }
          return south;
        case 3:
          IntVec2 west = new IntVec2(cell.z, -cell.x);
          if (size.x.IsEven())
          {
            west.x++;
          }
          if (size.z.IsEven())
          {
            west.z++;
          }
          if (reverseRotate)
          {
            if (size.x.IsEven())
            {
              west.z++;
              west.x--;
            }
            if (size.z.IsEven())
            {
              west.z--;
              west.x--;
            }
            west.x *= -1;
            west.z *= -1;
          }
          return west;
        default:
          return cell;
      }
    }

    public static void CleanupVehicleHandlers(this LordJob lordJob)
    {
      foreach (Pawn pawn in lordJob.lord.ownedPawns)
      {
        if (pawn is VehiclePawn vehicle)
        {
          foreach (Pawn innerPawn in vehicle.AllPawnsAboard)
          {
            if (innerPawn.mindState != null)
            {
              innerPawn.mindState.duty = null;
            }
            lordJob.Map.attackTargetsCache.UpdateTarget(innerPawn);
            if (lordJob.EndPawnJobOnCleanup(innerPawn) && innerPawn.Spawned && innerPawn.CurJob != null &&
              (!lordJob.DontInterruptLayingPawnsOnCleanup || !RestUtility.IsLayingForJobCleanup(innerPawn)))
            {
              innerPawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
          }
        }
      }
    }

    public static CellRect VehicleRect(this VehiclePawn vehicle, bool maxSizePossible = false)
    {
      return vehicle.VehicleRect(vehicle.Position, vehicle.Rotation, maxSizePossible: maxSizePossible);
    }

    public static CellRect VehicleRect(this VehiclePawn vehicle, IntVec3 center, Rot4 rot, bool maxSizePossible = false)
    {
      return VehicleRect(vehicle.VehicleDef, center, rot, maxSizePossible: maxSizePossible);
    }

    public static CellRect VehicleRect(this VehicleDef vehicleDef, IntVec3 center, Rot4 rot, bool maxSizePossible = false)
    {
      IntVec2 size = vehicleDef.size;
      AdjustForVehicleOccupiedRect(ref size, ref rot, maxSizePossible: maxSizePossible);
      return GenAdj.OccupiedRect(center, rot, size);
    }

    public static void AdjustForVehicleOccupiedRect(ref IntVec2 size, ref Rot4 rot, bool maxSizePossible = false)
    {
      if (rot == Rot4.West) rot = Rot4.East;
      if (rot == Rot4.South) rot = Rot4.North;
      if (maxSizePossible)
      {
        int maxSize = Mathf.Max(size.x, size.z);
        size.x = maxSize;
        size.z = maxSize;
      }
    }

    public static IntVec3 PadForHitbox(this IntVec3 cell, Map map, VehiclePawn vehicle)
    {
      return PadForHitbox(cell, map, vehicle.VehicleDef);
    }

    public static IntVec3 PadForHitbox(this IntVec3 cell, Map map, VehicleDef vehicleDef)
    {
      int largestSize = Mathf.Max(vehicleDef.Size.x, vehicleDef.Size.z);
      bool even = largestSize % 2 == 0;
      int padding = Mathf.CeilToInt(largestSize / 2f);
      if (even)
      {
        padding += 1; // If size is even, add 1 to account for rotations with lower center point.  This will ensure all rotations are padded enough
      }
      if (cell.x < padding)
      {
        cell.x = padding;
      }
      else if (cell.x + padding > map.Size.x)
      {
        cell.x = map.Size.x - padding;
      }

      if (cell.z < padding)
      {
        cell.z = padding;
      }
      else if (cell.z + padding > map.Size.z)
      {
        cell.z = map.Size.z - padding;
      }
      return cell;
    }

    public static void RegenerateEvents(this VehiclePawn vehicle)
    {
      vehicle.RegisterEvents();
    }

    public static void RegisterEvents(this VehiclePawn vehicle)
    {
      if (vehicle.EventRegistry != null && vehicle.EventRegistry.Initialized())
      {
        return; //Disallow re-registering events
      }

      vehicle.FillEvents_Def();

      vehicle.AddEvent(VehicleEventDefOf.CargoAdded, vehicle.statHandler.MarkAllDirty);
      vehicle.AddEvent(VehicleEventDefOf.CargoRemoved, vehicle.statHandler.MarkAllDirty);
      vehicle.AddEvent(VehicleEventDefOf.PawnEntered, vehicle.RecachePawnCount);
      vehicle.AddEvent(VehicleEventDefOf.PawnExited, vehicle.vehiclePather.RecalculatePermissions, vehicle.RecachePawnCount);
      vehicle.AddEvent(VehicleEventDefOf.PawnRemoved, vehicle.vehiclePather.RecalculatePermissions, vehicle.RecachePawnCount);
      vehicle.AddEvent(VehicleEventDefOf.PawnChangedSeats, vehicle.vehiclePather.RecalculatePermissions, vehicle.RecachePawnCount);
      vehicle.AddEvent(VehicleEventDefOf.PawnKilled, vehicle.vehiclePather.RecalculatePermissions, vehicle.RecachePawnCount);
      vehicle.AddEvent(VehicleEventDefOf.PawnCapacitiesDirty, vehicle.vehiclePather.RecalculatePermissions);
      vehicle.AddEvent(VehicleEventDefOf.IgnitionOff, vehicle.vehiclePather.RecalculatePermissions);
      vehicle.AddEvent(VehicleEventDefOf.DamageTaken, vehicle.vehiclePather.RecalculatePermissions, vehicle.statHandler.MarkAllDirty, vehicle.Notify_TookDamage);
      vehicle.AddEvent(VehicleEventDefOf.Repaired, vehicle.vehiclePather.RecalculatePermissions, vehicle.statHandler.MarkAllDirty);
      vehicle.AddEvent(VehicleEventDefOf.OutOfFuel, delegate ()
      {
        if (vehicle.Spawned)
        {
          vehicle.vehiclePather.PatherFailed();
          vehicle.ignition.Drafted = false;
        }
      });

      if (!vehicle.VehicleDef.events.NullOrEmpty())
      {
        foreach ((VehicleEventDef vehicleEventDef, List<ResolvedMethod<VehiclePawn>> methods) in vehicle.VehicleDef.events)
        {
          if (!methods.NullOrEmpty())
          {
            foreach (ResolvedMethod<VehiclePawn> method in methods)
            {
              vehicle.AddEvent(vehicleEventDef, () => method.Invoke(null, vehicle));
            }
          }
        }
      }
      if (!vehicle.VehicleDef.statEvents.NullOrEmpty())
      {
        foreach (StatCache.EventLister eventLister in vehicle.VehicleDef.statEvents)
        {
          foreach (VehicleEventDef eventDef in eventLister.eventDefs)
          {
            vehicle.AddEvent(eventDef, () => vehicle.statHandler.MarkStatDirty(eventLister.statDef));
          }
        }
      }

      //One Shots
      if (!vehicle.VehicleDef.soundOneShotsOnEvent.NullOrEmpty())
      {
        foreach (var soundEventEntry in vehicle.VehicleDef.soundOneShotsOnEvent)
        {
          vehicle.AddEvent(soundEventEntry.key, () => PlayOneShot(vehicle, soundEventEntry), soundEventEntry.removalKey);
        }
      }

      //Sustainers
      if (!vehicle.VehicleDef.soundSustainersOnEvent.NullOrEmpty())
      {
        foreach (var soundEventEntry in vehicle.VehicleDef.soundSustainersOnEvent)
        {
          vehicle.AddEvent(soundEventEntry.start, () => StartSustainer(vehicle, soundEventEntry), soundEventEntry.removalKey);
          vehicle.AddEvent(soundEventEntry.stop, () => StopSustainer(vehicle, soundEventEntry), soundEventEntry.removalKey);
        }
      }

      foreach (ThingComp comp in vehicle.AllComps)
      {
        if (comp is VehicleComp vehicleComp)
        {
          vehicleComp.EventRegistration();
        }
      }
    }

    public static void PlayOneShot<T>(VehiclePawn vehicle, VehicleSoundEventEntry<T> soundEventEntry)
    {
      if (vehicle.Spawned)
      {
        soundEventEntry.value.PlayOneShot(vehicle);
      }
    }

    public static void StartSustainer<T>(VehiclePawn vehicle, VehicleSustainerEventEntry<T> soundEventEntry)
    {
      if (vehicle.Spawned)
      {
        vehicle.sustainers.Spawn(vehicle, soundEventEntry.value);
      }
      else if (vehicle.SustainerTarget is ISustainerTarget sustainerTarget)
      {
        vehicle.sustainers.Spawn(sustainerTarget, soundEventEntry.value);
      }
    }

    public static void StopSustainer<T>(VehiclePawn vehicle, VehicleSustainerEventEntry<T> soundEventEntry)
    {
      vehicle.sustainers.EndAll(soundEventEntry.value);
    }

    public static bool DeconstructibleBy(this VehiclePawn vehicle, Faction faction)
    {
      return DebugSettings.godMode || (vehicle.Faction == faction || vehicle.ClaimableBy(faction));
    }

    public static void RefundMaterials(this VehiclePawn vehicle, Map map, DestroyMode mode, List<Thing> listOfLeavingsOut = null)
    {
      float multiplier = RefundMaterialCount(vehicle.VehicleDef, mode);
      vehicle.RefundMaterials(map, mode, multiplier: multiplier);
    }

    public static float RefundMaterialCount(VehicleDef vehicleDef, DestroyMode mode)
    {
      return mode switch
      {
        DestroyMode.Vanish => 0,
        DestroyMode.WillReplace => 0,
        DestroyMode.KillFinalize => 0.25f,
        DestroyMode.KillFinalizeLeavingsOnly => 0,
        DestroyMode.Deconstruct => vehicleDef.resourcesFractionWhenDeconstructed,
        DestroyMode.FailConstruction => 0.5f,
        DestroyMode.Cancel => 1,
        DestroyMode.Refund => 1,
        DestroyMode.QuestLogic => 0,
        _ => throw new ArgumentException("Unknown destroy mode " + mode),
      };
    }

    public static void RefundMaterials(this VehiclePawn vehicle, Map map, DestroyMode mode, float multiplier)
    {
      ThingOwner<Thing> thingOwner = new ThingOwner<Thing>();
      foreach (ThingDefCountClass thingDefCountClass in vehicle.VehicleDef.buildDef.CostListAdjusted(vehicle.Stuff))
      {
        if (thingDefCountClass.thingDef == ThingDefOf.ReinforcedBarrel && !Find.Storyteller.difficulty.classicMortars)
        {
          continue;
        }

        if (mode == DestroyMode.KillFinalize && vehicle.def.killedLeavings != null)
        {
          for (int k = 0; k < vehicle.def.killedLeavings.Count; k++)
          {
            Thing thing = ThingMaker.MakeThing(vehicle.def.killedLeavings[k].thingDef, null);
            thing.stackCount = vehicle.def.killedLeavings[k].count;
            thingOwner.TryAdd(thing, true);
          }
        }

        int refundCount = GenMath.RoundRandom(multiplier * thingDefCountClass.count);
        if (refundCount > 0 && mode == DestroyMode.KillFinalize && thingDefCountClass.thingDef.slagDef != null)
        {
          int count = thingDefCountClass.thingDef.slagDef.smeltProducts.First((ThingDefCountClass pro) => pro.thingDef == ThingDefOf.Steel).count;
          int proportionalCount = refundCount / count;
          proportionalCount = Mathf.Min(proportionalCount, vehicle.def.size.Area / 2);
          for (int n = 0; n < proportionalCount; n++)
          {
            thingOwner.TryAdd(ThingMaker.MakeThing(thingDefCountClass.thingDef.slagDef, null), true);
          }
          refundCount -= proportionalCount * count;
        }
        if (refundCount > 0)
        {
          Thing thing2 = ThingMaker.MakeThing(thingDefCountClass.thingDef);
          thing2.stackCount = refundCount;
          thingOwner.TryAdd(thing2, true);
        }
      }
      for (int i = vehicle.inventory.innerContainer.Count - 1; i >= 0; i--)
      {
        Thing thing = vehicle.inventory.innerContainer[i];
        thingOwner.TryAddOrTransfer(thing);
      }
      foreach (ThingComp thingComp in vehicle.AllComps)
      {
        if (thingComp is IRefundable refundable)
        {
          foreach ((ThingDef refundDef, float count) in refundable.Refunds)
          {
            if (refundDef != null)
            {
              Thing thing = ThingMaker.MakeThing(refundDef);
              thing.stackCount = GenMath.RoundRandom(count * multiplier);
              thingOwner.TryAdd(thing);
            }
          }
        }
      }
      TryDropAllOutsideVehicle(thingOwner, map, vehicle.OccupiedRect(), DestroyMode.Refund);
    }

    public static bool TryDropOutsideVehicle(this ThingOwner container, Thing thing, Map map, CellRect cellRect, DestroyMode mode = DestroyMode.Refund)
    {
      IntVec3 cell = cellRect.EdgeCells.RandomElement();
      if (mode == DestroyMode.KillFinalize && !map.areaManager.Home[cell])
      {
        thing.SetForbidden(true, warnOnFail: false);
      }
      return container.TryDrop(thing, ThingPlaceMode.Near, thing.stackCount, out _, null, nearPlaceValidator: CanPlaceAt);

      bool CanPlaceAt(IntVec3 cell)
      {
        if (!cell.InBounds(map))
        {
          return false;
        }
        if (map.thingGrid.ThingAt<VehiclePawn>(cell) != null)
        {
          return false;
        }
        return map.pathing.Normal.pathGrid.WalkableFast(cell);
      }
    }

    public static bool TryDropAllOutsideVehicle(this ThingOwner container, Map map, CellRect cellRect, DestroyMode mode = DestroyMode.Refund)
    {
      RotatingList<IntVec3> occupiedCells = cellRect.EdgeCells.InRandomOrder().ToRotatingList();
      while (container.Count > 0)
      {
        IntVec3 cell = occupiedCells.Next;
        if (mode == DestroyMode.KillFinalize && !map.areaManager.Home[cell])
        {
          container[0].SetForbidden(true, warnOnFail: false);
        }
        if (!container.TryDrop(container[0], cell, map, ThingPlaceMode.Near, out _, null, nearPlaceValidator: CanPlaceAt))
        {
          Log.Warning($"Failing to drop all from container {container.Owner}");
          return false;
        }
      }
      return true;

      bool CanPlaceAt(IntVec3 cell)
      {
        if (!cell.InBounds(map))
        {
          return false;
        }
        if (map.thingGrid.ThingAt<VehiclePawn>(cell) != null)
        {
          return false;
        }
        return map.pathing.Normal.pathGrid.WalkableFast(cell);
      }
    }

    /// <summary>
    /// Get AerialVehicle pawn is currently inside
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns><c>null</c> if not currently inside an AerialVehicle</returns>
    public static AerialVehicleInFlight GetAerialVehicle(this Pawn pawn)
    {
      if (VehicleWorldObjectsHolder.Instance?.AerialVehicles != null) //may get triggered prematurely from loading save
      {
        foreach (AerialVehicleInFlight aerialVehicle in VehicleWorldObjectsHolder.Instance.AerialVehicles)
        {
          if (aerialVehicle?.vehicle == pawn || aerialVehicle.vehicle.AllPawnsAboard.Contains(pawn))
          {
            return aerialVehicle;
          }
        }
      }
      return null;
    }

    /// <summary>
    /// Get all unique Vehicles in <paramref name="vehicles"/>
    /// </summary>
    /// <param name="vehicles"></param>
    public static List<VehicleDef> UniqueVehicleDefsInList(this IEnumerable<VehiclePawn> vehicles)
    {
      return vehicles.Select(v => v.VehicleDef).Distinct().ToList();
    }

    /// <summary>
    /// Get all unique Vehicles in <paramref name="vehicles"/>
    /// </summary>
    /// <param name="vehicles"></param>
    public static List<VehicleDef> UniqueVehicleDefsInList(this IEnumerable<Pawn> pawns)
    {
      return pawns.Where(pawn => pawn is VehiclePawn).Select(pawn => (pawn as VehiclePawn).VehicleDef).Distinct().ToList();
    }

    /// <summary>
    /// Check if <paramref name="thing"/> is a boat
    /// </summary>
    /// <param name="thing"></param>
    public static bool IsBoat(this Thing thing)
    {
      return thing is VehiclePawn vehicle && vehicle.VehicleDef.vehicleType == VehicleType.Sea;
    }

    /// <summary>
    /// Check if <paramref name="thingDef"/> is a boat
    /// </summary>
    /// <param name="thingDef"></param>
    /// <returns></returns>
    public static bool IsBoat(this ThingDef thingDef)
    {
      return thingDef is VehicleDef vehicleDef && vehicleDef.vehicleType == VehicleType.Sea;
    }

    /// <summary>
    /// Any Vehicle exists in collection of pawns
    /// </summary>
    /// <param name="pawns"></param>
    public static bool HasVehicle(this IEnumerable<Pawn> pawns)
    {
      return pawns.NotNullAndAny(x => x is VehiclePawn);
    }

    /// <summary>
    /// Any Boat exists in collection of pawns
    /// </summary>
    /// <param name="pawns"></param>
    /// <returns></returns>
    public static bool HasBoat(this IEnumerable<Pawn> pawns)
    {
      return pawns?.NotNullAndAny(x => IsBoat(x)) ?? false;
    }

    public static bool IsFormingVehicleCaravan(this Pawn pawn)
    {
      return pawn.GetLord()?.LordJob is LordJob_FormAndSendVehicles;
    }

    /// <summary>
    /// Caravan contains one or more Vehicles
    /// </summary>
    /// <param name="pawn"></param>
    public static bool HasVehicleInCaravan(this Pawn pawn)
    {
      return pawn.IsFormingVehicleCaravan() && pawn.GetLord().ownedPawns.NotNullAndAny(p => p is VehiclePawn);
    }

    /// <summary>
    /// Check if pawn is in VehicleCaravan
    /// </summary>
    /// <param name="pawn"></param>
    public static bool InVehicleCaravan(this Pawn pawn)
    {
      return pawn.GetVehicleCaravan() != null;
    }

    /// <summary>
    /// Get VehicleCaravan pawn is in
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns><c>null</c> if pawn is not currently inside a VehicleCaravan</returns>
    public static VehicleCaravan GetVehicleCaravan(this Pawn pawn)
    {
      if (pawn.ParentHolder is VehicleHandler handler)
      {
        return handler.vehicle.GetVehicleCaravan();
      }
      return pawn.ParentHolder as VehicleCaravan;
    }

    //REDO
    /// <summary>
    /// Vehicle speed should be reduced temporarily
    /// </summary>
    /// <param name="vehicle"></param>
    public static bool SlowSpeed(this VehiclePawn vehicle)
    {
      var lord = vehicle.GetLord();
      if (lord is null)
      {
        return false;
      }
      return false;// vehicleLordCategories.Contains(lord.LordJob.GetType());
    }

    /// <summary>
    /// Vehicle is able to travel on the coast of <paramref name="tile"/>
    /// </summary>
    /// <param name="vehicleDef"></param>
    /// <param name="tile"></param>
    public static bool CoastalTravel(this VehicleDef vehicleDef, int tile)
    {
      if (vehicleDef.properties.customBiomeCosts.TryGetValue(BiomeDefOf.Ocean, out float pathCost) && pathCost < WorldVehiclePathGrid.ImpassableMovementDifficulty)
      {
        WorldGrid worldGrid = Find.WorldGrid;
        List<int> neighbors = new List<int>();
        worldGrid.GetTileNeighbors(tile, neighbors);

        foreach (int neighborTile in neighbors)
        {
          if (worldGrid[neighborTile].biome == BiomeDefOf.Ocean) return true;
        }
      }
      return false;

    }

    /// <summary>
    /// Vehicle can path over cell and cell is in bounds.
    /// </summary>
    public static bool Drivable(this VehiclePawn vehicle, IntVec3 cell)
    {
      return cell.InBounds(vehicle.Map) && DrivableFast(vehicle, cell);
    }

    /// <summary>
    /// Vehicle can path over cell at ( <paramref name="x"/>, <paramref name="z"/> )
    /// </summary>
    public static bool DrivableFast(this VehiclePawn vehicle, int x, int z)
    {
      return DrivableFast(vehicle, vehicle.Map.cellIndices.CellToIndex(x, z));
    }

    /// <summary>
    /// <paramref name="vehicle"/> is able to move into <paramref name="cell"/>
    /// </summary>
    /// <param name="vehicle"></param>
    /// <param name="cell"></param>
    public static bool DrivableFast(this VehiclePawn vehicle, IntVec3 cell)
    {
      int index = vehicle.Map.cellIndices.CellToIndex(cell);
      return DrivableFast(vehicle, index);
    }

    /// <summary>
    /// Vehicle can path over cell at <paramref name="index"/>.
    /// </summary>
    public static bool DrivableFast(this VehiclePawn vehicle, int index)
    {
      VehiclePawn claimedBy = vehicle.Map.GetCachedMapComponent<VehiclePositionManager>()
        .ClaimedBy(vehicle.Map.cellIndices.IndexToCell(index));
      bool passable = (claimedBy is null || claimedBy == vehicle) &&
        vehicle.Map.GetCachedMapComponent<VehicleMapping>()[vehicle.VehicleDef].VehiclePathGrid.WalkableFast(index);
      return passable;
    }

    /// <summary>
    /// Determine if <paramref name="dest"/> is not large enough to fit <paramref name="vehicle"/>'s full hitbox
    /// </summary>
    /// <param name="vehicle"></param>
    /// <param name="dest"></param>
    public static bool LocationRestrictedBySize(this VehiclePawn vehicle, IntVec3 dest, Rot8 rot, Map map = null)
    {
      map ??= vehicle.Map;
      foreach (IntVec3 cell in vehicle.VehicleRect(dest, rot))
      {
        if (!cell.Walkable(vehicle.VehicleDef, map))
        {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// NxN rect of smallest dimension of vehicle
    /// </summary>
    /// <remarks>3x5 vehicle returns 3x3 rect, 2x4 returns 2x2, etc.</remarks>
    /// <param name="vehicle"></param>
    /// <param name="cell"></param>

    public static CellRect MinRect(this VehiclePawn vehicle, IntVec3 cell)
    {
      int minSize = Mathf.Min(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      return CellRect.CenteredOn(cell, Mathf.FloorToInt(minSize / 2f));
    }

    public static CellRect MaxRect(this VehiclePawn vehicle, IntVec3 cell)
    {
      int maxSize = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      return CellRect.CenteredOn(cell, Mathf.FloorToInt(maxSize / 2f));
    }

    public static IEnumerable<IntVec3> DiagonalRect(this VehiclePawn vehicle, IntVec3 cell, Rot8 rot)
    {
      if (!rot.IsDiagonal)
      {
        //return vehicle.OccupiedRect();
      }

      yield break;
    }

    /// <summary>
    /// Determines if vehicle is able to traverse this cell given its minimum bounds.
    /// </summary>
    /// <remarks>DOES take other vehicles into account</remarks>
    /// <param name="vehicle"></param>
    /// <param name="cell"></param>
    public static bool DrivableRectOnCell(this VehiclePawn vehicle, IntVec3 cell, bool maxPossibleSize = false)
    {
      if (maxPossibleSize)
      {
        if (!vehicle.VehicleRect(cell, Rot8.North).All(rectCell => vehicle.Drivable(rectCell)))
        {
          return false;
        }
        return vehicle.VehicleRect(cell, Rot8.East).All(rectCell => vehicle.Drivable(rectCell));
      }
      return MinRect(vehicle, cell).Cells.All(cell => vehicle.Drivable(cell));
    }

    /// <summary>
    /// Determines if vehicle fits on this cell with its minimum bounds
    /// </summary>
    /// <remarks>DOES NOT take other vehicles into account</remarks>
    /// <param name="vehicle"></param>
    /// <param name="cell"></param>
    public static bool FitsOnCell(this VehiclePawn vehicle, IntVec3 cell)
    {
      int minSize = Mathf.Min(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      CellRect cellRect = CellRect.CenteredOn(cell, Mathf.FloorToInt(minSize / 2f));
      return cellRect.Cells.All(cell => cell.Walkable(vehicle.VehicleDef, vehicle.Map));
    }

    /// <summary>
    /// Ensures the cellrect inhabited by the vehicle contains no Things that will block pathing and movement.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="c"></param>
    public static bool CellRectStandable(this VehiclePawn vehicle, Map map, IntVec3? c = null, Rot4? rot = null)
    {
      IntVec3 loc = c ?? vehicle.Position;
      Rot4 facing = rot ?? vehicle.Rotation;
      return vehicle.VehicleDef.CellRectStandable(map, loc, facing);
    }

    /// <summary>
    /// Ensures the cellrect inhabited by <paramref name="vehicleDef"/> contains no Things that will block pathing and movement at <paramref name="cell"/>.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="c"></param>
    public static bool CellRectStandable(this VehicleDef vehicleDef, Map map, IntVec3 position, Rot4 rot)
    {
      foreach (IntVec3 cell in vehicleDef.VehicleRect(position, rot))
      {
        if (!GenGridVehicles.Standable(cell, vehicleDef, map))
        {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Determine if <paramref name="cell"/> is able to fit the width of <paramref name="vehicleDef"/>
    /// </summary>
    /// <param name="vehicleDef"></param>
    /// <param name="cell"></param>
    /// <param name="dir"></param>
    public static bool WidthStandable(this VehicleDef vehicleDef, Map map, IntVec3 cell)
    {
      CellRect cellRect = CellRect.CenteredOn(cell, vehicleDef.SizePadding);
      foreach (IntVec3 cellCheck in cellRect)
      {
        if (!cellCheck.InBounds(map) || !GenGridVehicles.Walkable(cellCheck, vehicleDef, map))
        {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Seats assigned to vehicle in caravan formation
    /// </summary>
    /// <param name="vehicle"></param>
    public static int CountAssignedToVehicle(this VehiclePawn vehicle)
    {
      return CaravanHelper.assignedSeats.Where(a => a.Value.vehicle == vehicle).Select(s => s.Key).Count();
    }

    /// <summary>
    /// Gets the vehicle that <paramref name="pawn"/> is in.
    /// </summary>
    /// <param name="pawn">Pawn to check</param>
    /// <returns>VehiclePawn <paramref name="pawn"/> is in, or null if they aren't in a vehicle.</returns>
    public static VehiclePawn GetVehicle(this Pawn pawn)
    {
      return (pawn.ParentHolder as VehicleHandler)?.vehicle;
    }

    /// <summary>
    /// Returns true if <paramref name="pawn"/> is in a vehicle.
    /// </summary>
    /// <param name="pawn">Pawn to check</param>
    /// <returns>true if <paramref name="pawn"/> is in a vehicle, false otherwise</returns>
    public static bool IsInVehicle(this Pawn pawn)
    {
      return pawn.ParentHolder is VehicleHandler;
    }

    public static float GetStatValueAbstract(this VehicleDef vehicleDef, VehicleStatDef statDef)
    {
      return statDef.Worker.GetValueAbstract(vehicleDef);
    }
  }
}
