﻿using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using SmashTools.UnitTesting;
using UnityEngine;
using UpdateLogTool;
using Verse;
using Verse.Sound;

namespace Vehicles;

public class Section_Debug : SettingsSection
{
  private const float ButtonHeight = 30f;
  private const float VerticalGap = 2f;
  private const int ButtonRows = 4;
  private const int DebugSectionColumns = 2;

  public bool debugDraftAnyVehicle;
  public bool debugShootAnyTurret;

  public bool debugDrawCannonGrid;
  public bool debugDrawNodeGrid;
  public bool debugDrawHitbox;
  public bool debugDrawVehicleTracks;
  public bool debugDrawBumpers;
  public bool debugDrawLordMeetingPoint;
  public bool debugDrawFleePoint;
  public FlashGridType debugDrawFlashGrid = FlashGridType.None;

  public bool debugLogging;
  public bool debugPathCostChanges;

  public bool debugDrawVehiclePathCosts;
  public bool debugDrawPathfinderSearch;

  public bool debugSpawnVehicleBuildingGodMode;
  public bool debugUseMultithreading = true;
  public bool debugLoadAssetBundles = true;

  public bool debugAllowRaiders;

  public override void ResetSettings()
  {
    base.ResetSettings();
    debugDraftAnyVehicle = false;
    debugShootAnyTurret = false;


    debugDrawCannonGrid = false;
    debugDrawNodeGrid = false;
    debugDrawHitbox = false;
    debugDrawVehicleTracks = false;
    debugDrawBumpers = false;
    debugDrawLordMeetingPoint = false;
    debugDrawFleePoint = false;
    debugDrawFlashGrid = FlashGridType.None;

    debugLogging = false;
    debugPathCostChanges = false;

    debugDrawVehiclePathCosts = false;
    debugDrawPathfinderSearch = false;

    debugSpawnVehicleBuildingGodMode = false;
    debugUseMultithreading = true;
    debugLoadAssetBundles = true;

    debugAllowRaiders = false;
  }

  public override void ExposeData()
  {
    Scribe_Values.Look(ref debugDraftAnyVehicle, nameof(debugDraftAnyVehicle));
    Scribe_Values.Look(ref debugShootAnyTurret, nameof(debugShootAnyTurret));

    Scribe_Values.Look(ref debugDrawCannonGrid, nameof(debugDrawCannonGrid));
    Scribe_Values.Look(ref debugDrawNodeGrid, nameof(debugDrawNodeGrid));
    Scribe_Values.Look(ref debugDrawHitbox, nameof(debugDrawHitbox));
    Scribe_Values.Look(ref debugDrawVehicleTracks, nameof(debugDrawVehicleTracks));
    Scribe_Values.Look(ref debugDrawBumpers, nameof(debugDrawBumpers));
    Scribe_Values.Look(ref debugDrawLordMeetingPoint, nameof(debugDrawLordMeetingPoint));
    Scribe_Values.Look(ref debugDrawFleePoint, nameof(debugDrawFleePoint));
    Scribe_Values.Look(ref debugDrawFlashGrid, nameof(debugDrawFlashGrid));

    Scribe_Values.Look(ref debugLogging, nameof(debugLogging));
    Scribe_Values.Look(ref debugPathCostChanges, nameof(debugPathCostChanges));

    Scribe_Values.Look(ref debugDrawVehiclePathCosts, nameof(debugDrawVehiclePathCosts));
    Scribe_Values.Look(ref debugDrawPathfinderSearch, nameof(debugDrawPathfinderSearch));

    if (DebugProperties.debug)
    {
      Scribe_Values.Look(ref debugSpawnVehicleBuildingGodMode,
        nameof(debugSpawnVehicleBuildingGodMode));

      Scribe_Values.Look(ref debugUseMultithreading, nameof(debugUseMultithreading),
        defaultValue: true);
      Scribe_Values.Look(ref debugLoadAssetBundles, nameof(debugLoadAssetBundles),
        defaultValue: true);
    }
#if RAIDERS
    Scribe_Values.Look(ref debugAllowRaiders, nameof(debugAllowRaiders));
#endif
  }

