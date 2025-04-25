using Verse;

namespace Vehicles;

internal class GeneratorVehiclePawnKindDef : IVehicleDefGenerator<PawnKindDef>
{
  bool IVehicleDefGenerator<PawnKindDef>.TryGenerateImpliedDef(VehicleDef vehicleDef,
    out PawnKindDef kindDef,
    bool hotReload)
  {
    kindDef = vehicleDef.kindDef;
    if (kindDef == null)
    {
      string defName = vehicleDef.defName + "_PawnKind";
      kindDef = !hotReload ?
        new PawnKindDef() :
        DefDatabase<PawnKindDef>.GetNamed(defName, false) ?? new PawnKindDef();
      kindDef.defName = defName;
      kindDef.label = vehicleDef.label;
      kindDef.description = vehicleDef.description;
      kindDef.combatPower = vehicleDef.combatPower;
      kindDef.race = vehicleDef;
      kindDef.ignoresPainShock = true;
      kindDef.lifeStages = [new PawnKindLifeStage() { bodyGraphicData = vehicleDef.graphicData }];
      kindDef.canBeSapper = true;
      vehicleDef.kindDef = kindDef;
      return true;
    }

    return false;
  }
}