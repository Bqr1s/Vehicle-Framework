using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Performance;
using UnityEngine;
using Verse;
using Verse.Sound;
using Urgency = Vehicles.DeferredRegionGenerator.Urgency;

namespace Vehicles
{
  /// <summary>
  /// MapComponent container for all pathing related sub-components for vehicles
  /// </summary>
  [StaticConstructorOnStartup]
  public sealed class VehicleMapping : MapComponent
  {
    private const int EventMapId = 0;
    public const bool ForceGenerateAllRegions = true;

    private VehiclePathData[] vehicleData;

    private VehicleDef buildingFor;
    private int ownerCleanIndex = 0;

    internal DedicatedThread dedicatedThread;
    internal DeferredRegionGenerator deferredRegionGenerator;

    private int deferredRegionsCalculatedDayOfYear;

    public VehicleMapping(Map map) : base(map)
    {
      ConstructComponents();
    }

    private static int DayOfYearAt0Long => GenDate.DayOfYear(GenTicks.TicksAbs, 0f);

    /// <summary>
    /// <see cref="dedicatedThread"/> is initialized and running.
    /// </summary>
    public bool ThreadAlive => dedicatedThread != null && dedicatedThread.thread.IsAlive;

    /// <summary>
    /// <see cref="dedicatedThread"/> is alive, not suspended, and not in a long operation.
    /// </summary>
    /// <remarks>Verify this is true before queueing up a method, otherwise you may just be sending it to the void 
    /// where it will never be executed.</remarks>
    public bool ThreadAvailable => ThreadAlive && !dedicatedThread.Suspended;

