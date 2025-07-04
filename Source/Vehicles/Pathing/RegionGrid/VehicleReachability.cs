﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Performance;
using Verse;
using Verse.AI;

namespace Vehicles
{
  /// <summary>
  /// Reachability calculator for quick result path finding before running the algorithm
  /// </summary>
  public sealed class VehicleReachability : VehicleGridManager
  {
    private readonly Queue<VehicleRegion> openQueue = new Queue<VehicleRegion>();
    private readonly AStar chunkSearch;

    private readonly List<VehicleRegion> startingRegions = new List<VehicleRegion>();
    private readonly List<VehicleRegion> destRegions = new List<VehicleRegion>();

    private uint reachedIndex = 1;

    private readonly VehicleReachabilityCache cache = new VehicleReachabilityCache();

    private readonly VehiclePathGrid pathGrid;
    private readonly VehicleRegionGrid regionGrid;

    public VehicleReachability(VehiclePathingSystem mapping, VehicleDef createdFor,
      VehiclePathGrid pathGrid, VehicleRegionGrid regionGrid) : base(mapping, createdFor)
    {
      chunkSearch = new AStar(this, mapping, createdFor);
      this.pathGrid = pathGrid;
      this.regionGrid = regionGrid;
    }

    /// <summary>
    /// Currently calculating reachability between regions
    /// </summary>
    private bool CalculatingReachability { get; set; }

    /// <summary>
    /// Clear reachability cache
    /// </summary>
    public void ClearCache()
    {
      if (cache.Count > 0)
      {
        cache.Clear();
      }
    }

    /// <summary>
    /// Clear reachability cache for specific vehicle
    /// </summary>
    /// <param name="vehicle"></param>
    public void ClearCacheFor(VehiclePawn vehicle)
    {
      cache.ClearFor(vehicle);
    }

    /// <summary>
    /// Clear reachability cache for targets retaining hostile Pawn
    /// </summary>
    /// <param name="hostileTo"></param>
    public void ClearCacheForHostile(Thing hostileTo)
    {
      cache.ClearForHostile(hostileTo);
    }

    /// <summary>
    /// Queue region for reachability check
    /// </summary>
    /// <param name="region"></param>
    private void QueueNewOpenRegion(VehicleRegion region)
    {
      if (region == null)
      {
        Log.Warning("Tried to queue null region (Vehicles).");
        return;
      }

      if (region.reachedIndex == reachedIndex)
      {
        Log.ErrorOnce(
          $"VehicleRegion is already reached; you can't open it. VehicleRegion={region}",
          region.GetHashCode());
        return;
      }

      openQueue.Enqueue(region);
      region.reachedIndex =
        reachedIndex; //Ensures region links don't back traverse to regions already visited
    }

    private void QueueChunk(VehicleRegion region)
    {
      if (region is null)
      {
        Log.ErrorOnce($"[{VehicleHarmony.LogLabel}] Tried to queue null region.",
          "NullVehicleRegion".GetHashCode());
        return;
      }

      if (region.reachedIndex == reachedIndex)
      {
        Log.ErrorOnce(
          $"[{VehicleHarmony.LogLabel}] VehicleRegion has already been reached, attempting to retrace which may result in infinite loops. VehicleRegion={region}",
          region.GetHashCode());
        return;
      }

      openQueue.Enqueue(region);
      region.reachedIndex = reachedIndex;
    }

    /// <summary>
    /// <seealso cref="CanReachVehicle(IntVec3, LocalTargetInfo, PathEndMode, TraverseParms)"/>
    /// </summary>
    /// <param name="start"></param>
    /// <param name="dest"></param>
    /// <param name="peMode"></param>
    /// <param name="traverseMode"></param>
    /// <param name="maxDanger"></param>
    public bool CanReachVehicleNonLocal(IntVec3 start, TargetInfo dest, PathEndMode peMode,
      TraverseMode traverseMode, Danger maxDanger)
    {
      return (dest.Map is null || dest.Map == mapping.map) && CanReachVehicle(start,
        (LocalTargetInfo)dest, peMode, traverseMode, maxDanger);
    }

    /// <summary>
    /// <seealso cref="CanReachVehicle(IntVec3, LocalTargetInfo, PathEndMode, TraverseParms)"/>
    /// </summary>
    /// <param name="start"></param>
    /// <param name="dest"></param>
    /// <param name="peMode"></param>
    /// <param name="traverseParms"></param>
    public bool CanReachVehicleNonLocal(IntVec3 start, TargetInfo dest, PathEndMode peMode,
      TraverseParms traverseParms)
    {
      return (dest.Map is null || dest.Map == mapping.map) &&
        CanReachVehicle(start, (LocalTargetInfo)dest, peMode, traverseParms);
    }

