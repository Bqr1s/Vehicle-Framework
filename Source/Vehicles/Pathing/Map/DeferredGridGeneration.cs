using System;
using System.Collections.Generic;
using System.Diagnostics;
using LudeonTK;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using Verse;

namespace Vehicles;

public class DeferredGridGeneration
{
  private const int DaysUnusedForRemoval = 3;

  private readonly VehicleMapping mapping;

  private readonly GridCounter regionGrid = new();
  private readonly GridCounter pathGrid = new();

  public DeferredGridGeneration(VehicleMapping mapping)
  {
    this.mapping = mapping;
  }

  internal void GenerateAllPathGrids()
  {
    foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
    {
      Action postGenerationCallback = null;
#if DEBUG
      postGenerationCallback = () => CoroutineManager.QueueInvoke(() =>
        Ext_Messages.Message($"PathGrid generated for vehicle {vehicleDef.LabelCap}.",
          MessageTypeDefOf.SilentInput,
          time: 0.5f, historical: false));
#endif
      RequestPathGrid(vehicleDef, Urgency.Deferred,
        postGenerationCallback: postGenerationCallback);
    }
  }

  internal void GenerateAllRegionGrids()
  {
    foreach (VehicleDef ownerDef in GridOwners.AllOwners)
    {
      Action postGenerationCallback = null;
#if DEBUG
      postGenerationCallback = () => CoroutineManager.QueueInvoke(() =>
        Ext_Messages.Message($"Regions generated for owner {ownerDef.LabelCap}.",
          MessageTypeDefOf.SilentInput,
          time: 0.5f, historical: false));
#endif
      RequestRegionSet(ownerDef, Urgency.Deferred,
        postGenerationCallback: postGenerationCallback);
    }
  }

  public void RequestGridsFor(VehicleDef vehicleDef, Urgency urgency,
    Action postGenerationCallback = null)
  {
    if (RequestPathGrid(vehicleDef, urgency, postGenerationCallback) == Urgency.Urgent)
      Debug.Message($"Skipped deferred PathGrid generation for {vehicleDef}");
    if (RequestRegionSet(vehicleDef, urgency, postGenerationCallback) == Urgency.Urgent)
      Debug.Message($"Skipped deferred Region generation for {vehicleDef}");
  }

  public void DoPass()
  {
    Assert.IsTrue(regionGrid.UsedCount == 0);
    Assert.IsTrue(pathGrid.UsedCount == 0);
    foreach (Pawn pawn in mapping.map.mapPawns.AllPawns)
    {
      if (pawn is VehiclePawn vehicle)
      {
        pathGrid.SetUsed(vehicle.VehicleDef);
        regionGrid.SetUsed(GridOwners.GetOwner(vehicle.VehicleDef));
      }
    }

    foreach (Pawn pawn in Find.World.worldPawns.AllPawnsAlive)
    {
      if (pawn is VehiclePawn vehicle && vehicle.Faction.IsPlayerSafe())
      {
        pathGrid.SetUsed(vehicle.VehicleDef);
        regionGrid.SetUsed(GridOwners.GetOwner(vehicle.VehicleDef));
      }
    }

    // Release region set for vehicles not actively in use
    foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
    {
      if (GridOwners.IsOwner(vehicleDef) && regionGrid.IsUsed(vehicleDef))
        continue;

      if (pathGrid.IsUsed(vehicleDef))
        continue;

      pathGrid.IncrementUnused(vehicleDef);
      // Remove when exceeds days unused
      if (pathGrid.ShouldRemoveGrid(vehicleDef))
      {
        ReleasePathGrid(vehicleDef);
      }

      if (GridOwners.IsOwner(vehicleDef))
      {
        regionGrid.IncrementUnused(vehicleDef);
        if (regionGrid.ShouldRemoveGrid(vehicleDef))
        {
          ReleaseRegionSet(vehicleDef);
        }
      }
    }

    pathGrid.OnPassDone();
    regionGrid.OnPassDone();
  }

  private Urgency RequestPathGrid(VehicleDef vehicleDef, Urgency urgency,
    Action postGenerationCallback = null)
  {
    VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];
    // Path grid has already been initialized
    if (pathData.VehiclePathGrid.Enabled)
    {
      postGenerationCallback?.Invoke();
      return Urgency.None;
    }

    // If thread is not available, all rebuild requests are urgent
    if (urgency < Urgency.Urgent && mapping.ThreadAlive)
    {
      AsyncLongOperationAction longOperation = AsyncPool<AsyncLongOperationAction>.Get();
      longOperation.Set(GeneratePathGrid, () => !mapping.map.Disposed);
      mapping.dedicatedThread.Enqueue(longOperation);
      return Urgency.Deferred;
    }

    GeneratePathGrid();
    return Urgency.Urgent;

