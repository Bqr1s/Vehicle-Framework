﻿using System.Collections.Generic;
using System.Threading;
using SmashTools;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using RegionResult = Vehicles.VehicleRegionMaker.RegionResult;

namespace Vehicles
{
  /// <summary>
  /// Region and room update handler
  /// </summary>
  public class VehicleRegionAndRoomUpdater : VehicleGridManager
  {
    private readonly List<VehicleRegion> newRegions = [];

    private readonly List<VehicleRoom> newRooms = [];
    private readonly HashSet<VehicleRoom> reusedOldRooms = [];

    private readonly List<VehicleRegion> currentRegionGroup = [];

    private VehicleRegionGrid regionGrid;

    public VehicleRegionAndRoomUpdater(VehiclePathingSystem mapping, VehicleDef createdFor)
      : base(mapping, createdFor)
    {
    }

    /// <summary>
    /// Updater has been initialized
    /// </summary>
    public bool Initialized { get; private set; }

    /// <summary>
    /// Currently updating regions
    /// </summary>
    public bool UpdatingRegion { get; private set; }

    /// <summary>
    /// Cross check objects not in thread safe code is not being modified outside
    /// of the thread responsible for updating the region set. If within the same
    /// thread, many locks can be avoided since many of the region specific fields
    /// are not accessed outside of this context.
    /// </summary>
    internal int UpdatingFromThreadId { get; private set; }

    /// <summary>
    /// Updater has finished initial build
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Anything in RegionGrid that needs to be rebuilt
    /// </summary>
    public bool AnythingToRebuild
    {
      get
      {
        if (UpdatingRegion || !Enabled) return false;
        return !Initialized || mapping[createdFor].VehicleRegionDirtyer.AnyDirty;
      }
    }

    public void Init()
    {
      if (!mapping[createdFor].VehiclePathGrid.Enabled &&
        !mapping.GridOwners.TryForfeitOwnership(createdFor))
      {
        Trace.Fail("Trying to initialize region grids with no vehicle to claim ownership.");
        return;
      }

      Enabled = true;
      regionGrid = mapping[createdFor].VehicleRegionGrid;
      regionGrid.Init();
    }

    public void Release()
    {
      Initialized = false;
      Enabled = false;
      regionGrid.Release();
    }

    /// <summary>
    /// Should only be called for map generation so spawn events don't attempt to rebuild regions. 
    /// </summary>
    public void Disable()
    {
      Enabled = false;
    }

    /// <summary>
    /// Rebuild all regions
    /// </summary>
    public void RebuildAllVehicleRegions()
    {
      if (!Enabled)
      {
        Log.Warning(
          $"Called RebuildAllVehicleRegions but VehicleRegionAndRoomUpdater is disabled. " +
          $"VehicleRegions won't be rebuilt. StackTrace: {StackTraceUtility.ExtractStackTrace()}");
      }

      mapping[createdFor].VehicleRegionDirtyer.SetAllDirty();
      TryRebuildVehicleRegions();
    }

    /// <summary>
    /// Rebuild all regions on the map and generate associated rooms
    /// </summary>
    public void TryRebuildVehicleRegions()
    {
      if (UpdatingRegion || !Enabled) return;

      UpdatingRegion = true;
      if (!Initialized)
      {
        mapping[createdFor].VehicleRegionDirtyer.SetAllDirty();
      }
      else if (!mapping[createdFor].VehicleRegionDirtyer.AnyDirty)
      {
        UpdatingRegion = false;
        return;
      }

      try
      {
#if DEBUG
        UpdatingFromThreadId = Thread.CurrentThread.ManagedThreadId;
#endif
        RegenerateNewVehicleRegions();
        CreateOrUpdateVehicleRooms();
      }
      finally
      {
        newRegions.Clear();
        Initialized = true;
        UpdatingRegion = false;
      }
    }

    /// <summary>
    /// Generate regions with dirty cells
    /// </summary>
    private void RegenerateNewVehicleRegions()
    {
      newRegions.Clear();
      VehiclePathingSystem.VehiclePathData pathData = mapping[createdFor];
      foreach (IntVec3 cell in pathData.VehicleRegionDirtyer.DirtyCells)
      {
        if (!cell.InBounds(mapping.map))
        {
          Trace.Fail($"Dirtied invalid cell at {cell}");
          continue;
        }

        VehicleRegion region = pathData.VehicleRegionGrid.GetRegionAt(cell);

        // ObjectPool should never hold a region which still has references in the region grid.
        Assert.IsTrue(region == null || !region.InPool,
          $"{region} has been returned to pool prematurely.");

        if (region == null || !region.valid)
        {
          RegionResult result = pathData.VehicleRegionMaker.TryGenerateRegionFrom(cell, ref region);
          switch (result)
          {
            case RegionResult.Success:
            {
              newRegions.Add(region);
            }
              break;
            case RegionResult.NoRegion:
            {
              // Clean immediately rather than following RimWorld convention of delayed
              // Update-based clean.
              if (region != null)
              {
                regionGrid.SetRegionAt(cell, null);
              }
            }
              break;
          }
        }
      }
    }

