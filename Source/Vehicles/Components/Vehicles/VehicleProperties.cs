﻿using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using Verse;

namespace Vehicles;

[VehicleSettingsClass]
[HeaderTitle(Label = "VF_Properties", Translate = true)]
public class VehicleProperties
{
#if FISHING
		[PostToSettings(Label = "VF_FishingEnabled", Tooltip = "VF_FishingEnabledTooltip", Translate =
 true, UISettingsType = UISettingsType.Checkbox, VehicleType = VehicleType.Sea)]
		[DisableSettingConditional(MayRequireAny = new string[] { CompatibilityPackageIds.VE_Fishing })]
#endif
  public bool fishing = false;

  public VehicleTrack track;

  [PostToSettings(Label = "VF_CollisionMultiplier", Tooltip = "VF_CollisionMultiplierTooltip",
    Translate = true, UISettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0, MaxValue = 2, Increment = 0.05f, RoundDecimalPlaces = 2)]
  public float pawnCollisionMultiplier = 0.5f;

  [PostToSettings(Label = "VF_CollisionVehicleMultiplier",
    Tooltip = "VF_CollisionVehicleMultiplierTooltip", Translate = true,
    UISettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0, MaxValue = 2, Increment = 0.05f, RoundDecimalPlaces = 2)]
  public float pawnCollisionRecoilMultiplier = 0.5f;

  public List<VehicleJobLimitations> vehicleJobLimitations = [];

  public bool diagonalRotation = true;

  [PostToSettings(Label = "VF_ManhunterTargetsVehicle",
    Tooltip = "VF_ManhunterTargetsVehicleTooltip", Translate = true,
    UISettingsType = UISettingsType.Checkbox)]
  public bool manhunterTargetsVehicle = false;

  [PostToSettings(Label = "VF_CanAdaptToEMP", Tooltip = "VF_CanAdaptToEMPTooltip",
    Translate = true, UISettingsType = UISettingsType.Checkbox)]
  [DisableSettingConditional(MemberType = typeof(VehicleDef),
    Property = nameof(VehicleDef.CanDisableEMPSetting), DisableIfEqualTo = true,
    DisableReason = "VF_VehicleCannotStun")]
  public bool canAdaptToEMP = false;

  /// <summary>
  /// Player-facing only, allowing players to disable emp stuns for a vehicle without having to modify components via patches.
  /// Only enabled if any component within the vehicle has an emp severity > None.
  /// </summary>
  [Unsaved]
  [PostToSettings(Label = "VF_EMPStuns", Tooltip = "VF_EMPStunsTooltip", Translate = true,
    UISettingsType = UISettingsType.Checkbox)]
  [DisableSettingConditional(MemberType = typeof(VehicleDef),
    Property = nameof(VehicleDef.CanDisableEMPSetting), DisableIfEqualTo = true,
    DisableReason = "VF_VehicleCannotStun")]
  public bool empStuns;

  public string iconTexPath;
  public bool generateThingIcon = true;

  public SimpleDictionary<DamageDef, float> damageDefMultipliers;

  //---------------   Pathing   ---------------

  public bool defaultTerrainImpassable = false;
  public bool defaultBiomesImpassable = false;

  // Local Pathing
  /// <summary>
  /// Additional tick cost for snow, clamped between 0 and 450 ticks.
  /// </summary>
  public SimpleDictionary<WeatherBuildupCategory, int> customWeatherCosts;

  /// <summary>
  /// Set to 10000 for impassable terrain
  /// </summary>
  public SimpleDictionary<TerrainDef, int> customTerrainCosts;

  /// <summary>
  /// Set to 10000 for impassable thing
  /// </summary>
  public SimpleDictionary<ThingDef, int> customThingCosts;

  // World Pathing
  [PostToSettings(Label = "VF_OffRoadMultiplier", Tooltip = "VF_OffRoadMultiplierTooltip",
    Translate = true, UISettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0.01f, MaxValue = 2, RoundDecimalPlaces = 1)]
  public float offRoadMultiplier = 1;