    /// <summary>
    /// <seealso cref="CanReachVehicle(IntVec3, LocalTargetInfo, PathEndMode, TraverseParms)"/>
    /// </summary>
    /// <param name="start"></param>
    /// <param name="dest"></param>
    /// <param name="peMode"></param>
    /// <param name="traverseMode"></param>
    /// <param name="maxDanger"></param>
    public bool CanReachVehicle(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode,
      TraverseMode traverseMode, Danger maxDanger)
    {
      return CanReachVehicle(start, dest, peMode,
        TraverseParms.For(traverseMode, maxDanger, false));
    }

    /// <summary>
    /// Traverse by cell or by region to determine reachability for Vehicle
    /// </summary>
    /// <param name="start"></param>
    /// <param name="dest"></param>
    /// <param name="peMode"></param>
    /// <param name="traverseParms"></param>
    public bool CanReachVehicle(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode,
      TraverseParms traverseParms)
    {
      if (!ValidateCanStart(start, dest, traverseParms, out VehicleDef vehicleDef))
      {
        return false;
      }

      if (!pathGrid.WalkableFast(start))
      {
        Debug.Message($"Unable to start pathing from {start} to {dest}. Not walkable at {start}");
        return false;
      }

      bool freeTraversal = traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater &&
        traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
      if ((peMode == PathEndMode.OnCell || peMode == PathEndMode.Touch ||
          peMode == PathEndMode.ClosestTouch) && freeTraversal)
      {
        VehicleRoom room = VehicleRegionAndRoomQuery.RoomAtFast(start, mapping.map, createdFor);
        if (room != null &&
          room == VehicleRegionAndRoomQuery.RoomAtFast(dest.Cell, mapping.map, createdFor))
        {
          return true;
        }
      }

      if (traverseParms.mode == TraverseMode.PassAllDestroyableThings)
      {
        // NOTE - I don't know if I'll ever enable door-capabilities for vehicles but right now the
        // region type will never be Portal so this is essentially just TraverseMode.ByPawn. Keeping
        // TraverseMode.PassDoors to retain what would be a vanilla entry point for door reachability.
        if (CanReachVehicle(start, dest, peMode,
          traverseParms with { mode = TraverseMode.PassDoors }))
        {
          return true;
        }
      }

      dest = (LocalTargetInfo)GenPathVehicles.ResolvePathMode(vehicleDef, mapping.map,
        dest.ToTargetInfo(mapping.map), ref peMode);
      CalculatingReachability = true;
      try
      {
        reachedIndex += 1;
        destRegions.Clear();
        if (peMode == PathEndMode.OnCell)
        {
          VehicleRegion region = VehicleRegionAndRoomQuery.RegionAt(dest.Cell, mapping, createdFor);
          if (region != null && region.Allows(traverseParms))
          {
            destRegions.Add(region);
          }
        }
        else if (peMode == PathEndMode.Touch)
        {
          TouchPathEndModeUtilityVehicles.AddAllowedAdjacentRegions(dest, traverseParms,
            mapping.map, createdFor, destRegions);
        }

        if (destRegions.Count == 0 && traverseParms.mode != TraverseMode.PassAllDestroyableThings &&
          traverseParms.mode !=
          TraverseMode.PassAllDestroyableThingsNotWater)
        {
          return false;
        }

        destRegions.RemoveDuplicates();
        openQueue.Clear();
        DetermineStartRegions(start);

        if (openQueue.Count == 0 &&
          traverseParms.mode != TraverseMode.PassAllDestroyableThings &&
          traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater)
        {
          return false;
        }

        if (startingRegions.Any() && destRegions.Any() && CanUseCache(traverseParms.mode))
        {
          BoolUnknown cachedResult = GetCachedResult(traverseParms);
          switch (cachedResult)
          {
            case BoolUnknown.True:
              return true;
            case BoolUnknown.False:
              return false;
            case BoolUnknown.Unknown:
            break;
            default:
              throw new NotImplementedException(nameof(BoolUnknown));
          }
        }

        if (traverseParms.mode == TraverseMode.PassAllDestroyableThings ||
          traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater ||
          traverseParms.mode == TraverseMode.NoPassClosedDoorsOrWater)
        {
          return CheckCellBasedReachability(start, dest, peMode, traverseParms);
        }

        return CheckRegionBasedReachability(traverseParms);
      }
      finally
      {
        CalculatingReachability = false;
      }
    }