    /// <summary>
    /// Update procedure for Rooms associated with Vehicle based regions
    /// </summary>
    private void CreateOrUpdateVehicleRooms()
    {
      newRooms.Clear();
      reusedOldRooms.Clear();
      int numRegionGroups = CombineNewRegionsIntoContiguousGroups();
      CreateOrAttachToExistingRooms(numRegionGroups);
      CombineNewAndReusedRoomsIntoContiguousGroups();
      newRooms.Clear();
      reusedOldRooms.Clear();
    }

    /// <summary>
    /// Combine rooms together with room group criteria met
    /// </summary>
    private int CombineNewAndReusedRoomsIntoContiguousGroups()
    {
      int num = 0;
      for (int i = 0; i < newRegions.Count; i++)
      {
        if (newRegions[i].newRegionGroupIndex < 0)
        {
          VehicleRegionTraverser.FloodAndSetNewRegionIndex(newRegions[i], num);
          num++;
        }
      }

      return num;
    }

    /// <summary>
    /// Create new room or attach to existing room with predetermined number of region groups
    /// </summary>
    /// <param name="numRegionGroups"></param>
    private void CreateOrAttachToExistingRooms(int numRegionGroups)
    {
      for (int i = 0; i < numRegionGroups; i++)
      {
        currentRegionGroup.Clear();
        for (int j = 0; j < newRegions.Count; j++)
        {
          if (newRegions[j].newRegionGroupIndex == i)
          {
            currentRegionGroup.Add(newRegions[j]);
          }
        }

        if (!currentRegionGroup[0].type.AllowsMultipleRegionsPerDistrict())
        {
          if (currentRegionGroup.Count != 1)
          {
            Log.Error(
              "Region type doesn't allow multiple regions per room but there are >1 regions in this group.");
          }

          VehicleRoom room = VehicleRoom.MakeNew(mapping.map, createdFor);
          currentRegionGroup[0].Room = room;
          newRooms.Add(room);
        }
        else
        {
          VehicleRoom room2 = FindCurrentRegionGroupNeighborWithMostRegions(out bool flag);
          if (room2 is null)
          {
            VehicleRoom item = VehicleRegionTraverser.FloodAndSetRooms(currentRegionGroup[0],
              mapping.map, createdFor, null);
            newRooms.Add(item);
          }
          else if (!flag)
          {
            for (int k = 0; k < currentRegionGroup.Count; k++)
            {
              currentRegionGroup[k].Room = room2;
            }

            reusedOldRooms.Add(room2);
          }
          else
          {
            VehicleRegionTraverser.FloodAndSetRooms(currentRegionGroup[0], mapping.map, createdFor,
              room2);
            reusedOldRooms.Add(room2);
          }
        }
      }
    }

    /// <summary>
    /// Combine regions that meet region group criteria
    /// </summary>
    private int CombineNewRegionsIntoContiguousGroups()
    {
      int num = 0;
      for (int i = 0; i < newRegions.Count; i++)
      {
        if (newRegions[i].newRegionGroupIndex < 0)
        {
          VehicleRegionTraverser.FloodAndSetNewRegionIndex(newRegions[i], num);
          num++;
        }
      }

      return num;
    }

    /// <summary>
    /// Find neighboring region group with most regions
    /// </summary>
    /// <param name="multipleOldNeighborRooms"></param>
    private VehicleRoom FindCurrentRegionGroupNeighborWithMostRegions(
      out bool multipleOldNeighborRooms)
    {
      multipleOldNeighborRooms = false;
      VehicleRoom room = null;
      for (int i = 0; i < currentRegionGroup.Count; i++)
      {
        foreach (VehicleRegion region in currentRegionGroup[i].NeighborsOfSameType)
        {
          if (region.Room != null && !reusedOldRooms.Contains(region.Room))
          {
            if (room == null)
            {
              room = region.Room;
            }
            else if (region.Room != room)
            {
              multipleOldNeighborRooms = true;
              if (region.Room.RegionCount > room.RegionCount)
              {
                room = region.Room;
              }
            }
          }
        }
      }

      return room;
    }
  }
}