    /// <summary>
    /// Generates all path data if they haven't been already and fetches
    /// <see cref="VehiclePathData"/> for <paramref name="vehicleDef"/>.
    /// </summary>
    public VehiclePathData this[VehicleDef vehicleDef]
    {
      get
      {
#if DEBUG
        if (buildingFor == vehicleDef)
        {
          Assert.Fail(@"Trying to pull VehiclePathData by indexing when it's currently in the middle of generation. 
Recursion is not supported here.");
          return null;
        }
#endif
        return vehicleData[vehicleDef.DefIndex];
      }
    }

    internal void InitThread(Map map)
    {
      if (dedicatedThread != null)
      {
        ReleaseThread();
      }

      if (!VehicleMod.settings.debug.debugUseMultithreading)
      {
        Log.Warning($"Loading map without DedicatedThread. This will cause performance issues. Map={map}.");
        return;
      }

      if (map.info?.parent == null)
      {
        return; // MapParent won't have reference resolved when loading from save, GetDedicatedThread will be called a 2nd time on PostLoadInit
      }

      dedicatedThread = GetDedicatedThread(map);
      deferredRegionGenerator = new DeferredRegionGenerator(this);
    }

    private static DedicatedThread GetDedicatedThread(Map map)
    {
      DedicatedThread thread;
      if (map.IsPlayerHome)
      {
        thread = ThreadManager.CreateNew();
        Log.Message($"<color=orange>{VehicleHarmony.LogLabel} Creating thread (id={thread?.id})</color>");
        return thread;
      }

      thread = ThreadManager.GetShared(EventMapId);
      Log.Message($"<color=orange>{VehicleHarmony.LogLabel} Fetching thread from pool (id={thread?.id})</color>");
      return thread;
    }

    /// <summary>
    /// Check if <paramref name="vehicleDef"/> is an owner of a region set
    /// </summary>
    /// <param name="vehicleDef"></param>
    [Obsolete("Use VehicleHarmony.gridOwners instead")]
    public bool IsOwner(VehicleDef vehicleDef)
    {
      return GridOwners.IsOwner(vehicleDef);
    }

    [Obsolete("Use VehicleHarmony.gridOwners instead")]
    public VehicleDef GetOwner(VehicleDef vehicleDef)
    {
      return GridOwners.GetOwner(vehicleDef);
    }

    public List<VehicleDef> GetPiggies(VehicleDef ownerDef)
    {
      List<VehicleDef> owners = [];
      if (!GridOwners.IsOwner(ownerDef))
      {
        return owners;
      }

      foreach (VehicleDef vehicleDef in VehicleHarmony.AllMoveableVehicleDefs)
      {
        if (!GridOwners.IsOwner(vehicleDef))
        {
          VehicleDef matchingOwnerDef = GridOwners.GetOwner(vehicleDef);
          if (matchingOwnerDef == ownerDef)
          {
            owners.Add(vehicleDef);
          }
        }
      }

      return owners;
    }

    /// <summary>
    /// Finalize initialization for map component
    /// </summary>
    public override void FinalizeInit()
    {
      base.FinalizeInit();
      RegenerateGrids();
    }

    public void RegenerateGrids(bool forceRegenerate = ForceGenerateAllRegions)
    {
      Ext_Map.StashLongEventText();

      GeneratePathGrids();

      if (!ThreadAlive)
      {
        // Init dedicated thread after map generation to avoid duplicate pathgrid and region recalcs
        InitThread(map);
      }

      // Unit tests need all regions generated before execution. Dedicated thread would also be getting
      // suspended sporadically during unit testing so deferred generation will get interrupted.
      if (deferredRegionGenerator != null && !UnitTestManager.RunningUnitTests)
      {
        if (forceRegenerate) deferredRegionGenerator.GenerateAllRegions();
      }
      else
      {
        Debug.Message($"Skipping deferred generation for regions...");
        GenerateRegionsAsync();
      }

      Ext_Map.RevertLongEventText();
    }

    private void GeneratePathGrids()
    {
      if (vehicleData.NullOrEmpty())
      {
        return;
      }

      for (int i = 0; i < vehicleData.Length; i++)
      {
        VehiclePathData vehiclePathData = vehicleData[i];
        LongEventHandler
         .SetCurrentEventText($"{"VF_GeneratingPathGrids".Translate()} {i}/{vehicleData.Length}");
        //Needs to check validity, non-pathing vehicles are still indexed since sequential vehicles will have higher index numbers
        if (vehiclePathData.IsValid)
        {
          vehiclePathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
        }
      }
    }

    private void GenerateRegions()
    {
      if (!GridOwners.AnyOwners) return;

      int total = GridOwners.AllOwners.Count;
      for (int i = 0; i < total; i++)
      {
        VehicleDef vehicleDef = GridOwners.AllOwners[i];
        LongEventHandler.SetCurrentEventText($"{"VF_GeneratingRegions".Translate()} {i}/{total}");

        VehiclePathData vehiclePathData = this[vehicleDef];
        vehiclePathData.VehicleRegionAndRoomUpdater.Init();
        vehiclePathData.VehicleRegionAndRoomUpdater.RebuildAllVehicleRegions();
      }
    }

    private void GenerateRegionsAsync()
    {
      if (!GridOwners.AnyOwners) return;

      if (GridOwners.AllOwners.Count <= 3)
      {
        // Generating regions is a lot faster now, so anything below 2~3
        // can just be done synchronously. Will take < 1s regardless.
        GenerateRegions();
        return;
      }

      DeepProfiler.Start("Vehicle Regions");
      Parallel.ForEach(GridOwners.AllOwners, delegate(VehicleDef vehicleDef)
      {
        LongEventHandler.SetCurrentEventText("VF_GeneratingRegions".Translate());
        VehiclePathData vehiclePathData = this[vehicleDef];
        vehiclePathData.VehicleRegionAndRoomUpdater.Init();
        vehiclePathData.VehicleRegionAndRoomUpdater.RebuildAllVehicleRegions();
      });
      DeepProfiler.End();
    }

    /// <summary>
    /// Construct and cache <see cref="VehiclePathData"/> for each moveable <see cref="VehicleDef"/> 
    /// </summary>
    public void ConstructComponents()
    {
      int size = DefDatabase<VehicleDef>.DefCount;
      vehicleData = new VehiclePathData[size];

      GenerateAllPathData();
      DisableAllRegionUpdaters();
    }

    public void DisableAllRegionUpdaters()
    {
      foreach (VehicleDef vehicleDef in GridOwners.AllOwners)
      {
        VehiclePathData pathData = this[vehicleDef];
        pathData.VehicleRegionAndRoomUpdater.Disable();
      }
    }

    public override void ExposeData()
    {
      base.ExposeData();
      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        if (dedicatedThread == null)
        {
          InitThread(map);
        }
      }
    }