    /// <summary>
    /// Determine starting region either on starting cell or cardinal / diagonal to it.
    /// </summary>
    /// <param name="start"></param>
    private void DetermineStartRegions(IntVec3 start)
    {
      startingRegions.Clear();
      if (pathGrid.WalkableFast(start))
      {
        VehicleRegion validRegionAt = regionGrid.GetValidRegionAt(start);
        QueueNewOpenRegion(validRegionAt);
        startingRegions.Add(validRegionAt);
        return;
      }

      for (int i = 0; i < 8; i++)
      {
        IntVec3 cell = start + GenAdj.AdjacentCells[i];
        if (cell.InBounds(mapping.map) && pathGrid.WalkableFast(cell))
        {
          VehicleRegion validRegionAt = regionGrid.GetValidRegionAt(cell);
          if (validRegionAt != null && validRegionAt.reachedIndex != reachedIndex)
          {
            QueueNewOpenRegion(validRegionAt);
            startingRegions.Add(validRegionAt);
          }
        }
      }
    }

    /// <summary>
    /// Retrieve cached value for reachability result
    /// </summary>
    /// <param name="traverseParms"></param>
    private BoolUnknown GetCachedResult(TraverseParms traverseParms)
    {
      bool anyUnknown = false;
      for (int i = 0; i < startingRegions.Count; i++)
      {
        for (int j = 0; j < destRegions.Count; j++)
        {
          if (destRegions[j] == startingRegions[i])
          {
            return BoolUnknown.True;
          }

          BoolUnknown boolUnknown = cache.CachedResultFor(startingRegions[i].Room,
            destRegions[j].Room, traverseParms);
          if (boolUnknown == BoolUnknown.True)
          {
            return BoolUnknown.True;
          }

          if (boolUnknown == BoolUnknown.Unknown)
          {
            anyUnknown = true;
          }
        }
      }

      return anyUnknown ? BoolUnknown.Unknown : BoolUnknown.False;
    }

    /// <summary>
    /// Determine reachability by region traversal
    /// </summary>
    private bool CheckRegionBasedReachability(TraverseParms traverseParms)
    {
      while (openQueue.Count > 0)
      {
        VehicleRegion region = openQueue.Dequeue();
        using ListSnapshot<VehicleRegionLink> links = region.Links;
        foreach (VehicleRegionLink regionLink in links)
        {
          if (RegionReachable(regionLink.regionA) || RegionReachable(regionLink.regionB))
          {
            return true;
          }
        }
      }

      foreach (VehicleRegion startRegion in startingRegions)
      {
        foreach (VehicleRegion destRegion in destRegions)
        {
          cache.AddCachedResult(startRegion.Room, destRegion.Room, traverseParms, false);
        }
      }

      return false;

      bool RegionReachable(VehicleRegion linkedRegion)
      {
        if (linkedRegion != null && linkedRegion.reachedIndex != reachedIndex &&
          linkedRegion.type.Passable() && linkedRegion.Allows(traverseParms))
        {
          if (destRegions.Contains(linkedRegion))
          {
            foreach (VehicleRegion startRegion in startingRegions)
            {
              cache.AddCachedResult(startRegion.Room, linkedRegion.Room, traverseParms, true);
            }

            return true;
          }

          QueueNewOpenRegion(linkedRegion);
        }

        return false;
      }
    }

    public ChunkSet FindChunks(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode,
      TraverseParms traverseParms, bool debugDrawSearch = false)
    {
      if (ValidateCanStart(start, dest, traverseParms, out VehicleDef _))
      {
        openQueue.Clear();
        reachedIndex++;

        return chunkSearch.Run(start, dest, traverseParms, debugDrawSearch: debugDrawSearch);
      }

      Log.Error($"Can't validate for chunk search");
      return null;
    }

