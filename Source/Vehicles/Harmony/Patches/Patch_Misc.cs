using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles;

internal class Patch_Misc : IPatchCategory
{
  public const float IconBarDim = 30;

  public void PatchMethods()
  {
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(MapPawns), "PlayerEjectablePodHolder"),
      prefix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(PlayerEjectableVehicles)));
    VehicleHarmony.Patch(original: AccessTools.Method(typeof(Pawn), nameof(Pawn.GetChildHolders)),
      prefix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(GetVehicleHandlerIThingHolders)));

    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(Selector), "HandleMapClicks"),
      prefix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(MultiSelectFloatMenu)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(MentalState_Manhunter),
        nameof(MentalState_Manhunter.ForceHostileTo), [typeof(Thing)]), prefix: null,
      postfix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(ManhunterDontAttackVehicles)));
    VehicleHarmony.Patch(
      original: AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.Paused)),
      postfix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(PausedFromVehicles)));
    VehicleHarmony.Patch(
      original: AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed)),
      postfix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(ForcePauseFromVehicles)));
    VehicleHarmony.Patch(original: AccessTools.Method(typeof(Dialog_ManageAreas), "DoAreaRow"),
      transpiler: new HarmonyMethod(typeof(Patch_Misc),
        nameof(VehicleAreaRowTranspiler)));

    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(PawnCapacitiesHandler),
        nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty)),
      prefix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(RecheckVehicleHandlerCapacities)));
    VehicleHarmony.Patch(original: AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
      prefix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(MoveOnDeath)));
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(PawnUtility),
        nameof(PawnUtility.ShouldSendNotificationAbout)),
      postfix: new HarmonyMethod(typeof(Patch_Misc),
        nameof(SendNotificationsVehicle)));
  }

  private static bool PlayerEjectableVehicles(Thing thing, ref IThingHolder __result)
  {
    if (thing is VehiclePawn vehicle)
    {
      __result = vehicle;
      return false;
    }
    return true;
  }

  private static void GetVehicleHandlerIThingHolders(Pawn __instance,
    List<IThingHolder> outChildren)
  {
    if (__instance is VehiclePawn vehicle && !vehicle.handlers.NullOrEmpty())
    {
      outChildren.AddRange(vehicle.handlers);
    }
  }

  private static bool MultiSelectFloatMenu(List<object> ___selected)
  {
    if (Event.current.type == EventType.MouseDown)
    {
      if (Event.current.button == 1 && ___selected.Count > 0)
      {
        if (___selected.Count > 1)
        {
          return !SelectionHelper.MultiSelectClicker(___selected);
        }
      }
    }
    return true;
  }

  private static void ManhunterDontAttackVehicles(Thing t, ref bool __result)
  {
    if (__result && t is VehiclePawn vehicle && !SettingsCache.TryGetValue(
      vehicle.VehicleDef, typeof(VehicleProperties),
      nameof(VehicleProperties.manhunterTargetsVehicle),
      vehicle.VehicleDef.properties.manhunterTargetsVehicle))
    {
      __result = false;
    }
  }

  private static void PausedFromVehicles(ref bool __result)
  {
    if (LandingTargeter.Instance.ForcedTargeting || StrafeTargeter.Instance.ForcedTargeting)
    {
      __result = true;
    }
  }

  private static void ForcePauseFromVehicles(ref TimeSpeed __result)
  {
    if (LandingTargeter.Instance.ForcedTargeting || StrafeTargeter.Instance.ForcedTargeting)
    {
      __result = TimeSpeed.Paused;
    }
  }

  private static IEnumerable<CodeInstruction> VehicleAreaRowTranspiler(
    IEnumerable<CodeInstruction> instructions)
  {
    List<CodeInstruction> instructionList = instructions.ToList();

    for (int i = 0; i < instructionList.Count; i++)
    {
      CodeInstruction instruction = instructionList[i];

      if (instruction.Calls(AccessTools.Method(typeof(WidgetRow), nameof(WidgetRow.Icon))))
      {
        yield return instruction; //WidgetRow.Icon
        i += 2; //Skip Pop
        instruction = instructionList[i];
        yield return new CodeInstruction(opcode: OpCodes.Ldarg_2);
        yield return new CodeInstruction(opcode: OpCodes.Call,
          operand: AccessTools.Method(typeof(Patch_Misc), nameof(ChangeAreaColor)));
      }
      yield return instruction;
    }
  }

  private static void ChangeAreaColor(Rect rect, Area area)
  {
    if (area is Area_Allowed && Widgets.ButtonInvisible(rect))
    {
      Find.WindowStack.Add(new Dialog_ColorWheel(area.Color, delegate(Color color)
      {
        AccessTools.Field(typeof(Area_Allowed), "colorInt").SetValue(area, color);
        AccessTools.Field(typeof(Area), "colorTextureInt").SetValue(area, null);
        AccessTools.Field(typeof(Area), "drawer").SetValue(area, null);
      }));
    }
  }

  private static void RecheckVehicleHandlerCapacities(Pawn ___pawn)
  {
    if (___pawn.GetVehicle() is { } vehicle)
    {
      //Null check for initial pawn capacities dirty caching when VehiclePawn has not yet called SpawnSetup
      vehicle.EventRegistry?[VehicleEventDefOf.PawnCapacitiesDirty].ExecuteEvents();
    }
  }

  private static void MoveOnDeath(Pawn __instance)
  {
    if (__instance.IsInVehicle())
    {
      VehiclePawn vehicle = __instance.GetVehicle();
      vehicle.AddOrTransfer(__instance);
      if (Find.World.worldPawns.Contains(__instance))
      {
        Find.WorldPawns.RemovePawn(__instance);
      }
      vehicle.EventRegistry[VehicleEventDefOf.PawnKilled].ExecuteEvents();
    }
  }

  private static void SendNotificationsVehicle(Pawn p, ref bool __result)
  {
    if (!__result && p.Faction is { IsPlayer: true } && p is
      { ParentHolder: VehicleRoleHandler or Pawn_InventoryTracker { pawn: VehiclePawn } })
    {
      __result = true;
    }
  }
}