  public override void DrawSection(Rect rect)
  {
    Rect devModeRect = rect.ContractedBy(10);
    devModeRect.yMin += VehicleMod.ResetImageSize + 5;
    float buttonRowHeight = (ButtonHeight * ButtonRows + VerticalGap * (ButtonRows - 1));
    devModeRect.height = devModeRect.height - buttonRowHeight;

    listingStandard = new Listing_Standard();
    listingStandard.ColumnWidth =
      (devModeRect.width / DebugSectionColumns) - 4 * DebugSectionColumns;
    listingStandard.Begin(devModeRect);
    {
      using (new TextBlock(Color.white))
      {
        listingStandard.Header("VF_DevMode_Logging".Translate(), ListingExtension.BannerColor,
          fontSize: GameFont.Small, anchor: TextAnchor.MiddleCenter);
        listingStandard.CheckboxLabeled("VF_DevMode_DebugLogging".Translate(), ref debugLogging,
          "VF_DevMode_DebugLoggingTooltip".Translate());
#if DEBUG
        listingStandard.CheckboxLabeled(
          "VF_DevMode_DebugPathCostRecalculationLogging".Translate(), ref debugPathCostChanges,
          "VF_DevMode_DebugPathCostRecalculationLoggingTooltip".Translate());
#endif

        listingStandard.Header("VF_DevMode_Troubleshooting".Translate(),
          ListingExtension.BannerColor, fontSize: GameFont.Small,
          anchor: TextAnchor.MiddleCenter);
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDraftAnyVehicle".Translate(),
          ref debugDraftAnyVehicle, "VF_DevMode_DebugDraftAnyVehicleTooltip".Translate());
        bool shootAnyTurret = debugShootAnyTurret;
        listingStandard.CheckboxLabeled("VF_DevMode_DebugShootAnyTurret".Translate(),
          ref debugShootAnyTurret, "VF_DevMode_DebugShootAnyTurretTooltip".Translate());

        if (shootAnyTurret != debugShootAnyTurret &&
          Current.ProgramState == ProgramState.Playing && !Find.Maps.NullOrEmpty())
        {
          foreach (Map map in Find.Maps)
          {
            foreach (VehiclePawn vehicle in map.AllPawnsOnMap<VehiclePawn>(Faction.OfPlayer))
            {
              vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
            }
          }
        }
#if DEBUG
        listingStandard.Header("Debugging Only", ListingExtension.BannerColor,
          fontSize: GameFont.Small, anchor: TextAnchor.MiddleCenter);

#if RAIDERS
        listingStandard.CheckboxLabeledWithMessage("Raiders / Traders (Experimental)",
          (value) =>
            new Message("VF_WillRequireRestart".Translate(), MessageTypeDefOf.CautionInput),
          ref debugAllowRaiders,
          "Enables vehicle generation for NPCs.\n NOTE: This is an experimental feature. Use at your own risk.");
#endif
        listingStandard.CheckboxLabeled("VF_DevMode_DebugSpawnVehiclesGodMode".Translate(),
          ref debugSpawnVehicleBuildingGodMode,
          "VF_DevMode_DebugSpawnVehiclesGodModeTooltip".Translate());

        bool checkOn = debugUseMultithreading;
        listingStandard.CheckboxLabeled("Use Multithreading", ref checkOn);

        if (checkOn != debugUseMultithreading)
        {
          if (!checkOn)
          {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
              "Are you sure you want to disable multi-threading? Performance will decrease significantly. This should only be done for debugging.",
              delegate()
              {
                debugUseMultithreading = checkOn;
                RevalidateAllMapThreads();
              }));
          }
          else
          {
            debugUseMultithreading = checkOn;
            RevalidateAllMapThreads();
          }
        }

        listingStandard.CheckboxLabeled("Load AssetBundles", ref debugLoadAssetBundles);
#endif

        listingStandard.Header("VF_DevMode_Drawers".Translate(), ListingExtension.BannerColor,
          fontSize: GameFont.Small, anchor: TextAnchor.MiddleCenter);
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawUpgradeNodeGrid".Translate(),
          ref debugDrawNodeGrid,
          "VF_DevMode_DebugDrawUpgradeNodeGridTooltip".Translate());
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawHitbox".Translate(),
          ref debugDrawHitbox,
          "VF_DevMode_DebugDrawHitboxTooltip".Translate());

#if DEBUG
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawVehicleTracks".Translate(),
          ref debugDrawVehicleTracks,
          "VF_DevMode_DebugDrawVehicleTracksTooltip".Translate());
