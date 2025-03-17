using System.Collections.Generic;
using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestMaterialPoolDefs : UnitTest
  {
    public override string Name => "MaterialPool_Defs";

    public override TestType ExecuteOn => TestType.MainMenu;

    public override ExecutionPriority Priority => ExecutionPriority.First;

    public override IEnumerable<UTResult> Execute()
    {
      int count = 0;
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        yield return TestVehicleDef(vehicleDef, ref count);
      }

      foreach (PatternDef patternDef in DefDatabase<PatternDef>.AllDefsListForReading)
      {
        yield return TestPattern(patternDef, ref count);
      }

      yield return UTResult.For("MaterialPool (Total Count)",
        count == RGBMaterialPool.TotalMaterials);
    }

    private UTResult TestVehicleDef(VehicleDef vehicleDef, ref int count)
    {
      UTResult result = new();
      if (vehicleDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
      {
        count += vehicleDef.MaterialCount;
        result.Add($"MaterialPool_{vehicleDef} (Cached)", RGBMaterialPool.TargetCached(vehicleDef));
        result.Add($"MaterialPool_{vehicleDef} (Generated)",
          RGBMaterialPool.GetAll(vehicleDef)?.Length == vehicleDef.MaterialCount);
      }

      // Overlays
      if (vehicleDef.drawProperties != null && !vehicleDef.drawProperties.overlays.NullOrEmpty())
      {
        foreach (GraphicOverlay overlay in vehicleDef.drawProperties.overlays)
        {
          if (overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
          {
            count += overlay.MaterialCount;
            result.Add($"MaterialPool_{overlay.Name} (Cached)",
              RGBMaterialPool.TargetCached(overlay));
            result.Add($"MaterialPool_{overlay.Name} (Generated)",
              RGBMaterialPool.GetAll(overlay)?.Length == overlay.MaterialCount);
          }
        }
      }

      // Turrets
      if (vehicleDef.GetCompProperties<CompProperties_VehicleTurrets>() is
          CompProperties_VehicleTurrets compTurrets && !compTurrets.turrets.NullOrEmpty())
      {
        foreach (VehicleTurret turret in compTurrets.turrets)
        {
          if (!turret.NoGraphic &&
            turret.turretDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
          {
            count += turret.MaterialCount;

            result.Add($"MaterialPool_{turret.Name} (Cached)",
              RGBMaterialPool.TargetCached(turret));
            result.Add($"MaterialPool_{turret.Name} (Generated)",
              RGBMaterialPool.GetAll(turret)?.Length == turret.MaterialCount);
          }

          if (!turret.TurretGraphics.NullOrEmpty())
          {
            foreach (VehicleTurret.TurretDrawData drawData in turret.TurretGraphics)
            {
              if (drawData.graphicData.shaderType.Shader.SupportsRGBMaskTex())
              {
                count += drawData.MaterialCount;

                result.Add($"MaterialPool_{drawData.Name} (Cached)",
                  RGBMaterialPool.TargetCached(drawData));
                result.Add($"MaterialPool_{drawData.Name} (Generated)",
                  RGBMaterialPool.GetAll(drawData)?.Length == drawData.MaterialCount);
              }
            }
          }
        }
      }

      // Upgrades
      if (vehicleDef.GetCompProperties<CompProperties_UpgradeTree>() is
          CompProperties_UpgradeTree compUpgrade && compUpgrade.def != null &&
        !compUpgrade.def.nodes.NullOrEmpty())
      {
        foreach (UpgradeNode node in compUpgrade.def.nodes)
        {
          List<GraphicOverlay> overlays = compUpgrade.TryGetOverlays(node);
          if (!overlays.NullOrEmpty())
          {
            foreach (GraphicOverlay overlay in overlays)
            {
              if (overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
              {
                count += overlay.MaterialCount;
                result.Add($"MaterialPool_{node.label}_{overlay.Name} (Cached)",
                  RGBMaterialPool.TargetCached(overlay));
                result.Add($"MaterialPool_{node.label}_{overlay.Name} (Generated)",
                  RGBMaterialPool.GetAll(overlay)?.Length == overlay.MaterialCount);
              }
            }
          }
        }
      }

      return result;
    }

    private UTResult TestPattern(PatternDef patternDef, ref int count)
    {
      UTResult result = new();
      count += patternDef.MaterialCount;
      result.Add($"MaterialPool_{patternDef} (Cached)", RGBMaterialPool.TargetCached(patternDef));
      result.Add($"MaterialPool_{patternDef} (Generated)",
        RGBMaterialPool.GetAll(patternDef)?.Length == patternDef.MaterialCount);

      return result;
    }
  }
}