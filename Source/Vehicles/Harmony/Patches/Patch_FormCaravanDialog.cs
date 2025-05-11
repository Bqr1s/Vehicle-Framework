using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using UnityEngine.Assertions;
using Vehicles.Rendering;
using Verse;

namespace Vehicles;

internal class Patch_FormCaravanDialog : IPatchCategory
{
  // Starting at a high offset just to avoid any int value clashing with the underlying enum
  // type. It will require changes anyways since the TabRecord will be missing the translation key
  // but at least it won't cause the tab to be completely hidden.
  private const int TabVehicles = 10;

  private const string VehiclesTabLabelKey = "VF_Vehicles";
  private const string PawnsTabLabelKey = "PawnsTab";
  private const string ItemsTabLabelKey = "ItemsTab";
  private const string TravelSuppliesTabLabelKey = "TravelSupplies";

  private static readonly string[] tabKeys =
    [PawnsTabLabelKey, ItemsTabLabelKey, TravelSuppliesTabLabelKey];

  private static readonly Type formCaravanTabEnumType;
  private static readonly Type splitCaravanTabEnumType;

  private static TransferableVehicleWidget vehiclesTransfer;
  private static int selectedTab;

  static Patch_FormCaravanDialog()
  {
    formCaravanTabEnumType = GenTypes.GetTypeInAnyAssembly("Dialog_FormCaravan+Tab", "RimWorld");
    splitCaravanTabEnumType = GenTypes.GetTypeInAnyAssembly("Dialog_SplitCaravan+Tab", "RimWorld");
  }