    private static void MarkRegionForDrawing(VehicleRegion region, Map map, bool drawRegions = true,
      bool drawWeights = false)
    {
      if (drawRegions)
      {
        foreach (IntVec3 cell in region.Cells)
        {
          map.DrawCell_ThreadSafe(cell, colorPct: 0.65f);
        }
      }

#if HIERARCHAL_PATHFINDING
      if (drawWeights)
      {
        foreach (VehicleRegionLink regionLink in region.Links)
        {
          foreach (VehicleRegionLink toRegionLink in region.Links)
          {
            if (regionLink == toRegionLink) continue;
            float weight = region.WeightBetween(regionLink, toRegionLink);
            regionLink.DrawWeight(map, toRegionLink, weight);
          }
        }
      }
#endif
    }

    [Conditional("HIERARCHAL_PATHFINDING")]
    private static void MarkLinksForDrawing(VehicleRegion region, Map map, VehicleRegionLink from,
      VehicleRegionLink to)
    {
      float weight = region.WeightBetween(from, to);
      from.DrawWeight(map, to, weight);
    }

    private bool ValidateCanStart(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms,
      out VehicleDef forVehicleDef)
    {
      if (CalculatingReachability)
      {
        Log.ErrorOnce(
          "Called CanReachVehicle() while working. This should never happen. Suppressing further errors.",
          "CanReachVehicleWorkingError".GetHashCode());
        forVehicleDef = null;
        return false;
      }

      VehiclePawn vehicle = traverseParms.pawn as VehiclePawn;
      forVehicleDef = vehicle?.VehicleDef ?? createdFor;
      if (vehicle != null)
      {
        if (!vehicle.Spawned)
        {
          Log.Error($"Attempting reachability check for unspawned vehicle {vehicle}.");
          return false;
        }

        if (vehicle.Map != mapping.map)
        {
          Log.Error(
            $"Called CanReach with a vehicle not spawned on this map. This means that we can't check its reachability here. Vehicle's current map should have been used instead. vehicle={vehicle} vehicle.Map={vehicle.Map} map={mapping.map}");
          return false;
        }
      }

      if (!dest.IsValid)
      {
        Debug.Warning($"Destination Invalid.");
        return false;
      }

      if (dest.HasThing && dest.Thing.Map != mapping.map)
      {
        Log.Error(
          $"Called CanReach for regions of a different map than destination.  Destination={dest} Map={mapping.map} Destination.Map={dest.Thing.Map}");
        return false;
      }

      if (!start.InBounds(mapping.map) || !dest.Cell.InBounds(mapping.map))
      {
        Debug.Warning($"Start or Destination out of bounds for reachability check.");
        return false;
      }

      return true;
    }

    /// <summary>
    /// Determine reachability by cell traversal
    /// </summary>
    /// <remarks>
    /// Only use outside normal search conditions, performance varies on distance
    /// </remarks>
    /// <param name="start"></param>
    /// <param name="dest"></param>
    /// <param name="peMode"></param>
    /// <param name="traverseParms"></param>
    private bool CheckCellBasedReachability(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode,
      TraverseParms traverseParms)
    {
      IntVec3 foundCell = IntVec3.Invalid;
      mapping.map.floodFiller.FloodFill(start,
        (IntVec3 cell) => PassCheck(cell, mapping.map, traverseParms), delegate(IntVec3 cell)
        {
          VehiclePawn vehicle = traverseParms.pawn as VehiclePawn;
          if (VehicleReachabilityImmediate.CanReachImmediateVehicle(cell, dest, mapping.map,
            vehicle.VehicleDef, peMode))
          {
            foundCell = cell;
            return true;
          }

          return false;
        }, int.MaxValue, false, null);

      if (foundCell.IsValid)
      {
        if (CanUseCache(traverseParms.mode))
        {
          VehicleRegion validRegionAt = regionGrid.GetValidRegionAt(foundCell);
          if (!(validRegionAt is null))
          {
            foreach (VehicleRegion startRegion in startingRegions)
            {
              cache.AddCachedResult(startRegion.Room, validRegionAt.Room, traverseParms, true);
            }
          }
        }

        return true;
      }

      if (CanUseCache(traverseParms.mode))
      {
        foreach (VehicleRegion startRegion in startingRegions)
        {
          foreach (VehicleRegion destRegion in destRegions)
          {
            cache.AddCachedResult(startRegion.Room, destRegion.Room, traverseParms, false);
          }
        }
      }

      return false;
    }

