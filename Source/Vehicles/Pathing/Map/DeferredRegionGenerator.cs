#define LAZY_REGIONS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using LudeonTK;
using RimWorld;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Performance;
using Verse;

namespace Vehicles
{
  public class DeferredRegionGenerator
  {
    public const int DaysUnusedForRemoval = 3;

    private readonly VehicleMapping mapping;

    private readonly Dictionary<VehicleDef, int> countdownToRemoval = [];

    private static readonly HashSet<VehicleDef> activelyUsedVehicles = [];

    public DeferredRegionGenerator(VehicleMapping mapping)
    {
      this.mapping = mapping;
    }

    internal void GenerateAllRegions()
    {
      foreach (VehicleDef ownerDef in GridOwners.AllOwners)
      {
        if (mapping[ownerDef].Suspended) continue;

        Action postGenerationAction = null;
#if DEBUG
        postGenerationAction = () => CoroutineManager.QueueInvoke(() =>
          Messages.Message($"Regions generated for owner {ownerDef.LabelCap}.", MessageTypeDefOf.SilentInput));
#endif
        RequestRegionSet(ownerDef, Urgency.Deferred, postGenerationAction: postGenerationAction);
      }
    }

    public void GenerateRegionsFor(VehicleDef vehicleDef, Urgency urgency, Action postGenerationAction = null)
    {
      if (RequestRegionSet(vehicleDef, urgency, postGenerationAction) == Urgency.Urgent)
      {
        Debug.Message($"Skipped deferred generation for {vehicleDef}");
      }
    }

    [Conditional("LAZY_REGIONS")]
    public void DoPass()
    {
      Assert.IsTrue(activelyUsedVehicles.Count == 0);
      foreach (Pawn pawn in mapping.map.mapPawns.AllPawns)
      {
        if (pawn is VehiclePawn vehicle)
        {
          activelyUsedVehicles.Add(GridOwners.GetOwner(vehicle.VehicleDef));
        }
      }
      foreach (Pawn pawn in Find.World.worldPawns.AllPawnsAlive)
      {
        if (pawn is VehiclePawn vehicle)
        {
          activelyUsedVehicles.Add(GridOwners.GetOwner(vehicle.VehicleDef));
        }
      }

      // Release region set for vehicles not actively in use
      foreach (VehicleDef ownerDef in GridOwners.AllOwners)
      {
        if (activelyUsedVehicles.Contains(ownerDef))
        {
          countdownToRemoval.Remove(ownerDef);
          continue;
        }

        // Make sure entry has been added or incremented for days unused
        if (!countdownToRemoval.ContainsKey(ownerDef))
          countdownToRemoval.Add(ownerDef, 1);
        else
          countdownToRemoval[ownerDef] += 1;

        // Remove when exceeds days unused
        if (countdownToRemoval[ownerDef] >= DaysUnusedForRemoval)
        {
          ReleaseRegionSet(ownerDef);
        }
      }
      activelyUsedVehicles.Clear();
    }

    private Urgency RequestRegionSet(VehicleDef vehicleDef, Urgency urgency, Action postGenerationAction = null)
    {
      VehicleDef ownerDef = GridOwners.GetOwner(vehicleDef);
      VehicleMapping.VehiclePathData pathData = mapping[ownerDef];
      // Region grid has already been initialized
      if (!pathData.Suspended)
      {
        postGenerationAction?.Invoke();
        return Urgency.None;
      }

      // If thread is not available, all rebuild requests are urgent
      if (urgency < Urgency.Urgent && mapping.ThreadAlive)
      {
        var longOperation = AsyncPool<AsyncLongOperationAction>.Get();
        longOperation.Set(GenerateRegions, () => !mapping.map.Disposed);
        mapping.dedicatedThread.Queue(longOperation);
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
        if (postGenerationAction != null)
        {
          CoroutineManager.QueueInvoke(postGenerationAction);
        }
      }
    }

    private void ReleaseRegionSet(VehicleDef ownerDef)
    {
      countdownToRemoval.Remove(ownerDef);
      VehicleMapping.VehiclePathData pathData = mapping[ownerDef];
      if (!pathData.Suspended)
      {
        pathData.VehicleRegionAndRoomUpdater.Release();

#if DEBUG
        Messages.Message($"Released Regions for {ownerDef}", MessageTypeDefOf.SilentInput, historical: false);
#endif
      }
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
          mapping.deferredRegionGenerator.DoPass();
        }
      }
    }

    public enum Urgency
    {
      None,
      Deferred, // Queue request to thread
      Urgent, // Generate immediately
    }
  }
}
