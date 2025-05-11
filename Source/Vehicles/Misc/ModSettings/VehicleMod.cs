﻿//#define UPGRADES_TAB

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using SmashTools;
using SmashTools.Performance;

namespace Vehicles;

[StaticConstructorOnStartup]
public class VehicleMod : Mod
{
  public const int MaxCoastalSettlementPush = 21;
  public const float ResetImageSize = 22;

  public static VehiclesModSettings settings;
  public static VehicleMod mod;

  internal static VehicleDef selectedDef;
  private static SettingsSection currentSection;

  internal string currentKey;
  internal static UpgradeNode selectedNode;
  internal static List<PatternDef> selectedPatterns = [];
  internal static CompProperties_UpgradeTree selectedDefUpgradeComp;

  private static List<TabRecord> tabs = [];

  internal static List<FieldInfo> vehicleDefFields = [];
  private static Dictionary<Type, List<FieldInfo>> vehicleCompFields = [];
  internal static readonly Dictionary<Type, List<FieldInfo>> cachedFields = [];
  internal static readonly HashSet<string> settingsDisabledFor = [];

  public VehicleMod(ModContentPack content) : base(content)
  {
    mod = this;
    settings = GetSettings<VehiclesModSettings>();
    InitializeSections();

    settings.colorStorage ??= new ColorStorage();
    selectedPatterns ??= [];

    CurrentSection = settings.main;
  }

  public static bool ModifiableSettings => settings.main.modifiableSettings;

  public static int CoastRadius =>
    settings.main.forceFactionCoastRadius >= MaxCoastalSettlementPush ?
      9999 :
      settings.main.forceFactionCoastRadius;

  public static float FishingSkillValue => settings.main.fishingSkillIncrease / 100f;

  public static SettingsSection CurrentSection
  {
    get { return currentSection; }
    set
    {
      if (currentSection == value)
        return;

      currentSection?.OnClose();
      currentSection = value;
      currentSection?.OnOpen();
    }
  }

  public static Dictionary<Type, List<FieldInfo>> VehicleCompFields
  {
    get
    {
      if (vehicleCompFields.NullOrEmpty())
      {
        ResetSelectedCachedTypes();
        vehicleDefFields =
          vehicleCompFields.TryGetValue(typeof(VehicleDef), []);
        vehicleCompFields.Remove(typeof(VehicleDef));
        vehicleCompFields.RemoveAll(d => d.Value.NullOrEmpty() || d.Value.All(f =>
          f.TryGetAttribute(out PostToSettingsAttribute postToSettings) &&
          postToSettings.UISettingsType == UISettingsType.None));
        vehicleCompFields = vehicleCompFields
         .OrderByDescending(d => d.Key == typeof(List<VehicleStatModifier>))
         .ThenByDescending(d => d.Key.SameOrSubclass(typeof(VehicleProperties)))
         .ThenByDescending(d => d.Key.SameOrSubclass(typeof(VehicleJobLimitations)))
         .ThenByDescending(d => d.Key.IsAssignableFrom(typeof(CompProperties)))
         .ThenByDescending(d => d.Key.IsClass)
         .ThenByDescending(d => d.Key.IsValueType && !d.Key.IsPrimitive && !d.Key.IsEnum)
         .ToDictionary(d => d.Key, d => d.Value);
      }
      return vehicleCompFields;
    }
  }

  public static void SelectVehicle(VehicleDef vehicleDef)
  {
    selectedDef = vehicleDef;
    ClearSelectedDefCache();
    selectedPatterns = DefDatabase<PatternDef>.AllDefsListForReading
     .Where(d => d.ValidFor(selectedDef)).ToList();
    selectedDefUpgradeComp = vehicleDef.GetSortedCompProperties<CompProperties_UpgradeTree>();
    CurrentSection.VehicleSelected();
  }

  public static void DeselectVehicle()
  {
    selectedDef = null;
    selectedPatterns.Clear();
    selectedDefUpgradeComp = null;
    selectedNode = null;
  }

  private static void InitializeSections()
  {
    settings.main ??= new Section_Main();
    settings.main.Initialize();

    settings.vehicles ??= new Section_Vehicles();
    settings.vehicles.Initialize();

    settings.upgrades ??= new Section_Upgrade();
    settings.upgrades.Initialize();

    settings.debug ??= new Section_Debug();
    settings.debug.Initialize();
  }

  private static void ClearSelectedDefCache()
  {
    vehicleCompFields.Clear();
    vehicleDefFields.Clear();
  }