    private bool PassCheck(IntVec3 cell, Map map, TraverseParms traverseParms)
    {
      int num = map.cellIndices.CellToIndex(cell);
      if ((traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater ||
          traverseParms.mode == TraverseMode.NoPassClosedDoorsOrWater) &&
        cell.GetTerrain(map).IsWater)
      {
        return false;
      }

      if (traverseParms.mode == TraverseMode.PassAllDestroyableThings ||
        traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater)
      {
        if (!pathGrid.WalkableFast(num))
        {
          Building edifice = cell.GetEdifice(map);
          if (edifice is null || !VehiclePathFinder.IsDestroyable(edifice))
          {
            return false;
          }
        }
      }
      else if (traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater)
      {
        Log.ErrorOnce("Do not use this method for non-cell based modes!", 938476762);
        if (!pathGrid.WalkableFast(num))
        {
          return false;
        }
      }

      VehicleRegion region = regionGrid.DirectGrid[num];
      return region is null || region.Allows(traverseParms);
    }

    /// <summary>
    /// Can reach colony at cell <paramref name="c"/>
    /// </summary>
    /// <param name="cell"></param>
    /// <param name="vehicleDef"></param>
    public bool CanReachBase(IntVec3 cell, VehicleDef vehicleDef)
    {
      if (Current.ProgramState != ProgramState.Playing)
      {
        return CanReachVehicle(cell, MapGenerator.PlayerStartSpot, PathEndMode.OnCell,
          TraverseParms.For(TraverseMode.PassDoors,
            Danger.Deadly, false));
      }

      if (!GenGridVehicles.Walkable(cell, vehicleDef, mapping))
      {
        return false;
      }

      Faction faction = mapping.map.ParentFaction ?? Faction.OfPlayer;
      List<Pawn> list = mapping.map.mapPawns.SpawnedPawnsInFaction(faction);
      foreach (Pawn p in list)
      {
        if (p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
        {
          return true;
        }
      }

      TraverseParms traverseParms = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
      if (faction == Faction.OfPlayer)
      {
        List<Building> allBuildingsColonist = mapping.map.listerBuildings.allBuildingsColonist;
        foreach (Building b in allBuildingsColonist)
        {
          if (CanReachVehicle(cell, b, PathEndMode.Touch, traverseParms))
          {
            return true;
          }
        }
      }
      else
      {
        List<Thing> list2 =
          mapping.map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        foreach (Thing thing in list2)
        {
          if (thing.Faction == faction &&
            CanReachVehicle(cell, thing, PathEndMode.Touch, traverseParms))
          {
            return true;
          }
        }
      }

      return CanReachBiggestMapEdgeRoom(cell);
    }

    /// <summary>
    /// Reachability to largest <see cref="VehicleRoom"/> touching map edge
    /// </summary>
    /// <param name="c"></param>
    public bool CanReachBiggestMapEdgeRoom(IntVec3 c)
    {
      VehicleRoom usableRoom = null;
      //ConcurrentDictionary.Keys snapshots, but ConcurrentDictionary.GetEnumerator does not. Must utilize Key or Value collections for thread safe enumeration
      foreach (VehicleRoom room in regionGrid.allRooms.Keys)
      {
        if (room.TouchesMapEdge)
        {
          if (usableRoom is null || room.RegionCount > usableRoom.RegionCount)
          {
            usableRoom = room;
          }
        }
      }

      return usableRoom != null && CanReachVehicle(c,
        usableRoom.Regions.FirstOrDefault().Key.AnyCell, PathEndMode.OnCell, TraverseParms.For(
          TraverseMode.PassDoors,
          Danger.Deadly, false));
    }

    /// <summary>
    /// Can reach map edge from <paramref name="cell"/>
    /// </summary>
    /// <param name="cell"></param>
    /// <param name="traverseParms"></param>
    public bool CanReachMapEdge(IntVec3 cell, TraverseParms traverseParms)
    {
      if (traverseParms.pawn is VehiclePawn vehicle)
      {
        if (!vehicle.Spawned)
        {
          return false;
        }

        if (vehicle.Map != mapping.map)
        {
          Log.Error(
            $"Called CanReachMapEdge with vehicle not spawned on this map. Pawn's current map should have been used instead of this one. vehicle={vehicle} vehicle.Map={vehicle.Map} map={mapping.map}");
          return false;
        }
      }

      VehicleRegion region =
        VehicleRegionAndRoomQuery.RegionAt(cell, mapping, createdFor, RegionType.Set_Passable);
      if (region is null)
      {
        return false;
      }

      if (region.Room.TouchesMapEdge)
      {
        return true;
      }

      bool entryCondition(VehicleRegion from, VehicleRegion r) => r.Allows(traverseParms);
      bool foundReg = false;

      bool regionProcessor(VehicleRegion r)
      {
        if (r.Room.TouchesMapEdge)
        {
          foundReg = true;
          return true;
        }

        return false;
      }

      VehicleRegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 9999,
        RegionType.Set_Passable);
      return foundReg;
    }