  public float riverCost = -1;

  public SimpleDictionary<RiverDef, float> customRiverCosts;

  public SimpleDictionary<BiomeDef, float> customBiomeCosts;

  public SimpleDictionary<Hilliness, float> customHillinessCosts;

  public SimpleDictionary<RoadDef, float> customRoadCosts;

  //-------------------------------------------

  [PostToSettings(Label = "VF_WinterSpeedMultiplier", Tooltip = "VF_WinterSpeedMultiplierTooltip",
    Translate = true, UISettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0, MaxValue = 10, RoundDecimalPlaces = 1)]
  [LoadAlias("winterSpeedMultiplier")] //Changed in 1.5.1381       1.6 - Remove LoadAlias
  [LoadAlias(
    "winterCostMultiplier")] //Changed in 1.5.1644        1.6 - Remove LoadAlias, Change UpgradeStatDef to match this name
  public float winterCost = 2f;

  [PostToSettings(Label = "VF_WorldSpeedMultiplier", Tooltip = "VF_WorldSpeedMultiplierTooltip",
    Translate = true, UISettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0, MaxValue = 10, RoundDecimalPlaces = 1)]
  public float worldSpeedMultiplier = 2.5f;

  public List<FactionDef> restrictToFactions;

  [TweakField]
  public List<VehicleRole> roles = [];

  public IEnumerable<string> ConfigErrors(VehicleDef vehicleDef)
  {
    yield break;
  }

  public void ResolveReferences(VehicleDef vehicleDef)
  {
    vehicleJobLimitations ??= [];

    customRiverCosts ??= new SimpleDictionary<RiverDef, float>();
    customBiomeCosts ??= new SimpleDictionary<BiomeDef, float>();
    customHillinessCosts ??= new SimpleDictionary<Hilliness, float>();
    customRoadCosts ??= new SimpleDictionary<RoadDef, float>();
    customTerrainCosts ??= new SimpleDictionary<TerrainDef, int>();
    customThingCosts ??= new SimpleDictionary<ThingDef, int>();
    customWeatherCosts ??= new SimpleDictionary<WeatherBuildupCategory, int>();

    if (riverCost > 0)
    {
      float minWidth = vehicleDef.Size.x * Ext_Math.Sqrt2;
      //Allow river travel on all larger rivers
      foreach (RiverDef riverDef in DefDatabase<RiverDef>.AllDefsListForReading)
      {
        if (!customRiverCosts.ContainsKey(riverDef) &&
          ModSettingsHelper.RiverMultiplier(riverDef) >= minWidth)
        {
          customRiverCosts[riverDef] = riverCost;
        }
      }
    }
    if (!roles.NullOrEmpty())
    {
      foreach (VehicleRole role in roles)
      {
        role.ResolveReferences(vehicleDef);
      }
    }
    empStuns = SettingsCache.TryGetValue(vehicleDef, typeof(VehicleProperties),
      nameof(empStuns),
      fallback: vehicleDef.components.NotNullAndAny(props =>
        props.empSeverity > VehicleEMPSeverity.None));
  }

  public void PostDefDatabase(VehicleDef vehicleDef)
  {
    string defName = vehicleDef.defName;

    XmlHelper.FillDefaults_Enum(defName, nameof(customWeatherCosts), customWeatherCosts);
    XmlHelper.FillDefaults_Def(defName, nameof(customTerrainCosts), customTerrainCosts);
    XmlHelper.FillDefaults_Def(defName, nameof(customThingCosts), customThingCosts);

    XmlHelper.FillDefaults_Def(defName, nameof(customRiverCosts), customRiverCosts);
    XmlHelper.FillDefaults_Def(defName, nameof(customBiomeCosts), customBiomeCosts);
    XmlHelper.FillDefaults_Enum(defName, nameof(customHillinessCosts), customHillinessCosts);
    XmlHelper.FillDefaults_Def(defName, nameof(customRoadCosts), customRoadCosts);
  }
}