  public void PatchMethods()
  {
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.PostOpen)),
      prefix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(CreateTabListPostOpen)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Dialog_FormCaravan),
        nameof(Dialog_FormCaravan.PostClose)),
      postfix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(ClearTabListPostClose)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(CaravanUIUtility), "CreateCaravanTransferableWidgets"),
      postfix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(CreateTransferableVehicleWidget)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Dialog_FormCaravan),
        nameof(Dialog_FormCaravan.DoWindowContents)),
      transpiler: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(FormCaravanTabsTranspiler)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Dialog_FormCaravan),
        nameof(Dialog_FormCaravan.Notify_ChoseRoute)),
      postfix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(BestExitTileForVehicles)));

    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.Start),
        parameters: [typeof(Dialog_FormCaravan)]),
      prefix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(StartRoutePlanningForVehicles)));

    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Dialog_FormCaravan), "TryReformCaravan"),
      prefix: new HarmonyMethod(typeof(Patch_FormCaravanDialog),
        nameof(ConfirmLeaveVehiclesOnReform)));
  }

  private static void CreateTabListPostOpen(Dialog_FormCaravan __instance,
    List<TabRecord> ___tabsList)
  {
    Assert.IsTrue(___tabsList.NullOrEmpty());
    selectedTab = TabVehicles;
    ___tabsList.Add(new TabRecord(VehiclesTabLabelKey.Translate(),
      delegate { selectedTab = TabVehicles; },
      () => selectedTab == TabVehicles));
    foreach (int value in Enum.GetValues(formCaravanTabEnumType))
    {
      string translationKey = !tabKeys.OutOfBounds(value) ? tabKeys[value] : "Missing Label";
      ___tabsList.Add(new TabRecord(translationKey.Translate(), delegate { selectedTab = value; },
        () => selectedTab == value));
    }
  }

  private static void ClearTabListPostClose(Dialog_FormCaravan __instance,
    List<TabRecord> ___tabsList)
  {
    ___tabsList.Clear();
    selectedTab = TabVehicles;
  }

  private static void CreateTransferableVehicleWidget(List<TransferableOneWay> transferables,
    string thingCountTip,
    IgnorePawnsInventoryMode ignorePawnInventoryMass, Func<float> availableMassGetter,
    bool ignoreSpawnedCorpsesGearAndInventoryMass, PlanetTile tile,
    bool playerPawnsReadOnly = false)
  {
    vehiclesTransfer = new TransferableVehicleWidget(null, null, null, thingCountTip,
      drawMass: true,
      ignorePawnInventoryMass: ignorePawnInventoryMass, includePawnsMassInMassUsage: false,
      availableMassGetter: availableMassGetter, extraHeaderSpace: 0,
      ignoreSpawnedCorpseGearAndInventoryMass: ignoreSpawnedCorpsesGearAndInventoryMass, tile: tile,
      drawMarketValue: true);
    vehiclesTransfer.AddSection("VF_Vehicles".Translate(),
      transferables.Where(t => t.AnyThing is VehiclePawn));
  }

  private static IEnumerable<CodeInstruction> FormCaravanTabsTranspiler(
    IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
  {
    // ReSharper disable ExtractCommonBranchingCode
    List<CodeInstruction> instructionList = [.. instructions];

    FieldInfo tabListField = AccessTools.Field(typeof(Dialog_FormCaravan), "tabsList");
    FieldInfo tabField = AccessTools.Field(typeof(Dialog_FormCaravan), "tab");
    MethodInfo clearTabList =
      AccessTools.Method(typeof(List<TabRecord>), nameof(List<TabRecord>.Clear));
    bool tabClearing = false;
    bool switchBlockClearing = false;
    for (int i = 0; i < instructionList.Count; i++)
    {
      CodeInstruction instruction = instructionList[i];

      // NOTE - We search for begin and end of tab init since Ludeon creates the tab list in every
      // single OnGUI event. Since we've created the list once in PostOpen, we skip the entire block
      // and just handle the drawing with our "inserted" enum value.
      if (instruction.LoadsField(tabListField) && instructionList[i + 1].Calls(clearTabList))
      {
        if (!tabClearing)
        {
          // Flag transpiler to start skipping instructions until we reach the 2nd tabsList.Clear()
          tabClearing = true;
        }
        else
        {
          tabClearing = false;
          instruction = instructionList[++i]; // Ldsfld: Dialog_FormCaravan::tabsList
          instruction = instructionList[++i]; // Callvirt: List`1<TabRecord>::Clear
          // ref inRect
          yield return new CodeInstruction(opcode: OpCodes.Ldarga_S, operand: 1);
          // Dialog_FormCaravan::tabsList
          yield return new CodeInstruction(opcode: OpCodes.Ldsfld, operand: tabListField);
          // DrawTabList(ref inRect, tabsList);
          yield return new CodeInstruction(opcode: OpCodes.Call,
            operand: AccessTools.Method(typeof(Patch_FormCaravanDialog), nameof(DrawTabList)));
        }
      }
      // Since we're using our own int-based values parallel to the Tab enum, we can skip the entire
      // switch block and just handle the drawing ourselves. If we didn't do this, the enum would never
      // be able to hold our Vehicle tab int value, so it would never be drawn.
      else if (!tabClearing && instruction.LoadsField(tabField))
      {
        switchBlockClearing = true;
        instruction = instructionList[++i]; // Ldfld: Dialog_FormCaravan::tab
        // transferablesRect
        yield return new CodeInstruction(opcode: OpCodes.Ldloc_S, operand: 4);
        // Dialog_FormCaravan::tabsList
        yield return new CodeInstruction(opcode: OpCodes.Ldsfld, operand: tabListField);
        // out bool anythingChanged
        yield return new CodeInstruction(opcode: OpCodes.Ldloca_S, operand: 5);
        // this->pawnsTransfer
        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldfld,
          operand: AccessTools.Field(typeof(Dialog_FormCaravan), "pawnsTransfer"));
        // this->pawnsTransfer
        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldfld,
          operand: AccessTools.Field(typeof(Dialog_FormCaravan), "itemsTransfer"));
        // this->pawnsTransfer
        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldfld,
          operand: AccessTools.Field(typeof(Dialog_FormCaravan), "travelSuppliesTransfer"));
        // DrawActiveTab(this, inRect, tabsList, out anythingChanged);
        yield return new CodeInstruction(opcode: OpCodes.Call,
          operand: AccessTools.Method(typeof(Patch_FormCaravanDialog), nameof(DrawActiveTab)));
      }
      if (switchBlockClearing && instructionList[i + 1].opcode == OpCodes.Ldloc_S &&
        instructionList[i + 1].operand is LocalBuilder { LocalIndex: 5 })
      {
        switchBlockClearing = false;
        instruction = instructionList[++i]; // Br_S: IL_02D8  (end of switch block)
      }

      if (!tabClearing && !switchBlockClearing)
        yield return instruction;
    }
    // ReSharper restore ExtractCommonBranchingCode
  }

  private static void DrawTabList(ref Rect inRect, List<TabRecord> tabsList)
  {
    inRect.yMin += 119f;
    Widgets.DrawMenuSection(inRect);
    TabDrawer.DrawTabs(inRect, tabsList);
  }

  private static void DrawActiveTab(Dialog_FormCaravan __instance, Rect transferablesRect,
    List<TabRecord> tabsList, out bool anythingChanged, TransferableOneWayWidget pawnsTransfer,
    TransferableOneWayWidget itemsTransfer, TransferableOneWayWidget travelSuppliesTransfer)
  {
    anythingChanged = false;
    switch (selectedTab)
    {
      case 0: // Dialog_FormCaravan.Tab.Pawns
        pawnsTransfer.OnGUI(transferablesRect, out anythingChanged);
      break;
      case 1: // Dialog_FormCaravan.Tab.Items
        itemsTransfer.OnGUI(transferablesRect, out anythingChanged);
      break;
      case 2: // Dialog_FormCaravan.Tab.TravelSupplies
        travelSuppliesTransfer.OnGUI(transferablesRect, out anythingChanged);
      break;
      case TabVehicles: // Vehicles Tab
        vehiclesTransfer.OnGUI(transferablesRect /*, out anythingChanged*/);
      break;
      default:
        Log.Error(
          $"Unknown enum type {selectedTab} for patched FormCaravan dialog. Switching back to known tab");
        selectedTab = 0;
      break;
    }
  }

  private static void BestExitTileForVehicles(Dialog_FormCaravan __instance,
    ref PlanetTile ___destinationTile)
  {
    // TODO
  }

  private static bool StartRoutePlanningForVehicles(WorldRoutePlanner __instance,
    Dialog_FormCaravan formCaravanDialog, ref bool ___active)
  {
    //List<TransferableOneWay> vehicles =
    //  formCaravanDialog.transferables.Where(transf => transf.AnyThing is VehiclePawn);
    //if (___active)
    //{
    //  __instance.Stop();
    //}
    return true;
  }

  /// <summary>
  /// Show DialogMenu for confirmation on leaving vehicles behind when forming caravan
  /// </summary>
  private static bool ConfirmLeaveVehiclesOnReform(Dialog_FormCaravan __instance,
    ref List<TransferableOneWay> ___transferables, Map ___map, PlanetTile ___destinationTile,
    ref bool __result)
  {
    if (___map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).HasVehicle())
    {
      List<Pawn> pawns = TransferableUtility.GetPawnsFromTransferables(___transferables);
      List<Pawn> correctedPawns = pawns.Where(p => p is not VehiclePawn).ToList();
      string vehicles = "";
      foreach (Pawn pawn in pawns.Where(p => p is VehiclePawn))
      {
        vehicles += pawn.LabelShort;
      }

      Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
        "VF_LeaveVehicleBehindCaravan".Translate(vehicles), delegate
        {
          if (!(bool)AccessTools.Method(typeof(Dialog_FormCaravan), "CheckForErrors")
           .Invoke(__instance, [correctedPawns]))
          {
            return;
          }
          AccessTools
           .Method(typeof(Dialog_FormCaravan), "AddItemsFromTransferablesToRandomInventories")
           .Invoke(__instance, [correctedPawns]);
          VehicleCaravan caravan = CaravanHelper.ExitMapAndCreateVehicleCaravan(correctedPawns,
            Faction.OfPlayer, __instance.CurrentTile, __instance.CurrentTile, ___destinationTile,
            false);
          ___map.Parent.CheckRemoveMapNow();
          TaggedString taggedString = "MessageReformedCaravan".Translate();
          if (caravan.vehiclePather.Moving && caravan.vehiclePather.ArrivalAction != null)
          {
            taggedString += " " + "MessageFormedCaravan_Orders".Translate() + ": " +
              caravan.vehiclePather.ArrivalAction.Label + ".";
          }
          Messages.Message(taggedString, caravan, MessageTypeDefOf.TaskCompletion, false);
        }));
      __result = true;
      return false;
    }
    return true;
  }
}