    public void VehicleSpawned(VehiclePawn vehicle)
    {
      // Try to generate regions immediately for a vehicle being spawned and cut in line
      // in front of any deferred region requests that may have just been queued.
      deferredRegionGenerator?.GenerateRegionsFor(vehicle.VehicleDef, Urgency.Deferred);
    }

    public override void MapRemoved()
    {
      ReleaseThread();
    }

    internal bool ReleaseThread()
    {
      if (dedicatedThread == null) return false;

      Log.Message($"<color=orange>Releasing thread {dedicatedThread.id}.</color>");
      bool released = dedicatedThread.Release();
      dedicatedThread = null;
      return released;
    }

    public override void MapComponentTick()
    {
      base.MapComponentTick();
      if (ThreadAlive && deferredRegionGenerator != null)
      {
        int dayOfYear = DayOfYearAt0Long;
        if (deferredRegionsCalculatedDayOfYear != dayOfYear)
        {
          deferredRegionGenerator.DoPass();
          deferredRegionsCalculatedDayOfYear = dayOfYear;
        }
      }
    }

    public override void MapComponentUpdate()
    {
      UpdateRegions();
#if DEBUG
      FlashGridType flashGridType = VehicleMod.settings.debug.debugDrawFlashGrid;
      if (flashGridType != FlashGridType.None)
      {
        if (Find.CurrentMap != null && !WorldRendererUtility.WorldRenderedNow)
        {
          switch (flashGridType)
          {
            case FlashGridType.CoverGrid:
              FlashCoverGrid();
              break;
            case FlashGridType.GasGrid:
              FlashGasGrid();
              break;
            case FlashGridType.PositionManager:
              FlashClaimants();
              break;
            case FlashGridType.ThingGrid:
              FlashThingGrid();
              break;
            default:
              Log.ErrorOnce($"Not Implemented: {flashGridType}", flashGridType.GetHashCode());
              break;
          }
        }
      }
#endif
    }

    private void FlashCoverGrid()
    {
      if (!Find.TickManager.Paused)
      {
        foreach (IntVec3 cell in Find.CameraDriver.CurrentViewRect)
        {
          float cover = CoverUtility.TotalSurroundingCoverScore(cell, map);
          map.debugDrawer.FlashCell(cell, cover / 8, cover.ToString("F2"), duration: 1);
        }
      }
    }

    private void FlashGasGrid()
    {
      if (!Find.TickManager.Paused)
      {
        foreach (IntVec3 cell in Find.CameraDriver.CurrentViewRect)
        {
          if (!map.gasGrid.GasCanMoveTo(cell)) continue;

          float gas = map.gasGrid.DensityPercentAt(cell, GasType.BlindSmoke);
          map.debugDrawer.FlashCell(cell, gas / 8, gas.ToString("F2"), duration: 1);
        }
      }
    }

    private void FlashClaimants()
    {
      if (!Find.TickManager.Paused)
      {
        var manager = map.GetCachedMapComponent<VehiclePositionManager>();
        foreach (IntVec3 cell in Find.CameraDriver.CurrentViewRect)
        {
          if (!manager.PositionClaimed(cell)) continue;

          map.debugDrawer.FlashCell(cell, 1, duration: 1);
        }
      }
    }

    private void FlashThingGrid()
    {
      if (!Find.TickManager.Paused)
      {
        foreach (IntVec3 cell in Find.CameraDriver.CurrentViewRect)
        {
          Thing thing = map.thingGrid.ThingAt(cell, ThingCategory.Pawn);
          if (thing is not VehiclePawn) continue;

          map.debugDrawer.FlashCell(cell, 1, duration: 1);
        }
      }
    }