    /// <summary>
    /// Can reach <paramref name="cell"/> with Unfogged constraint
    /// </summary>
    /// <param name="cell"></param>
    /// <param name="traverseParms"></param>
    public bool CanReachUnfogged(IntVec3 cell, TraverseParms traverseParms)
    {
      if (traverseParms.pawn != null)
      {
        if (!traverseParms.pawn.Spawned)
        {
          return false;
        }

        if (traverseParms.pawn.Map != mapping.map)
        {
          Log.Error(string.Concat(new object[]
          {
            "Called CanReachUnfogged() with a pawn spawned not on this map. This means that we can't check his reachability here. Pawn's current map should have been used instead of this one. pawn=",
            traverseParms.pawn,
            " pawn.Map=",
            traverseParms.pawn.Map,
            " map=",
            mapping.map
          }));
          return false;
        }
      }

      if (!cell.InBounds(mapping.map))
      {
        return false;
      }

      if (!cell.Fogged(mapping.map))
      {
        return true;
      }

      VehicleRegion region =
        VehicleRegionAndRoomQuery.RegionAt(cell, mapping, createdFor);
      if (region == null)
      {
        return false;
      }

      bool entryCondition(VehicleRegion from, VehicleRegion r) => r.Allows(traverseParms);
      bool foundReg = false;

      bool regionProcessor(VehicleRegion r)
      {
        if (!r.AnyCell.Fogged(mapping.map))
        {
          foundReg = true;
          return true;
        }

        return false;
      }

      VehicleRegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 9999,
        RegionType.Set_Passable);
      return foundReg;
    }

    /// <summary>
    /// Can retrieve cached value for reachability
    /// </summary>
    /// <param name="mode"></param>
    private bool CanUseCache(TraverseMode mode)
    {
      return mode != TraverseMode.PassAllDestroyableThingsNotWater &&
        mode != TraverseMode.NoPassClosedDoorsOrWater;
    }

    /// <summary>
    /// Not using generic pathfinding classes because traversal starts / ends from regions but traverses links with weights
    /// </summary>
    /// <returns>ChunkSet containing regions in order towards destination</returns>
    private class AStar
    {
      private const int Status_Invalid = -1;
      private const int Status_Open = 0;
      private const int Status_Closed = 1;
      private const int Status_Starter = 2;

      private readonly PriorityQueue<Node, int> openQueue = new PriorityQueue<Node, int>();
      private readonly Dictionary<IntVec3, Node> nodes = new Dictionary<IntVec3, Node>();

      private readonly VehicleReachability vehicleReachability;
      private readonly VehiclePathingSystem mapping;
      private readonly VehicleDef vehicleDef;

      public AStar(VehicleReachability vehicleReachability, VehiclePathingSystem mapping,
        VehicleDef vehicleDef)
      {
        this.vehicleReachability = vehicleReachability;
        this.mapping = mapping;
        this.vehicleDef = vehicleDef;
      }

      public bool LogRetraceAttempts { get; set; } = true;

      public Map Map => mapping.map;

