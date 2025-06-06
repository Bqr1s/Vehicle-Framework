using System;
using RimWorld;
using Verse;

namespace Vehicles;

internal class GeneratorVehicleBuildDef : IVehicleDefGenerator<VehicleBuildDef>
{
  // They removed the DefOf for this, and I can't be bothered to add it myself.
  // We're just doing a def lookup for a default designation category.
  private const string DefaultDesignationCategoryDefName = "Structure";

  bool IVehicleDefGenerator<VehicleBuildDef>.TryGenerateImpliedDef(VehicleDef vehicleDef,
    out VehicleBuildDef impliedBuildDef, bool hotReload)
  {
    impliedBuildDef = null;
    if (vehicleDef.buildDef is not null)
      return false;

    Log.Warning(
      $"[{vehicleDef}] Implied generation for vehicles is incomplete. Please define the VehicleBuildDef separately to avoid improper vehicle generation.");
    string defName = $"{vehicleDef.defName}_Blueprint";
    impliedBuildDef = !hotReload ?
      new VehicleBuildDef() :
      DefDatabase<VehicleBuildDef>.GetNamed(defName, false) ??
      new VehicleBuildDef();
    impliedBuildDef.defName = defName;
    impliedBuildDef.label = vehicleDef.label;
    impliedBuildDef.description = vehicleDef.description;
    impliedBuildDef.modContentPack = vehicleDef.modContentPack;

    impliedBuildDef.thingClass = typeof(VehicleBuilding);
    impliedBuildDef.thingToSpawn = vehicleDef;
    impliedBuildDef.selectable = vehicleDef.selectable;
    impliedBuildDef.altitudeLayer = vehicleDef.altitudeLayer;
    impliedBuildDef.terrainAffordanceNeeded = vehicleDef.terrainAffordanceNeeded;
    impliedBuildDef.constructEffect =
      vehicleDef.constructEffect ?? EffecterDefOf.ConstructMetal;
    impliedBuildDef.leaveResourcesWhenKilled = vehicleDef.leaveResourcesWhenKilled;
    impliedBuildDef.passability = vehicleDef.passability;
    impliedBuildDef.fillPercent = vehicleDef.fillPercent;
    impliedBuildDef.neverMultiSelect = true;
    impliedBuildDef.designationCategory = vehicleDef.designationCategory ??
      DefDatabase<DesignationCategoryDef>.GetNamed(
        DefaultDesignationCategoryDefName);
    impliedBuildDef.clearBuildingArea = true;
    impliedBuildDef.category = ThingCategory.Building;
    impliedBuildDef.blockWind = vehicleDef.blockWind;
    impliedBuildDef.useHitPoints = true;

    impliedBuildDef.rotatable = vehicleDef.rotatable;
    impliedBuildDef.statBases = vehicleDef.statBases;
    impliedBuildDef.size = vehicleDef.size;
    impliedBuildDef.researchPrerequisites = vehicleDef.researchPrerequisites;
    impliedBuildDef.costList = vehicleDef.costList;

    impliedBuildDef.soundImpactDefault = vehicleDef.soundImpactDefault;
    impliedBuildDef.soundBuilt = vehicleDef.soundBuilt;

    impliedBuildDef.graphicData = new GraphicData();

    impliedBuildDef.building = vehicleDef.building ?? new BuildingProperties()
    {
      canPlaceOverImpassablePlant = false,
      paintable = false
    };

    // Purge designation category from non-buildable VehiclePawn
    vehicleDef.designationCategory = null;
    impliedBuildDef.graphicData.CopyFrom(vehicleDef.graphicData);
    Type graphicClass = vehicleDef.graphicData.drawRotated ?
      typeof(Graphic_Multi) :
      typeof(Graphic_Single);
    impliedBuildDef.graphicData.graphicClass = graphicClass;
    vehicleDef.buildDef = impliedBuildDef;
    return true;
  }
}