    private void UpdateRegions()
    {
      if (!GridOwners.AnyOwners) return;

      if (ownerCleanIndex < GridOwners.AllOwners.Count)
      {
        VehicleDef vehicleDef = GridOwners.AllOwners[ownerCleanIndex];
        VehiclePathData pathData = this[vehicleDef];
        if (!pathData.Suspended && pathData.VehicleRegionDirtyer.AnyDirty)
        {
          if (ThreadAvailable)
          {
            AsyncRebuildRegionsAction action = AsyncPool<AsyncRebuildRegionsAction>.Get();
            action.Set(pathData);
            dedicatedThread.Queue(action);
          }
          else
          {
            // NOTE - This is not executed on the dedicated thread, I don't think this is necessary
            // anymore but it needs further testing + a unit test to ensure no invalid regions are left
            // behind in the region grid.
            pathData.VehicleRegionGrid.UpdateClean();
            pathData.VehicleRegionAndRoomUpdater.TryRebuildVehicleRegions();
          }
        }

        ownerCleanIndex++;
        if (ownerCleanIndex >= GridOwners.AllOwners.Count) ownerCleanIndex = 0;
      }
    }

    private void GenerateAllPathData()
    {
      // All vehicles need path data (even aerial vehicles for landing)
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        GeneratePathData(vehicleDef);
      }
    }

    /// <summary>
    /// Generate new <see cref="VehiclePathData"/> for <paramref name="vehicleDef"/>
    /// </summary>
    /// <param name="vehicleDef"></param>
    private VehiclePathData GeneratePathData(VehicleDef vehicleDef)
    {
      VehiclePathData vehiclePathData = new VehiclePathData(vehicleDef);
      vehicleData[vehicleDef.DefIndex] = vehiclePathData;
      bool isOwner = GridOwners.IsOwner(vehicleDef);

      buildingFor = vehicleDef;
      {
        vehiclePathData.VehiclePathGrid = new VehiclePathGrid(this, vehicleDef);
        vehiclePathData.VehiclePathFinder = new VehiclePathFinder(this, vehicleDef);

        if (isOwner)
        {
          vehiclePathData.ReachabilityData =
            new VehicleReachabilitySettings(this, vehicleDef, vehiclePathData);
        }
        else
        {
          // Will return itself if it's an owner
          VehicleDef ownerDef = GridOwners.GetOwner(vehicleDef);
          vehiclePathData.ReachabilityData = vehicleData[ownerDef.DefIndex].ReachabilityData;
        }
      }
      buildingFor = null;

      vehiclePathData.VehiclePathGrid.PostInit();
      vehiclePathData.VehiclePathFinder.PostInit();
      if (isOwner)
      {
        vehiclePathData.ReachabilityData.PostInit();
      }

      return vehiclePathData;
    }

    [DebugOutput(VehicleHarmony.VehiclesLabel, name = "Benchmark RegionGen",
                 onlyWhenPlaying = true)]
    private static void BenchmarkRegionGeneration()
    {
      const int IterationsPerTest = 50;
      const int MaxTimeForBenchmark = 10; // minutes

      CameraJumper.TryHideWorld();
      Map map = Find.CurrentMap;
      if (map is null)
      {
        Assert.Fail("Trying to perform region benchmark with null map.");
        return;
      }

      Stopwatch timeout = new();

      LongEventHandler.QueueLongEvent(delegate()
      {
        List<VehicleDef> regionOwners =
          GridOwners.AllOwners.Where(PathingHelper.ShouldCreateRegions).ToList();
        int total = regionOwners.Count;
        // Results will be useless if we don't have at least 5 owners for varied results
        Assert.IsTrue(total > 5);
        // No need for testing more than 10 owners. Test will stall for far too long and we
        // already know that parallelization will be far superior at >10. We're just
        // benchmarking for what vehicle count is the cutoff for async performance gains.
        total = Mathf.Min(total, 10);

        VehicleMapping mapping = Find.CurrentMap.GetCachedMapComponent<VehicleMapping>();

        // x2 since we're testing both sync and async region generation per count
        int totalVehiclesTested = Ext_Math.ArithmeticSeries(total, IterationsPerTest * 2);
        int tested = 0;

        timeout.Start();
        // Rerun test incrementing how many vehicles to generate regions for
        // and log results from benchmarking.
        for (int i = 1; i <= total; i++)
        {
          List<VehicleDef> defsToTest = regionOwners.Take(i).ToList();

          Benchmark.Results asyncResult = Benchmark.Run(IterationsPerTest, delegate()
          {
            // delegate will add a little bit of overhead from CallVirt but
            // this is how the original method is written so it's accurate.
            Parallel.ForEach(defsToTest, delegate(VehicleDef vehicleDef)
            {
              VehiclePathData vehiclePathData = mapping[vehicleDef];
              vehiclePathData.VehicleRegionAndRoomUpdater.Init();
              vehiclePathData.VehicleRegionAndRoomUpdater.RebuildAllVehicleRegions();

              Interlocked.Increment(ref tested);
              LongEventHandler
               .SetCurrentEventText($"Running Benchmark {tested}/{totalVehiclesTested}");
            });
          });

          Benchmark.Results syncResult = Benchmark.Run(IterationsPerTest, delegate()
          {
            foreach (VehicleDef vehicleDef in defsToTest)
            {
              VehiclePathData vehiclePathData = mapping[vehicleDef];
              vehiclePathData.VehicleRegionAndRoomUpdater.Init();
              vehiclePathData.VehicleRegionAndRoomUpdater.RebuildAllVehicleRegions();

              Interlocked.Increment(ref tested);
              LongEventHandler
               .SetCurrentEventText($"Running Benchmark {tested}/{totalVehiclesTested}");
            }
          });

          Log.Message($"{i} | Async({asyncResult.TotalString}) Sync({syncResult.TotalString})");
          if (timeout.Elapsed.TotalMinutes >= MaxTimeForBenchmark) break;
        }

        timeout.Stop();
        SoundDefOf.TinyBell.PlayOneShotOnCamera();
      }, string.Empty, true, null);
    }