#endif

        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawBumpers".Translate(),
          ref debugDrawBumpers,
          "VF_DevMode_DebugDrawBumpersTooltip".Translate());
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawLordMeetingPoint".Translate(),
          ref debugDrawLordMeetingPoint,
          "VF_DevMode_DebugDrawLordMeetingPointTooltip".Translate());
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawFleePoints".Translate(),
          ref debugDrawFleePoint,
          "VF_DevMode_DebugDrawFleePointsTooltip".Translate());
#if !RELEASE // Disabled in VehicleMapping.MapComponentUpdate for Release builds
        listingStandard.EnumSliderLabeled("VF_DevMode_DebugDrawCoverGrid".Translate(),
          ref debugDrawFlashGrid,
          "VF_DevMode_DebugDrawCoverGridTooltip".Translate(), string.Empty,
          valueNameGetter: (FlashGridType type) => type.ToString());
#endif

        listingStandard.Header("VF_DevMode_Pathing".Translate(), ListingExtension.BannerColor,
          fontSize: GameFont.Small, anchor: TextAnchor.MiddleCenter);
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawVehiclePathingCosts".Translate(),
          ref debugDrawVehiclePathCosts,
          "VF_DevMode_DebugDrawVehiclePathingCostsTooltip".Translate());
        listingStandard.CheckboxLabeled("VF_DevMode_DebugDrawPathfinderSearch".Translate(),
          ref debugDrawPathfinderSearch,
          "VF_DevMode_DebugDrawPathfinderSearchTooltip".Translate());
      }
    }
    listingStandard.End();

    DoBottomButtons(devModeRect, buttonRowHeight);

    listingStandard.End();
  }

  private void DoBottomButtons(Rect rect, float buttonRowHeight)
  {
    Rect devModeButtonsRect = new Rect(rect);
    devModeButtonsRect.y = rect.yMax;
    devModeButtonsRect.height = buttonRowHeight;

    listingStandard.ColumnWidth = devModeButtonsRect.width / 3 - 17;
    listingStandard.Begin(devModeButtonsRect);
    {
      if (listingStandard.ButtonText("VF_DevMode_ShowRecentNews".Translate()))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        ShowAllUpdates();
      }
#if DEBUG
      if (listingStandard.ButtonText("VF_DevMode_OpenQuickTestSettings".Translate()))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        StartupTest.OpenMenu();
      }
#endif

      if (listingStandard.ButtonText("VF_DevMode_LogThreadActivity".Translate(),
        "VF_DevMode_LogThreadActivityTooltip"))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        Find.WindowStack.Add(new Dialog_DedicatedThreadActivity(delegate()
        {
          if (Find.CurrentMap == null)
          {
            return null;
          }

          VehicleMapping vehicleMapping = Find.CurrentMap.GetCachedMapComponent<VehicleMapping>();
          return vehicleMapping.dedicatedThread;
        }));
      }

      if (listingStandard.ButtonText("VF_DevMode_GraphEditor".Translate()))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        Find.WindowStack.Add(new Dialog_GraphEditor());
      }

      if (listingStandard.ButtonText("VF_DevMode_DebugPathfinderDebugging".Translate(),
        "VF_DevMode_DebugPathfinderDebuggingTooltip"))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        RegionDebugMenu();
      }

      if (listingStandard.ButtonText("VF_DevMode_DebugWorldPathfinderDebugging".Translate(),
        "VF_DevMode_DebugWorldPathfinderDebuggingTooltip"))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        WorldPathingDebugMenu();
      }

#if DEBUG
      if (listingStandard.ButtonText("Profiling"))
      {
        Find.WindowStack.Add(new Dialog_Profiler());
      }