      public ChunkSet Run(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms,
        bool debugDrawSearch = false)
      {
        if (!InitRegions(start, dest, traverseParms, out VehicleRegion startingRegion,
          out VehicleRegion destinationRegion))
        {
          return null;
        }

        if (debugDrawSearch)
        {
          UnityThread.ExecuteOnMainThread(() => MarkRegionForDrawing(startingRegion, Map));
        }

        VehicleRegionLink closestLinkHeuristically = null;
        VehicleRegionLink closest2ndLinkHeuristically = null;
        float firstClosest = float.MaxValue;
        float secondClosest = float.MaxValue;

        // Fetch snapshot of links for heuristics calculation
        using (ListSnapshot<VehicleRegionLink> links = startingRegion.Links)
        {
          foreach (VehicleRegionLink startingLink in links)
          {
            float heuristic = VehicleRegion.EuclideanDistance(dest.Cell, startingLink);
            if (heuristic < firstClosest)
            {
              firstClosest = heuristic;
              secondClosest = firstClosest;
              closestLinkHeuristically = startingLink;
              closest2ndLinkHeuristically = closestLinkHeuristically;
            }
          }
        }

        //Start from link with smallest heuristic
        VehicleRegion otherClosestHeuristic =
          closestLinkHeuristically.GetOtherRegion(startingRegion);
        Node startingNodeHeuristic = CreateNode(dest.Cell, null, closestLinkHeuristically,
          startingRegion, otherClosestHeuristic,
          0); //Null previous, shouldn't backtrack past starting nodes
        startingNodeHeuristic.status = Status_Starter;
        nodes[closestLinkHeuristically.anchor] = startingNodeHeuristic;
        openQueue.Enqueue(startingNodeHeuristic,
          startingNodeHeuristic.cost + startingNodeHeuristic.heuristicCost);

        //Queue 2nd closest link with slightly higher cost for sweeping 2 directions at start
        if (closest2ndLinkHeuristically != null)
        {
          otherClosestHeuristic = closest2ndLinkHeuristically.GetOtherRegion(startingRegion);
          Node startingNode2Heuristic = CreateNode(dest.Cell, null, closest2ndLinkHeuristically,
            startingRegion, otherClosestHeuristic,
            1); //Null previous, shouldn't backtrack past starting nodes
          startingNode2Heuristic.status = Status_Starter;
          nodes[closest2ndLinkHeuristically.anchor] = startingNode2Heuristic;
          openQueue.Enqueue(startingNode2Heuristic,
            startingNode2Heuristic.cost + startingNode2Heuristic.heuristicCost);
        }

        try
        {
          while (openQueue.Count > 0)
          {
            if (!openQueue.TryDequeue(out Node current, out int priority))
            {
              Log.Error($"Failed to dequeue node. Count={openQueue.Count}");
              goto PathNotFound;
            }

            if (!current.IsOpen && current.status != Status_Starter)
            {
              continue;
            }

            //Sweep in all directions to neighboring links
            foreach (VehicleRegionLink neighbor in Neighbors(current))
            {
              Node node = GetNode(dest.Cell, current, neighbor);
              if (!node.Passable || !node.Allows(traverseParms))
              {
                node.status = Status_Invalid;
              }

              if (node.IsOpen)
              {
                //Cache node in back-traversal dict
                nodes[neighbor.anchor] = node;
                node.previous = current;

                NodeRegions(current, neighbor, out VehicleRegion inFacingRegion,
                  out VehicleRegion otherRegion);

                //Check if destination reached
                if (otherRegion == destinationRegion)
                {
#if HIERARCHAL_PATHFINDING
                  if (debugDrawSearch)
                    CoroutineManager.QueueOrInvoke(
                      () => MarkLinksForDrawing(inFacingRegion, Map, current.regionLink, neighbor),
                      secondsBetweenDrawing);
#endif
                  return SolvePath(startingRegion, destinationRegion, current);
                }

                //Queue for traversal of neighbors
                openQueue.Enqueue(node, node.cost + node.heuristicCost);

                if (debugDrawSearch)
                {
#if HIERARCHAL_PATHFINDING
                  CoroutineManager.QueueOrInvoke(
                    () => MarkLinksForDrawing(inFacingRegion, Map, current.regionLink, neighbor),
                    secondsBetweenDrawing);
#endif
                }
              }
            }

            current.status = Status_Closed;
          }

          PathNotFound: ;
          return null; //Fall back to cell-based pathing
        }
        finally
        {
          CleanUp();
        }
      }

      private Node GetNode(IntVec3 dest, Node current, VehicleRegionLink neighbor)
      {
        if (nodes.TryGetValue(neighbor.anchor, out Node node))
        {
          return node;
        }

        NodeRegions(current, neighbor, out VehicleRegion inFacingRegion,
          out VehicleRegion otherRegion);

        float cost = inFacingRegion.WeightBetween(current.regionLink, neighbor);
        return CreateNode(dest, current, neighbor, inFacingRegion, otherRegion, (int)cost);
      }

      private Node CreateNode(IntVec3 dest, Node current, VehicleRegionLink regionLink,
        VehicleRegion regionA, VehicleRegion regionB, int cost)
      {
        Node node = new(regionLink, regionA, regionB);
        node.cost = cost;
        node.heuristicCost = VehicleRegion.EuclideanDistance(dest, regionLink);
        return node;
      }