    /// <summary>
    /// Container for all path related subcomponents specific to a <see cref="VehicleDef"/>.
    /// </summary>
    /// <remarks>Stores data strictly for deviations from vanilla regarding impassable values</remarks>
    public class VehiclePathData
    {
      public VehiclePathData(VehicleDef vehicleDef)
      {
        Owner = vehicleDef;

        VehiclePathGrid = null;
        VehiclePathFinder = null;
        ReachabilityData = null;
      }

      public bool IsValid => Owner != null;

      // Default true, suspended indicates region grid is currently disabled.
      public bool Suspended { get; internal set; } = true;

      public VehicleDef Owner { get; }

      internal VehicleReachabilitySettings ReachabilityData { get; set; }

      public VehiclePathGrid VehiclePathGrid { get; set; }

      public VehiclePathFinder VehiclePathFinder { get; set; }

      public VehicleReachability VehicleReachability => ReachabilityData.reachability;

      public VehicleRegionGrid VehicleRegionGrid => ReachabilityData.regionGrid;

      public VehicleRegionMaker VehicleRegionMaker => ReachabilityData.regionMaker;

      public VehicleRegionAndRoomUpdater VehicleRegionAndRoomUpdater =>
        ReachabilityData.regionAndRoomUpdater;

      public VehicleRegionDirtyer VehicleRegionDirtyer => ReachabilityData.regionDirtyer;
    }

    public class VehicleReachabilitySettings
    {
      public readonly VehicleRegionGrid regionGrid;
      public readonly VehicleRegionMaker regionMaker;
      public readonly VehicleRegionAndRoomUpdater regionAndRoomUpdater;
      public readonly VehicleRegionDirtyer regionDirtyer;
      public readonly VehicleReachability reachability;

      public VehicleReachabilitySettings(VehicleMapping vehicleMapping, VehicleDef vehicleDef,
        VehiclePathData pathData)
      {
        regionGrid = new VehicleRegionGrid(vehicleMapping, vehicleDef);
        regionMaker = new VehicleRegionMaker(vehicleMapping, vehicleDef);
        regionAndRoomUpdater = new VehicleRegionAndRoomUpdater(vehicleMapping, vehicleDef);
        regionDirtyer = new VehicleRegionDirtyer(vehicleMapping, vehicleDef);
        reachability =
          new VehicleReachability(vehicleMapping, vehicleDef, pathData.VehiclePathGrid, regionGrid);
      }

      public void PostInit()
      {
        regionGrid.PostInit();
        regionMaker.PostInit();
        regionAndRoomUpdater.PostInit();
        regionDirtyer.PostInit();
        reachability.PostInit();
      }
    }
  }
}