#endif
    }
  }

  private void RevalidateAllMapThreads()
  {
    if (Current.ProgramState == ProgramState.Playing && !Find.Maps.NullOrEmpty())
    {
      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapComp = map.GetCachedMapComponent<VehicleMapping>();
        if (debugUseMultithreading)
        {
          mapComp.InitThread();
        }
        else
        {
          mapComp.ReleaseThread();
        }
      }
    }
  }

  public void RegionDebugMenu()
  {
    List<Toggle> vehicleDefToggles = new List<Toggle>();
    vehicleDefToggles.Add(new Toggle("None", () => DebugHelper.Local.VehicleDef == null ||
      DebugHelper.Local.DebugType ==
      DebugRegionType.None, delegate(bool value)
    {
      if (value)
      {
        DebugHelper.Local.VehicleDef = null;
        DebugHelper.Local.DebugType = DebugRegionType.None;
      }
    }));
    foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading.OrderBy(def =>
        def.modContentPack.ModMetaData.SamePackageId(VehicleHarmony.VehiclesUniqueId,
          ignorePostfix: true))
     .ThenBy(def => def.modContentPack.Name)
     .ThenBy(d => d.defName))
    {
      Toggle toggle = new Toggle(vehicleDef.defName, vehicleDef.modContentPack.Name,
        () => DebugHelper.Local.VehicleDef == vehicleDef,
        (value) => { }, onToggle: delegate(bool value)
        {
          if (value)
          {
            List<Toggle> debugOptionToggles =
              DebugHelper.DebugToggles(vehicleDef, DebugHelper.Local).ToList();
            Find.WindowStack.Add(new Dialog_ToggleMenu(
              "VF_DevMode_DebugPathfinderDebugging".Translate(), debugOptionToggles));
          }
          else
          {
            DebugHelper.Local.VehicleDef = null;
            DebugHelper.Local.DebugType = DebugRegionType.None;
          }
        });
      toggle.Disabled = !PathingHelper.ShouldCreateRegions(vehicleDef);
      vehicleDefToggles.Add(toggle);
    }

    Find.WindowStack.Add(
      new Dialog_RadioButtonMenu("VF_DevMode_DebugPathfinderDebugging".Translate(),
        vehicleDefToggles));
  }

  private static void WorldPathingDebugMenu()
  {
    List<Toggle> vehicleDefToggles = [];
    vehicleDefToggles.Add(new Toggle("None",
      () => DebugHelper.World.VehicleDef == null ||
        DebugHelper.World.DebugType == WorldPathingDebugType.None, delegate(bool value)
      {
        if (value)
        {
          DebugHelper.World.VehicleDef = null;
          DebugHelper.World.DebugType = WorldPathingDebugType.None;
        }
      }));
    foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading.OrderBy(def =>
        def.modContentPack.ModMetaData.SamePackageId(VehicleHarmony.VehiclesUniqueId,
          ignorePostfix: true))
     .ThenBy(def => def.modContentPack.Name)
     .ThenBy(d => d.defName))
    {
      Toggle toggle = new Toggle(vehicleDef.defName, vehicleDef.modContentPack.Name,
        () => DebugHelper.World.VehicleDef == vehicleDef, (value) => { },
        onToggle: delegate(bool value)
        {
          if (value)
          {
            List<Toggle> debugOptionToggles =
              DebugHelper.DebugToggles(vehicleDef, DebugHelper.World).ToList();
            Find.WindowStack.Add(new Dialog_RadioButtonMenu(
              "VF_DevMode_DebugWorldPathfinderDebugging".Translate(), debugOptionToggles));
          }
          else
          {
            DebugHelper.World.VehicleDef = null;
            DebugHelper.World.DebugType = WorldPathingDebugType.None;
          }
        });
      toggle.Disabled =
        !PathingHelper.ShouldCreateRegions(vehicleDef); // || !vehicleDef.canCaravan;
      vehicleDefToggles.Add(toggle);
    }

    Find.WindowStack.Add(
      new Dialog_RadioButtonMenu("VF_DevMode_DebugPathfinderDebugging".Translate(),
        vehicleDefToggles));
  }

  public void ShowAllUpdates()
  {
    string versionChecking = "Null";
    VehicleHarmony.updates.Clear();
    foreach (UpdateLog log in FileReader.ReadPreviousFiles(VehicleHarmony.VehicleMCP)
     .OrderByDescending(log =>
        Ext_Settings.CombineVersionString(log.UpdateData.currentVersion)))
    {
      VehicleHarmony.updates.Add(log);
    }

    try
    {
      List<DebugMenuOption> versions = [];
      foreach (UpdateLog update in VehicleHarmony.updates)
      {
        versionChecking = update.UpdateData.currentVersion;
        string label = versionChecking;
        if (versionChecking == VehicleHarmony.VehicleMMD.ModVersion)
        {
          label += " (Current)";
        }

        versions.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action,
          delegate() { Find.WindowStack.Add(new Dialog_NewUpdate([update])); }));
      }

      Find.WindowStack.Add(new Dialog_DebugOptionListLister(versions));
    }
    catch (Exception ex)
    {
      Log.Error(
        $"{VehicleHarmony.LogLabel} Unable to show update for {versionChecking} Exception = {ex}");
    }
  }
}