      private IEnumerable<VehicleRegionLink> Neighbors(Node node)
      {
        using (ListSnapshot<VehicleRegionLink> linksA = node.regionA.Links)
        {
          foreach (VehicleRegionLink regionLink in linksA)
          {
            if (regionLink != node.regionLink &&
              (!nodes.TryGetValue(regionLink.anchor, out Node neighborNode) || neighborNode.IsOpen))
            {
              yield return regionLink;
            }
          }
        }

        using (ListSnapshot<VehicleRegionLink> linksB = node.regionB.Links)
        {
          foreach (VehicleRegionLink regionLink in linksB)
          {
            if (regionLink != node.regionLink &&
              (!nodes.TryGetValue(regionLink.anchor, out Node neighborNode) || neighborNode.IsOpen))
            {
              yield return regionLink;
            }
          }
        }
      }

      private bool InitRegions(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms,
        out VehicleRegion startingRegion, out VehicleRegion destinationRegion)
      {
        startingRegion = vehicleReachability.regionGrid.GetValidRegionAt(start);
        destinationRegion = null;
        if (startingRegion == null)
        {
          Log.Error($"Unable to fetch valid starting region at {start}.");
          return false;
        }

        destinationRegion = VehicleRegionAndRoomQuery.RegionAt(dest.Cell, mapping,
          vehicleReachability.createdFor, RegionType.Set_Passable);
        if (startingRegion == null || !destinationRegion.Allows(traverseParms))
        {
          Log.Error(
            $"Unable to fetch valid starting region that allows traverseParms={traverseParms} at {start}.");
          return false;
        }

        if (startingRegion == destinationRegion)
        {
          return false; //no need for HPA* in same region
        }

        return true;
      }

      private void NodeRegions(Node current, VehicleRegionLink neighbor,
        out VehicleRegion inFacingRegion, out VehicleRegion otherRegion)
      {
        inFacingRegion = current.regionLink.GetInFacingRegion(neighbor);
        otherRegion = neighbor.GetOtherRegion(inFacingRegion);
      }

      private ChunkSet SolvePath(VehicleRegion start, VehicleRegion destination, Node finalNode)
      {
        HashSet<VehicleRegion> result = new HashSet<VehicleRegion>();

        //Pre-add destination region and in-facing region from starting node, start at link
        Node node = finalNode;
        result.Add(destination);
        //result.AddRange(destination.Neighbors);

        //Limit to total nodes traversed to avoid infinite loop
        for (int i = 0; i < nodes.Count; i++)
        {
          //Ensure node doesn't reach incorrect destination with no backtrack available
          if (node == null || node == node.previous)
          {
            SmashLog.Error(
              $"Unable to find hierarchal path from {start} to {destination}.  Couldn't backtrack {node} to starting node.");
            return null;
          }

          //Will add duplicates but gets filtered out when building cells in ChunkList
          result.Add(node.regionA);
          result.Add(node.regionB);

          //RegionA (in-facing) may be the starting region is chunk traverses backwards from closest link heuristically
          if (node.regionA == start || node.regionB == start)
          {
            //result.Add(start);
            //result.Reverse(); //No need for reversal

            return new ChunkSet(result);
          }

          node = node.previous;
        }

        SmashLog.Error($"Ran out of nodes to backtrace for solution.");
        return null;
      }

      private void CleanUp()
      {
        openQueue.Clear();
        nodes.Clear();
      }

      private class Node
      {
        public VehicleRegionLink regionLink;
        public Node previous;

        public VehicleRegion regionA; //in-facing region
        public VehicleRegion regionB; //out-facing region

        public int cost;
        public int heuristicCost;
        public int status;

        //Initializes as newly opened node
        public Node(VehicleRegionLink regionLink, VehicleRegion regionA, VehicleRegion regionB)
        {
          this.regionLink = regionLink;

          this.regionA = regionA;
          this.regionB = regionB;

          cost = 0;
          heuristicCost = 0;
          status = Status_Open;
        }

        public IntVec3 Pos => regionLink.anchor;

        public bool IsOpen => status == Status_Open;

        public bool Passable => regionA != null && regionA.type.Passable() && regionB != null &&
          regionB.type.Passable();

        public bool Allows(TraverseParms traverseParms)
        {
          return regionA.Allows(traverseParms) && regionB.Allows(traverseParms);
        }

        public override string ToString()
        {
          return Pos.ToString();
        }

        public override int GetHashCode()
        {
          return Pos.GetHashCode();
        }
      }
    }
  }
}