  private static void ResetSelectedCachedTypes()
  {
    if (selectedDef != null)
    {
      foreach (FieldInfo field in selectedDef.GetType().GetPostSettingsFields())
      {
        IterateTypeFields(typeof(VehicleDef), field);
      }
      foreach (CompProperties comp in selectedDef.comps)
      {
        foreach (FieldInfo field in comp.GetType().GetPostSettingsFields())
        {
          IterateTypeFields(comp.GetType(), field);
        }
      }
    }
  }

  private static void IterateTypeFields(Type containingType, FieldInfo field)
  {
    if (field.TryGetAttribute(out PostToSettingsAttribute postToSettingsAttr))
    {
      if (postToSettingsAttr.ParentHolder)
      {
        foreach (FieldInfo innerField in field.FieldType.GetPostSettingsFields())
        {
          IterateTypeFields(field.FieldType, innerField);
        }
      }
      else
      {
        if (!vehicleCompFields.ContainsKey(containingType))
        {
          vehicleCompFields.Add(containingType, []);
        }
        vehicleCompFields[containingType].Add(field);
      }
    }
  }

  internal static void PopulateCachedFields()
  {
    QuickIter.EnumerateAllModTypes(CacheForType);
  }

  private static void CacheForType(Type type)
  {
    if (!type.HasAttribute<VehicleSettingsClassAttribute>())
      return;

    List<FieldInfo> fields = type.GetPostSettingsFields();
    if (!fields.NullOrEmpty())
    {
      cachedFields[type] = fields;
    }
  }

  public void InitializeTabs()
  {
    tabs =
    [
      new TabRecord("VF_MainSettings".Translate(),
        delegate { CurrentSection = settings.main; }, () => CurrentSection == settings.main),
    ];
    if (ModifiableSettings)
    {
      tabs.Add(new TabRecord("VF_Vehicles".Translate(), delegate
      {
        CurrentSection = settings.vehicles;
        _ = SectionDrawer.VehicleDefs; // Trigger recache
      }, () => CurrentSection == settings.vehicles));
#if UPGRADES_TAB
				tabs.Add(new TabRecord("VF_Upgrades".Translate(), delegate()
				{
					CurrentSection = settings.upgrades;
				}, () => CurrentSection == settings.upgrades));
#endif
    }
    tabs.Add(new TabRecord("VF_DevMode".Translate(),
      delegate { CurrentSection = settings.debug; }, () => CurrentSection == settings.debug));
  }

  public override void DoSettingsWindowContents(Rect inRect)
  {
    const float Padding = ResetImageSize + 5;

    base.DoSettingsWindowContents(inRect);

    Rect menuRect = inRect.ContractedBy(10f);
    menuRect.y += 20f;
    menuRect.height -= 20f;

    Widgets.DrawMenuSection(menuRect);
    TabDrawer.DrawTabs(menuRect, tabs);

    CurrentSection.OnGUI(menuRect);

    /* Reset Buttons */
    Rect resetAllButton = new(menuRect.width - Padding, menuRect.y + 15, ResetImageSize,
      ResetImageSize);

    if (Widgets.ButtonImage(CurrentSection.ButtonRect(resetAllButton), VehicleTex.ResetPage))
    {
      List<FloatMenuOption> options = CurrentSection.ResetOptions.ToList();
      FloatMenu floatMenu = new(options)
      {
        vanishIfMouseDistant = true
      };
      Find.WindowStack.Add(floatMenu);
    }
  }

  public override string SettingsCategory()
  {
    return "VehicleFramework".Translate();
  }

  public static void ResetAllSettings()
  {
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
      "VF_DevMode_ResetAllConfirmation".Translate(), ResetAllSettingsConfirmed));
  }

  private static void ResetAllSettingsConfirmed()
  {
    SoundDefOf.Click.PlayOneShotOnCamera();
    cachedFields.Clear();
    PopulateCachedFields();
    settings.main.ResetSettings();
    settings.vehicles.ResetSettings();
    settings.upgrades.ResetSettings();
    settings.debug.ResetSettings();

    if (Current.ProgramState == ProgramState.Playing)
    {
      foreach (Map map in Find.Maps)
      {
        map.GetCachedMapComponent<VehicleReservationManager>().ReleaseAllClaims();
      }
    }
  }

  public override void WriteSettings()
  {
    base.WriteSettings();
    selectedNode = null;
    Find.WindowStack.Windows.FirstOrDefault(w => w is Dialog_NodeSettings)?.Close();
  }
}