using System.Collections.Generic;
using System.Threading;
using SmashTools;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// Region grid for vehicle specific regions
  /// </summary>
  public sealed class VehicleRegionGrid : VehicleGridManager
  {
    private const int CleanSquaresPerFrame = 16;

    //Thread Safe - Only accessed from the same thread within the same method
    private HashSet<VehicleRegion> allRegionsYielded;

    //Thread safe - Only used inside UpdateClean
    private int curCleanIndex;
    private VehicleRegion[] regionGrid;

    public ConcurrentSet<VehicleRoom> allRooms = [];

    private VehicleRegionAndRoomUpdater regionUpdater;

    public VehicleRegionGrid(VehicleMapping mapping, VehicleDef createdFor) : base(mapping,
      createdFor)
    {
    }

    /// <summary>
    /// Region grid getter
    /// </summary>
    public VehicleRegion[] DirectGrid => regionGrid;

    /// <summary>
    /// Yield all non-null regions
    /// </summary>
    public IEnumerable<VehicleRegion> AllRegions_NoRebuild_InvalidAllowed
    {
      get
      {
        allRegionsYielded.Clear();
        try
        {
          int count = mapping.map.cellIndices.NumGridCells;
          for (int i = 0; i < count; i++)
          {
            VehicleRegion region = GetRegionAt(i);
            if (region != null && !allRegionsYielded.Contains(region))
            {
              yield return region;
              allRegionsYielded.Add(region);
            }
          }
        }
        finally
        {
          allRegionsYielded.Clear();
        }
      }
    }

    internal bool AnyInvalidRegions
    {
      get
      {
        for (int i = 0; i < regionGrid.Length; i++)
        {
          if (regionGrid[i] is VehicleRegion region && !region.valid)
            return true;
        }

        return false;
      }
    }

    /// <summary>
    /// All valid regions in grid
    /// </summary>
    public IEnumerable<VehicleRegion> AllRegions
    {
      get
      {
        allRegionsYielded.Clear();
        try
        {
          int count = mapping.map.cellIndices.NumGridCells;
          for (int i = 0; i < count; i++)
          {
            VehicleRegion region = GetRegionAt(i);
            if (region != null && region.valid && !allRegionsYielded.Contains(region))
            {
              yield return region;
              allRegionsYielded.Add(region);
            }
          }
        }
        finally
        {
          allRegionsYielded.Clear();
        }
      }
    }

    public void Release()
    {
      regionGrid = null;
      allRooms.Clear();
    }

    public void Init()
    {
      // RegionGrid is large in size and could still be in-use if rebuild-all is called
      // from debug menu. No need to reallocate the entire array if this is the case.
      regionGrid ??= new VehicleRegion[mapping.map.cellIndices.NumGridCells];
      allRegionsYielded = [];
      allRooms = [];
      regionUpdater = mapping[createdFor].VehicleRegionAndRoomUpdater;
    }

    /// <summary>
    /// Retrieve valid region at <paramref name="cell"/>
    /// </summary>
    public VehicleRegion GetValidRegionAt(IntVec3 cell, bool rebuild = true)
    {
      if (!cell.InBounds(mapping.map))
      {
        Log.Error($"Tried to get valid vehicle region for {createdFor} out of bounds at {cell}");
        return null;
      }

      if (rebuild)
      {
        if (!regionUpdater.Enabled && regionUpdater.AnythingToRebuild)
        {
          Log.Warning($"Trying to get valid vehicle region for {createdFor} at {cell} but " +
            $"RegionAndRoomUpdater is disabled. The result may be incorrect.");
        }

        regionUpdater.TryRebuildVehicleRegions();
      }

      VehicleRegion region = GetRegionAt(cell);
      return (region != null && region.valid) ? region : null;
    }

    /// <summary>
    /// Get region from grid at <paramref name="cell"/>
    /// </summary>
    public VehicleRegion GetRegionAt(IntVec3 cell)
    {
      int index = mapping.map.cellIndices.CellToIndex(cell);
      return GetRegionAt(index);
    }

    /// <summary>
    /// Get region from grid at <paramref name="index"/>
    /// </summary>
    public VehicleRegion GetRegionAt(int index)
    {
      // regionGrid will be null if region set has been suspended from updating
      return regionGrid?[index];
    }

    /// <summary>
    /// Set existing region at <paramref name="cell"/> to <paramref name="region"/>
    /// </summary>
    public void SetRegionAt(IntVec3 cell, VehicleRegion region)
    {
      SetRegionAt(mapping.map.cellIndices.CellToIndex(cell), region);
    }

    /// <summary>
    /// Set existing region at <paramref name="index"/> to <paramref name="region"/>
    /// </summary>
    public void SetRegionAt(int index, VehicleRegion region)
    {
      VehicleRegion other = regionGrid[index];
      other?.DecrementRefCount();
      region?.IncrementRefCount();
      Interlocked.CompareExchange(ref regionGrid[index], region, regionGrid[index]);
    }

    /// <summary>
    /// Remove all references of region from grid.
    /// </summary>
    /// <remarks>Prepares region for refresh without moving to object pool.</remarks>
    internal void ClearFromGrid(VehicleRegion region)
    {
      // NOTE - Reference count needs to be set to 0 after region is removed from grid.
      // or else we may encounter unexpected region pooling. Region should be reset
      // after all references have been removed from grid.
      foreach (IntVec3 cell in region.Cells)
      {
        int index = mapping.map.cellIndices.CellToIndex(cell);
        Interlocked.CompareExchange(ref regionGrid[index], null, regionGrid[index]);
      }

      region.Reset();
    }

    /// <summary>
    /// Update regionGrid and purge all invalid regions
    /// </summary>
    public void UpdateClean()
    {
      for (int i = 0; i < CleanSquaresPerFrame; i++)
      {
        if (curCleanIndex >= regionGrid.Length)
        {
          curCleanIndex = 0;
        }

        VehicleRegion region = regionGrid[curCleanIndex];
        if (region != null && !region.valid)
        {
          Assert.Fail("Cleaning region which should have already been returned to pool.");
          SetRegionAt(curCleanIndex, null);
        }

        curCleanIndex++;
      }
    }

    /// <summary>
    /// Draw debug data
    /// </summary>
    public void DebugDraw(DebugRegionType debugRegionType)
    {
      if (mapping.map != Find.CurrentMap) return;

      foreach (VehicleRoom room in allRooms.Keys)
      {
        room.DebugDraw(debugRegionType);
      }

      if (DebugProperties.drawAllRegions)
      {
        foreach (VehicleRegion debugRegion in AllRegions_NoRebuild_InvalidAllowed)
        {
          debugRegion.DebugDraw(debugRegionType);
        }
      }

      IntVec3 intVec = UI.MouseCell();
      if (intVec.InBounds(mapping.map))
      {
        VehicleRegion region = GetRegionAt(intVec);
        region?.DebugDraw(debugRegionType);
      }
    }

    /// <summary>
    /// Draw OnGUI label path costs
    /// </summary>
    public void DebugOnGUI(DebugRegionType debugRegionType)
    {
      IntVec3 intVec = UI.MouseCell();
      if (intVec.InBounds(mapping.map))
      {
        VehicleRegion region = GetRegionAt(intVec);
        region?.DebugOnGUIMouseover(debugRegionType);
      }
    }
  }
}