    void GeneratePathGrid()
    {
      if (pathData.VehiclePathGrid.Enabled) return;

      pathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();

      // Post-generation event should be invoked on the main thread
      if (postGenerationCallback != null)
      {
        CoroutineManager.QueueInvoke(postGenerationCallback);
      }
    }
  }

  private Urgency RequestRegionSet(VehicleDef vehicleDef, Urgency urgency,
    Action postGenerationCallback = null)
  {
    VehicleDef ownerDef = GridOwners.GetOwner(vehicleDef);
    VehicleMapping.VehiclePathData pathData = mapping[ownerDef];
    // Region grid has already been initialized
    if (!pathData.Suspended)
    {
      postGenerationCallback?.Invoke();
      return Urgency.None;
    }

    // If thread is not available, all rebuild requests are urgent
    if (urgency < Urgency.Urgent && mapping.ThreadAlive)
    {
      AsyncLongOperationAction longOperation = AsyncPool<AsyncLongOperationAction>.Get();
      longOperation.Set(GenerateRegions, () => !mapping.map.Disposed);
      mapping.dedicatedThread.Enqueue(longOperation);
      return Urgency.Deferred;
    }

    GenerateRegions();
    return Urgency.Urgent;

    void GenerateRegions()
    {
      if (!pathData.Suspended) return;

      pathData.VehicleRegionAndRoomUpdater.Init();
      pathData.VehicleRegionAndRoomUpdater.RebuildAllVehicleRegions();
      // post-generation event should be invoked on the main thread
      if (postGenerationCallback != null)
      {
        CoroutineManager.QueueInvoke(postGenerationCallback);
      }
    }
  }

  internal void ReleaseAll(VehicleDef vehicleDef)
  {
    ReleasePathGrid(vehicleDef);
    ReleaseRegionSet(GridOwners.GetOwner(vehicleDef));
  }

  private void ReleasePathGrid(VehicleDef ownerDef)
  {
    VehicleMapping.VehiclePathData pathData = mapping[ownerDef];
    if (pathData.VehiclePathGrid.Enabled)
    {
      pathData.VehiclePathGrid.Release();

#if DEBUG
      Ext_Messages.Message($"Released PathGrid for {ownerDef}", MessageTypeDefOf.SilentInput,
        time: 1f, historical: false);
#endif
    }
  }

  private void ReleaseRegionSet(VehicleDef ownerDef)
  {
    VehicleMapping.VehiclePathData pathData = mapping[ownerDef];
    if (!pathData.Suspended)
    {
      pathData.VehicleRegionAndRoomUpdater.Release();

#if DEBUG
      Ext_Messages.Message($"Released Regions for {ownerDef}", MessageTypeDefOf.SilentInput,
        time: 1f, historical: false);
#endif
    }
  }

  public static Urgency UrgencyFor(VehiclePawn vehicle)
  {
    // If null faction, vehicle is immovable anyways
    if (vehicle.Faction == null) return Urgency.None;

    if (!vehicle.Faction.IsPlayer)
    {
      // Non-player factions need grid urgently for incidents
      return Urgency.Urgent;
    }

    return Urgency.Deferred;
  }

  [DebugAction(VehicleHarmony.VehiclesLabel, "Force Remove Unused Regions")]
  private static void DoPassOnAllMaps()
  {
    // Simulate multiple days for multi-pass based removal
    for (int i = 0; i < DaysUnusedForRemoval; i++)
    {
      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        mapping.deferredGridGeneration.DoPass();
      }
    }
  }

  private class GridCounter
  {
    private readonly Dictionary<VehicleDef, int> countdownToRemoval = [];
    private readonly HashSet<VehicleDef> activelyUsed = [];

    public int UsedCount => activelyUsed.Count;

    public bool IsUsed(VehicleDef vehicleDef)
    {
      return activelyUsed.Contains(vehicleDef);
    }

    public void SetUsed(VehicleDef vehicleDef)
    {
      activelyUsed.Add(vehicleDef);
      countdownToRemoval.Remove(vehicleDef);
    }

    public void IncrementUnused(VehicleDef vehicleDef)
    {
      if (!countdownToRemoval.ContainsKey(vehicleDef))
        countdownToRemoval.Add(vehicleDef, 0);
      countdownToRemoval[vehicleDef]++;
    }

    /// <returns>VehicleDef grid has been unused past threshold and can be removed.</returns>
    public bool ShouldRemoveGrid(VehicleDef vehicleDef)
    {
      // Remove when exceeds days unused
      return countdownToRemoval[vehicleDef] >= DaysUnusedForRemoval;
    }

    public void OnPassDone()
    {
      activelyUsed.Clear();
    }
  }

  public enum Urgency
  {
    None,
    Deferred, // Queue request to thread
    Urgent, // Generate immediately
  }
}