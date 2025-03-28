using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.Sound;
using RimWorld;
using SmashTools;

namespace Vehicles
{
  internal class Construction : IPatchCategory
  {
    public void PatchMethods()
    {
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
          parameters: [typeof(PawnGenerationRequest)]),
        prefix: new HarmonyMethod(typeof(Construction),
          nameof(GenerateVehiclePawn)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(Frame), nameof(Frame.CompleteConstruction)),
        prefix: new HarmonyMethod(typeof(Construction),
          nameof(CompleteConstructionVehicle)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(ListerBuildingsRepairable),
          nameof(ListerBuildingsRepairable.Notify_BuildingRepaired)),
        prefix: new HarmonyMethod(typeof(Construction),
          nameof(Notify_RepairedVehicle)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(GenSpawn), name: nameof(GenSpawn.Spawn),
        [
          typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool),
          typeof(bool)
        ]),
        prefix: new HarmonyMethod(typeof(Construction),
          nameof(RegisterThingSpawned)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(Designator_Deconstruct),
          nameof(Designator.CanDesignateThing)),
        postfix: new HarmonyMethod(typeof(Construction),
          nameof(AllowDeconstructVehicle)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(GenLeaving), nameof(GenLeaving.DoLeavingsFor),
          [typeof(Thing), typeof(Map), typeof(DestroyMode), typeof(List<Thing>)]),
        prefix: new HarmonyMethod(typeof(Construction),
          nameof(DoUnsupportedVehicleRefunds)));
      VehicleHarmony.Patch(original: AccessTools.Method(typeof(Pawn), nameof(Pawn.Destroy)),
        transpiler: new HarmonyMethod(typeof(Construction),
          nameof(ValidDestroyModeForVehicles)));
    }

    private static bool GenerateVehiclePawn(PawnGenerationRequest request, ref Pawn __result)
    {
      if (request.KindDef != null && request.KindDef.race is VehicleDef vehicleDef)
      {
        __result = VehicleSpawner.GenerateVehicle(vehicleDef, request.Faction);
        return false;
      }

      return true;
    }

    public static bool CompleteConstructionVehicle(Pawn worker, Frame __instance)
    {
      if (__instance.def.entityDefToBuild is VehicleBuildDef def && def.thingToSpawn != null)
      {
        VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(def.thingToSpawn, worker.Faction);
        __instance.resourceContainer.ClearAndDestroyContents(DestroyMode.Vanish);
        Map map = __instance.Map;
        __instance.Destroy(DestroyMode.Vanish);

        if (def.soundBuilt != null)
        {
          def.soundBuilt.PlayOneShot(new TargetInfo(__instance.Position, map, false));
        }

        vehicle.SetFaction(worker.Faction);
        GenSpawn.Spawn(vehicle, __instance.Position, map, __instance.Rotation, WipeMode.FullRefund,
          false);
        worker.records.Increment(RecordDefOf.ThingsConstructed);

        if (!DebugSettings.godMode) //quick spawning for development
        {
          vehicle.Rename();
        }
        else
        {
          foreach (VehicleComp vehicleComp in vehicle.AllComps.Where(comp => comp is VehicleComp))
          {
            vehicleComp.SpawnedInGodMode();
          }
        }

        //Quality?
        //Art?
        //Tale RecordTale LongConstructionProject?
        return false;
      }

      return true;
    }

    public static bool Notify_RepairedVehicle(Building b, ListerBuildingsRepairable __instance)
    {
      if (b is VehicleBuilding building && b.def is VehicleBuildDef vehicleDef &&
        vehicleDef.thingToSpawn != null)
      {
        if (b.HitPoints < b.MaxHitPoints)
          return true;

        Pawn vehicle;
        if (building.vehicle != null)
        {
          vehicle = building.vehicle;
          vehicle.health.Reset();
        }
        else
        {
          vehicle = PawnGenerator.GeneratePawn(vehicleDef.thingToSpawn.kindDef);
        }

        Map map = b.Map;
        IntVec3 position = b.Position;
        Rot4 rotation = b.Rotation;

        AccessTools.Method(typeof(ListerBuildingsRepairable), "UpdateBuilding")
         .Invoke(__instance, new object[] { b });
        if (vehicleDef.soundBuilt != null)
        {
          vehicleDef.soundBuilt.PlayOneShot(new TargetInfo(position, map, false));
        }

        if (vehicle.Faction != Faction.OfPlayer)
        {
          vehicle.SetFaction(Faction.OfPlayer);
        }

        b.Destroy(DestroyMode.Vanish);
        vehicle.ForceSetStateToUnspawned();
        GenSpawn.Spawn(vehicle, position, map, rotation, WipeMode.FullRefund, false);
        return false;
      }

      return true;
    }

    /// <summary>
    /// Catch All for vehicle related Things spawned in. Handles GodMode placing of vehicle buildings, corrects immovable spawn locations, and registers air defenses
    /// </summary>
    /// <param name="newThing"></param>
    /// <param name="loc"></param>
    /// <param name="map"></param>
    /// <param name="rot"></param>
    /// <param name="__result"></param>
    /// <param name="wipeMode"></param>
    /// <param name="respawningAfterLoad"></param>
    public static bool RegisterThingSpawned(Thing newThing, ref IntVec3 loc, Map map, ref Rot4 rot,
      ref Thing __result, bool respawningAfterLoad)
    {
      if (newThing.def is VehicleBuildDef buildDef &&
        !VehicleMod.settings.debug.debugSpawnVehicleBuildingGodMode &&
        newThing.HitPoints == newThing.MaxHitPoints && !respawningAfterLoad)
      {
        return BuildVehicle(newThing, buildDef, map, ref rot, ref loc, ref __result);
      }

      if (newThing is VehiclePawn vehicle)
      {
        __result = vehicle;
        return PlaceVehicle(vehicle, map, ref rot, ref loc, respawningAfterLoad);
      }

      if (newThing is Pawn { Dead: false } pawn)
      {
        TryAdjustPawn(pawn, map, ref loc);
      }

      return true;

      static bool BuildVehicle(Thing newThing, VehicleBuildDef buildDef, Map map, ref Rot4 rot,
        ref IntVec3 loc, ref Thing __result)
      {
        VehiclePawn vehicle =
          VehicleSpawner.GenerateVehicle(buildDef.thingToSpawn, newThing.Faction);

        buildDef.soundBuilt?.PlayOneShot(new TargetInfo(loc, map));

        // NOTE - Vehicle will go back through this patch for placement where it will then have
        // its placement adjusted. We don't need to do anything about this loc here.
        GenSpawn.Spawn(vehicle, loc, map, rot, WipeMode.FullRefund);

        if (!DebugSettings.godMode)
        {
          // Only prompt name when not spawned via godmode. This is really annoying to deal with
          // when debugging. The prompt just gets all up in your face, demanding a response.
          vehicle.Rename();
        }
        else
        {
          foreach (ThingComp thingComp in vehicle.AllComps)
          {
            if (thingComp is VehicleComp vehicleComp)
              vehicleComp.SpawnedInGodMode();
          }
        }

        __result = vehicle;
        return false;
      }

      static bool PlaceVehicle(VehiclePawn vehicle, Map map, ref Rot4 rot, ref IntVec3 loc,
        bool respawningAfterLoad)
      {
        if (!vehicle.VehicleDef.rotatable)
        {
          rot = vehicle.VehicleDef.defaultPlacingRot;
        }

        VehiclePositionManager positionManager =
          map.GetCachedMapComponent<VehiclePositionManager>();
        bool standable = true;
        foreach (IntVec3 cell in vehicle.PawnOccupiedCells(loc, rot))
        {
          if (!cell.InBounds(map) || !cell.Walkable(vehicle.VehicleDef, map) ||
            positionManager.PositionClaimed(cell))
          {
            standable = false;
            break;
          }
        }

        if (standable)
        {
          if (!respawningAfterLoad)
            FinalizePosition(vehicle, rot, ref loc);
          Debug.Message($"Spawning {vehicle} at {loc} Rotation={rot}");
          return true; // If location is still valid, skip to spawning
        }

        Rot4 lambdaRot = rot;
        if (!CellFinderExtended.TryRadialSearchForCell(loc, map, 30, (cell) =>
          {
            foreach (IntVec3 occupiedCell in vehicle.PawnOccupiedCells(cell, lambdaRot))
            {
              if (!occupiedCell.InBounds(map) ||
                !occupiedCell.Walkable(vehicle.VehicleDef, map) ||
                positionManager.PositionClaimed(occupiedCell))
              {
                return false;
              }
            }

            Debug.Message($"Adjusting {vehicle} to {cell} Rotation={lambdaRot}");
            return true;
          }, out IntVec3 newLoc))
        {
          // Just get the vehicle spawned in, user will need to dev-mode teleport them once loaded.
          // This is easier to handle than lost vehicles needing to be recovered from world pawns.
          Log.Error(
            $"Unable to find location to spawn {vehicle.LabelShort}. Performing wider search.");
          if (!CellFinderExtended.TryRadialSearchForCell(loc, map, 100, (cell) =>
            {
              foreach (IntVec3 occupiedCell in vehicle.PawnOccupiedCells(cell, lambdaRot))
              {
                if (!occupiedCell.InBounds(map))
                {
                  return false;
                }
              }

              return true;
            }, out newLoc))
          {
            Log.Error($"Unable to find location to spawn {vehicle.LabelShort}. Aborting spawn.");
            return false;
          }
        }

        loc = newLoc;
        if (!respawningAfterLoad)
          FinalizePosition(vehicle, rot, ref loc);
        return true;
      }

      static void TryAdjustPawn(Pawn pawn, Map map, ref IntVec3 loc)
      {
        try
        {
          VehiclePositionManager positionManager =
            map.GetCachedMapComponent<VehiclePositionManager>();
          if (positionManager.PositionClaimed(loc))
          {
            VehiclePawn inPlaceVehicle = positionManager.ClaimedBy(loc);
            CellRect occupiedRect = inPlaceVehicle.OccupiedRect().ExpandedBy(1);
            Rand.PushState();
            for (int i = 0; i < 3; i++)
            {
              IntVec3 newLoc = occupiedRect.EdgeCells.Where(c => c.InBounds(map) &&
                c.Standable(map)).RandomElementWithFallback(inPlaceVehicle.Position);
              if (occupiedRect.EdgeCells.Contains(newLoc))
              {
                loc = newLoc;
                break;
              }

              occupiedRect = occupiedRect.ExpandedBy(1);
            }

            Rand.PopState();
          }
        }
        catch (Exception ex)
        {
          Log.Error(
            $"Pawn {pawn.Label} could not be readjusted for spawn location.\nException={ex}");
        }
      }

      // There is a discrepancy between Thing true centers and Vehicle true centers. If the vehicle
      // is even width or even height, it will shift when transitioning between the placement of
      // the building and the spawning of the vehicle. Adjusting the position unconditionally will
      // avoid map edge issues where the vehicle jumps 1 cell off the map and despawns from
      // registration issues.
      static void FinalizePosition(VehiclePawn vehicle, Rot4 rot, ref IntVec3 cell)
      {
        switch (rot.AsInt)
        {
          // This is only a problem with south and west facing entities due to the way
          // RimWorld handles even-size rotations.
          case 2:
            if (vehicle.VehicleDef.Size.x % 2 == 0)
              cell.x -= 1;
            if (vehicle.VehicleDef.Size.z % 2 == 0)
              cell.z -= 1;
            break;
          case 3:
            if (vehicle.VehicleDef.Size.x % 2 == 0)
              cell.z += 1;
            if (vehicle.VehicleDef.Size.z % 2 == 0)
              cell.x -= 1;
            break;
        }
      }
    }

    public static void AllowDeconstructVehicle(Designator_Deconstruct __instance, Thing t,
      ref AcceptanceReport __result)
    {
      if (t is VehiclePawn vehicle && vehicle.DeconstructibleBy(Faction.OfPlayer))
      {
        if (__instance.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) !=
          null)
        {
          __result = false;
        }
        else if (__instance.Map.designationManager.DesignationOn(t, DesignationDefOf.Uninstall) !=
          null)
        {
          __result = false;
        }
        else
        {
          __result = true;
        }
      }
    }

    public static bool DoUnsupportedVehicleRefunds(Thing diedThing, Map map, DestroyMode mode,
      List<Thing> listOfLeavingsOut = null)
    {
      if (diedThing is VehiclePawn vehicle)
      {
        vehicle.RefundMaterials(map, mode, listOfLeavingsOut);
        return false;
      }

      return true;
    }

    public static IEnumerable<CodeInstruction> ValidDestroyModeForVehicles(
      IEnumerable<CodeInstruction> instructions)
    {
      List<CodeInstruction> instructionList = instructions.ToList();

      for (int i = 0; i < instructionList.Count; i++)
      {
        CodeInstruction instruction = instructionList[i];

        if ((instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S) &&
          !instructionList.OutOfBounds(i - 1) && instructionList[i - 1].opcode == OpCodes.Ldarg_1)
        {
          List<Label> labels = instruction.labels;
          yield return instruction;
          instruction = instructionList[++i];

          yield return new CodeInstruction(opcode: OpCodes.Ldarg_0);
          yield return new CodeInstruction(opcode: OpCodes.Ldarg_1);
          yield return new CodeInstruction(opcode: OpCodes.Call, operand:
            AccessTools.Method(typeof(Construction), nameof(VehicleValidDestroyMode)));
          yield return new CodeInstruction(opcode: OpCodes.Brtrue,
            operand: labels.FirstOrDefault());
        }

        yield return instruction;
      }
    }

    public static bool VehicleValidDestroyMode(Pawn pawn, DestroyMode destroyMode)
    {
      return pawn is VehiclePawn && destroyMode != DestroyMode.QuestLogic &&
        destroyMode != DestroyMode.FailConstruction && destroyMode != DestroyMode.WillReplace;
    }
  }
}