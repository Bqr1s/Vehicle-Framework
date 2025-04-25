﻿using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using SmashTools;

namespace Vehicles
{
  internal class Components : IPatchCategory
  {
    public void PatchMethods()
    {
      VehicleHarmony.Patch(original: AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Drafted)),
        postfix: new HarmonyMethod(typeof(Components),
          nameof(VehicleIsDrafted)));
      VehicleHarmony.Patch(
        original: AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.CanTakeOrder)),
        postfix: new HarmonyMethod(typeof(Components),
          nameof(CanVehicleTakeOrder)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(FloatMenuUtility),
          nameof(FloatMenuUtility.GetMeleeAttackAction)),
        prefix: new HarmonyMethod(typeof(Components),
          nameof(NoMeleeForVehicles)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(PawnComponentsUtility),
          nameof(PawnComponentsUtility.CreateInitialComponents)),
        prefix: new HarmonyMethod(typeof(Components),
          nameof(CreateInitialVehicleComponents)));
      VehicleHarmony.Patch(
        original: AccessTools.Method(typeof(PawnComponentsUtility),
          nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents)),
        prefix: new HarmonyMethod(typeof(Components),
          nameof(AddAndRemoveVehicleComponents)));
      VehicleHarmony.Patch(original: AccessTools.Method(typeof(Pawn_MeleeVerbs), "ChooseMeleeVerb"),
        prefix: new HarmonyMethod(typeof(Components),
          nameof(VehiclesDontMeleeThings)));
    }

    /// <summary>
    /// Divert draft status check to <see cref="VehicleIgnitionController"/>
    /// </summary>
    public static void VehicleIsDrafted(Pawn __instance, ref bool __result)
    {
      if (__instance is VehiclePawn vehicle)
      {
        // May trigger prematurely from PrepareCarefully
        __result = vehicle.ignition?.Drafted ?? false;
      }
    }

    /// <summary>
    /// Allow vehicles to take orders despite them not being categorized as humanlike
    /// </summary>
    public static void CanVehicleTakeOrder(Pawn __instance, ref bool __result)
    {
      if (__result is false)
      {
        __result = __instance is VehiclePawn;
      }
    }

    /// <summary>
    /// Disable melee attacks for vehicles, which don't work anyways due to not having Manipulation capacity and only cause errors
    /// </summary>
    public static bool NoMeleeForVehicles(Pawn pawn, LocalTargetInfo target, out string failStr)
    {
      if (pawn is VehiclePawn)
      {
        failStr = "VF_IsIncapableOfRamming".Translate(target.Thing.LabelShort);
        //Add more to string or Action if ramming is implemented
        return false;
      }
      failStr = string.Empty;
      return true;
    }

    /// <summary>
    /// Initialize vehicle specific components
    /// </summary>
    /// <param name="pawn"></param>
    public static void CreateInitialVehicleComponents(Pawn pawn)
    {
      if (pawn is VehiclePawn vehicle && vehicle.vehiclePather is null)
      {
        vehicle.vehiclePather = new VehiclePathFollower(vehicle);
        vehicle.vehicleAI = new VehicleAI(vehicle);
        vehicle.statHandler = new VehicleStatHandler(vehicle);
        vehicle.sharedJob = new SharedJob();
        vehicle.overlayRenderer = new GraphicOverlayRenderer(vehicle);
        PatternData defaultPatternData =
          VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicle.VehicleDef.defName,
            vehicle.VehicleDef.graphicData);
        vehicle.patternData = new PatternData(defaultPatternData);
        if (vehicle.Stuff != null)
        {
          vehicle.DrawColor = vehicle.VehicleDef.GetColorForStuff(vehicle.Stuff);
        }
      }
    }

    /// <summary>
    /// Ensure that vehicles are given the right components when terminating from the main method
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="actAsIfSpawned"></param>
    public static void AddAndRemoveVehicleComponents(Pawn pawn, bool actAsIfSpawned = false)
    {
      if (pawn is VehiclePawn vehicle && (vehicle.Spawned || actAsIfSpawned) &&
        vehicle.ignition is null)
      {
        vehicle.ignition = new VehicleIgnitionController(vehicle);
        vehicle.trader = null; // new Pawn_TraderTracker(vehicle);
        vehicle.story = new Pawn_StoryTracker(vehicle);
        vehicle.playerSettings = new Pawn_PlayerSettings(vehicle);
        vehicle.training = null;
      }
    }

    /// <summary>
    /// Ensure that vehicles do not perform melee jobs
    /// </summary>
    /// <param name="target"></param>
    /// <param name="___pawn"></param>
    public static bool VehiclesDontMeleeThings(Thing target, Pawn ___pawn)
    {
      if (___pawn is VehiclePawn)
      {
        return false;
      }
      return true;
    }
  }
}