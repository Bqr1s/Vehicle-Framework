﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using RimWorld;
using SmashTools;
using SmashTools.Patching;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles;

internal class Patch_VehiclePathing : IPatchCategory
{
  private static readonly List<VehiclePawn> multiSelectGotoList = [];
  private static readonly HashSet<IntVec3> hitboxUpdateCells = [];

  PatchSequence IPatchCategory.PatchAt => PatchSequence.Mod;

  void IPatchCategory.PatchMethods()
  {
    // TODO 1.6 Beta / Release - Check if this is still necessary. Devs said they would make this virtual
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(FloatMenuOptionProvider),
        nameof(FloatMenuOptionProvider.SelectedPawnValid)),
      postfix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(VehiclesNotValidForNormalCommands)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(FloatMenuOptionProvider_DraftedMove), "PawnCanGoto"),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MultiselectVehicleGotoBlocked)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(Selector), "HandleMultiselectGoto"),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MultiselectGotoDraggingBlocked)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.IsCurrentJobPlayerInterruptible)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(JobInterruptibleForVehicle)));
    HarmonyPatcher.Patch(original: AccessTools.Method(typeof(Pawn_PathFollower), "NeedNewPath"),
      postfix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(IsVehicleInNextCell)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(Pawn_PathFollower),
        nameof(Pawn_PathFollower.StartPath)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(StartVehiclePath)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(GenAdj), nameof(GenAdj.AdjacentTo8WayOrInside),
        parameters: [typeof(IntVec3), typeof(Thing)]),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(AdjacentTo8WayOrInsideVehicle)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect),
        parameters: [typeof(Thing)]),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(OccupiedRectVehicles)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(Pathing),
        nameof(Pathing.RecalculatePerceivedPathCostAt)),
      postfix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(RecalculatePerceivedPathCostForVehicle)));

    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(TerrainGrid), "DoTerrainChangedEffects"),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(SetTerrainAndUpdateVehiclePathCosts)));
    HarmonyPatcher.Patch(original: AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)),
      transpiler: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(DeSpawnAndUpdateVehicleRegionsTranspiler)));
    HarmonyPatcher.Patch(original: AccessTools.Method(typeof(Thing), nameof(Thing.SpawnSetup)),
      transpiler: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(SpawnAndUpdateVehicleRegionsTranspiler)));
    HarmonyPatcher.Patch(
      original: AccessTools.PropertySetter(typeof(Thing), nameof(Thing.Position)),
      postfix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(SetPositionAndUpdateVehicleRegions)));
    HarmonyPatcher.Patch(
      original: AccessTools.PropertySetter(typeof(Thing), nameof(Thing.Rotation)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(SetRotationAndUpdateVehicleRegionsClipping)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.Register)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MonitorThingGridRegisterStart)),
      finalizer: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MonitorThingGridRegisterEnd)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.Deregister)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MonitorThingGridDeregisterStart)),
      finalizer: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(MonitorThingGridDeregisterEnd)));

    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(GenStep_RocksFromGrid),
        nameof(GenStep_RocksFromGrid.Generate)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(DisableRegionUpdatingRockGen)));
    HarmonyPatcher.Patch(
      original: AccessTools.Method(typeof(GenStep_RocksNearEdge),
        nameof(GenStep_RocksNearEdge.Generate)),
      prefix: new HarmonyMethod(typeof(Patch_VehiclePathing),
        nameof(DisableRegionUpdatingRockGen)));
  }

  private static void VehiclesNotValidForNormalCommands(ref bool __result,
    FloatMenuOptionProvider __instance,
    Pawn pawn, FloatMenuContext context)
  {
    if (__result && pawn is VehiclePawn vehicle)
    {
      __result = false;
      if (__instance is FloatMenuOptionProvider_Vehicle vehicleProvider)
      {
        __result = vehicleProvider.SelectedPawnValid(vehicle, context);
      }
    }
  }

  private static bool MultiselectVehicleGotoBlocked(Pawn pawn, ref AcceptanceReport __result)
  {
    if (pawn is VehiclePawn)
    {
      __result = false;
      return false;
    }
    return true;
  }

  private static bool MultiselectGotoDraggingBlocked(FloatMenuContext context)
  {
    if (context.IsMultiselect)
    {
      if (context.allSelectedPawns.All(pawn => pawn is VehiclePawn))
      {
        Assert.AreEqual(multiSelectGotoList.Count, 0);
        multiSelectGotoList.AddRange(context.allSelectedPawns.Cast<VehiclePawn>());
        if (!PathingHelper.TryFindNearestStandableCell(multiSelectGotoList.FirstOrDefault(),
          context.ClickedCell, out IntVec3 result))
        {
          return false;
        }
        VehicleOrientationController.StartOrienting(multiSelectGotoList, result,
          context.ClickedCell);
        multiSelectGotoList.Clear();
        return false;
      }
      // Remove any vehicles if not all are vehicles, preventing vanilla assigned position goto's
      for (int i = context.allSelectedPawns.Count - 1; i >= 0; i--)
      {
        Pawn pawn = context.allSelectedPawns[i];
        if (pawn is VehiclePawn)
          context.allSelectedPawns.RemoveAt(i);
      }
    }
    return true;
  }

  /// <summary>
  /// Bypass vanilla check for now, since it forces on-fire pawns to not be able to interrupt jobs which obviously shouldn't apply to vehicles.
  /// </summary>
  private static bool JobInterruptibleForVehicle(Pawn_JobTracker __instance, Pawn ___pawn,
    ref bool __result)
  {
    if (___pawn is VehiclePawn)
    {
      __result = true;
      if (__instance.curJob != null)
      {
        if (!__instance.curJob.def.playerInterruptible)
        {
          __result = false;
        }
        else if (__instance.curDriver != null && !__instance.curDriver.PlayerInterruptable)
        {
          __result = false;
        }
      }

      return false;
    }

    return true;
  }

  /// <summary>
  /// Determine if next cell is walkable with final determination if vehicle is in cell or not
  /// </summary>
  private static void IsVehicleInNextCell(ref bool __result, Pawn ___pawn,
    Pawn_PathFollower __instance)
  {
    if (!__result)
    {
      //Peek 2 nodes ahead to avoid collision last second
      __result = (__instance.curPath.NodesLeftCount > 1 &&
          PathingHelper.VehicleImpassableInCell(___pawn.Map, __instance.curPath.Peek(1))) ||
        (__instance.curPath.NodesLeftCount > 2 &&
          PathingHelper.VehicleImpassableInCell(___pawn.Map, __instance.curPath.Peek(2)));
    }
  }

  /// <summary>
  /// StartPath hook to divert to vehicle related pather
  /// </summary>
  /// <param name="dest"></param>
  /// <param name="peMode"></param>
  /// <param name="___pawn"></param>
  private static bool StartVehiclePath(LocalTargetInfo dest, PathEndMode peMode, Pawn ___pawn)
  {
    if (___pawn is VehiclePawn vehicle)
    {
      vehicle.vehiclePather.StartPath(dest, peMode);
      return false;
    }

    return true;
  }

  private static bool AdjacentTo8WayOrInsideVehicle(IntVec3 root, Thing t, ref bool __result)
  {
    if (t is VehiclePawn vehicle)
    {
      IntVec2 size = vehicle.def.size;
      Rot4 rot = vehicle.Rotation;
      Ext_Vehicles.AdjustForVehicleOccupiedRect(ref size, ref rot);
      __result = root.AdjacentTo8WayOrInside(vehicle.Position, rot, size);
      return false;
    }

    return true;
  }

  /// <summary>
  /// Set cells in which vehicles reside as impassable to other Pawns
  /// </summary>
  /// <param name="instructions"></param>
  /// <param name="ilg"></param>
  private static IEnumerable<CodeInstruction> PathAroundVehicles(
    IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
  {
    List<CodeInstruction> instructionList = instructions.ToList();
    for (int i = 0; i < instructionList.Count; i++)
    {
      CodeInstruction instruction = instructionList[i];
      if (instruction.Calls(AccessTools.Method(typeof(CellIndices),
        nameof(CellIndices.CellToIndex), new Type[] { typeof(int), typeof(int) })))
      {
        Label label = ilg.DefineLabel();
        Label vehicleLabel = ilg.DefineLabel();

        yield return instruction; //CALLVIRT CELLTOINDEX
        instruction = instructionList[++i];
        yield return instruction; //STLOC.S 43
        instruction = instructionList[++i];

        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldfld,
          operand: AccessTools.Field(typeof(PathFinder), "map"));
        yield return new CodeInstruction(opcode: OpCodes.Ldloc_S, 41);
        yield return new CodeInstruction(opcode: OpCodes.Ldloc_S, 42);
        yield return new CodeInstruction(opcode: OpCodes.Call,
          operand: AccessTools.Method(typeof(PathingHelper),
            nameof(PathingHelper.VehicleImpassableInCell),
            new Type[] { typeof(Map), typeof(int), typeof(int) }));

        yield return new CodeInstruction(opcode: OpCodes.Brfalse, label);
        yield return new CodeInstruction(opcode: OpCodes.Ldc_I4_0);
        yield return new CodeInstruction(opcode: OpCodes.Br, vehicleLabel);

        for (int j = i; j < instructionList.Count; j++)
        {
          CodeInstruction instruction2 = instructionList[j];
          if (instruction2.opcode == OpCodes.Brfalse || instruction2.opcode == OpCodes.Brfalse_S)
          {
            instruction2.labels.Add(vehicleLabel);
            break;
          }
        }

        instruction.labels.Add(label);
      }

      yield return instruction;
    }
  }

  /// <summary>
  /// Modify CanReach result if position is claimed by Vehicle in PositionManager
  /// </summary>
  /// <param name="start"></param>
  /// <param name="dest"></param>
  /// <param name="peMode"></param>
  /// <param name="traverseParams"></param>
  /// <param name="__result"></param>
  private static bool CanReachVehiclePosition(IntVec3 start, LocalTargetInfo dest,
    PathEndMode peMode, TraverseParms traverseParams, ref bool __result)
  {
    if (peMode == PathEndMode.OnCell && !(traverseParams.pawn is not null) &&
      traverseParams.pawn?.Map.GetDetachedMapComponent<VehiclePositionManager>()
       .ClaimedBy(dest.Cell) is VehiclePawn vehicle &&
      vehicle.VehicleDef.passability != Traversability.Standable)
    {
      __result = false;
      return false;
    }

    return true;
  }

  private static void ImpassableThroughVehicle(IntVec3 c, Map map, ref bool __result)
  {
    if (!__result && !PathingHelper.regionAndRoomUpdaterWorking(map.regionAndRoomUpdater))
    {
      __result = PathingHelper.VehicleImpassableInCell(map, c);
    }
  }

  private static void WalkableThroughVehicle(IntVec3 loc, ref bool __result, Map ___map)
  {
    if (__result && !PathingHelper.regionAndRoomUpdaterWorking(___map.regionAndRoomUpdater))
    {
      __result = !PathingHelper.VehicleImpassableInCell(___map, loc);
    }
  }

  private static void WalkableFastThroughVehicleIntVec3(IntVec3 loc, ref bool __result,
    Map ___map)
  {
    if (__result && !PathingHelper.regionAndRoomUpdaterWorking(___map.regionAndRoomUpdater))
    {
      __result = !PathingHelper.VehicleImpassableInCell(___map, loc);
    }
  }

  private static void WalkableFastThroughVehicleInt2(int x, int z, ref bool __result, Map ___map)
  {
    if (__result && !PathingHelper.regionAndRoomUpdaterWorking(___map.regionAndRoomUpdater))
    {
      __result = !PathingHelper.VehicleImpassableInCell(___map, new IntVec3(x, 0, z));
    }
  }

  private static void WalkableFastThroughVehicleInt(int index, ref bool __result, Map ___map)
  {
    if (__result && !PathingHelper.regionAndRoomUpdaterWorking(___map.regionAndRoomUpdater))
    {
      __result =
        !PathingHelper.VehicleImpassableInCell(___map, ___map.cellIndices.IndexToCell(index));
    }
  }

  private static bool OccupiedRectVehicles(Thing t, ref CellRect __result)
  {
    if (t is VehiclePawn vehicle)
    {
      __result = vehicle.VehicleRect();
      return false;
    }

    return true;
  }

  private static void RecalculatePerceivedPathCostForVehicle(IntVec3 c, PathingContext ___normal)
  {
    PathingHelper.RecalculatePerceivedPathCostAt(c, ___normal.map);
  }

  /// <summary>
  /// Pass <paramref name="c"/> by reference to allow Harmony to skip prefix method when MapPreview skips it during preview generation
  /// </summary>
  /// <param name="c"></param>
  /// <param name="___map"></param>
  private static void SetTerrainAndUpdateVehiclePathCosts(ref IntVec3 c, Map ___map)
  {
    if (Current.ProgramState == ProgramState.Playing)
    {
      PathingHelper.RecalculatePerceivedPathCostAt(c, ___map);
    }
  }

  private static IEnumerable<CodeInstruction> DeSpawnAndUpdateVehicleRegionsTranspiler(
    IEnumerable<CodeInstruction> instructions)
  {
    List<CodeInstruction> instructionList = instructions.ToList();

    MethodInfo coverGridDeregisterMethod = AccessTools.Method(typeof(TickManager),
      nameof(TickManager.DeRegisterAllTickabilityFor));
    for (int i = 0; i < instructionList.Count; i++)
    {
      CodeInstruction instruction = instructionList[i];

      if (instruction.Calls(coverGridDeregisterMethod))
      {
        yield return instruction;
        instruction = instructionList[++i];

        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldloc_0);
        yield return new CodeInstruction(opcode: OpCodes.Call,
          operand: AccessTools.Method(typeof(Patch_VehiclePathing),
            nameof(Patch_VehiclePathing.DeSpawnAndNotifyVehicleRegions)));
      }

      yield return instruction;
    }
  }

  private static IEnumerable<CodeInstruction> SpawnAndUpdateVehicleRegionsTranspiler(
    IEnumerable<CodeInstruction> instructions)
  {
    List<CodeInstruction> instructionList = instructions.ToList();

    MethodInfo coverGridDeregisterMethod =
      AccessTools.Method(typeof(CoverGrid), nameof(CoverGrid.Register));
    for (int i = 0; i < instructionList.Count; i++)
    {
      CodeInstruction instruction = instructionList[i];

      if (instruction.Calls(coverGridDeregisterMethod))
      {
        yield return instruction;
        instruction = instructionList[++i];

        yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
        yield return new CodeInstruction(opcode: OpCodes.Ldarg_1);
        yield return new CodeInstruction(opcode: OpCodes.Call,
          operand: AccessTools.Method(typeof(Patch_VehiclePathing),
            nameof(Patch_VehiclePathing.SpawnAndNotifyVehicleRegions)));
      }

      yield return instruction;
    }
  }

  private static void SetPositionAndUpdateVehicleRegions(Thing __instance, IntVec3 value)
  {
    if (__instance.Spawned)
    {
      if (__instance is VehiclePawn vehicle)
      {
        vehicle.ReclaimPosition();
      }

      PathingHelper.ThingAffectingRegionsOrientationChanged(__instance, __instance.Map);
    }
  }

  private static bool SetRotationAndUpdateVehicleRegionsClipping(Thing __instance, Rot4 value,
    ref Rot4 ___rotationInt)
  {
    if (__instance is VehiclePawn vehicle)
    {
      vehicle.SetRotationInt(value, ref ___rotationInt);
      return false;
    }

    return true;
  }

  private static void SetRotationAndUpdateVehicleRegions(Thing __instance)
  {
    if (__instance.Spawned && (__instance.def.size.x != 1 || __instance.def.size.z != 1))
    {
      PathingHelper.ThingAffectingRegionsOrientationChanged(__instance, __instance.Map);
    }
  }

  private static void MonitorThingGridRegisterStart(ThingGrid __instance)
  {
    Monitor.Enter(__instance);
  }

  private static void MonitorThingGridRegisterEnd(ThingGrid __instance)
  {
    Monitor.Exit(__instance);
  }

  private static void MonitorThingGridDeregisterStart(ThingGrid __instance)
  {
    Monitor.Enter(__instance);
  }

  private static void MonitorThingGridDeregisterEnd(ThingGrid __instance)
  {
    Monitor.Exit(__instance);
  }

  private static void DisableRegionUpdatingRockGen(Map map)
  {
    if (!map.TileInfo.WaterCovered)
    {
      map.GetCachedMapComponent<VehiclePathingSystem>().DisableAllRegionUpdaters();
    }
  }

  /* ---- Helper Methods related to patches ---- */

  private static void SpawnAndNotifyVehicleRegions(Thing thing, Map map)
  {
    PathingHelper.ThingAffectingRegionsStateChange(thing, map, true);
  }

  private static void DeSpawnAndNotifyVehicleRegions(Thing thing, Map map)
  {
    PathingHelper.ThingAffectingRegionsStateChange(thing, map, false);
  }
}