using System.Collections.Generic;
using DevTools.UnitTesting;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.MainMenu)]
  internal class UnitTest_MaterialPoolDefs
  {
    [Test]
    private void VehicleDefs()
    {
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        using Test.Group group = new(vehicleDef.defName);

        if (vehicleDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
        {
          Expect.IsTrue("Target Cached", RGBMaterialPool.TargetCached(vehicleDef));
          Expect.IsTrue("Materials Allocated",
            RGBMaterialPool.GetAll(vehicleDef)?.Length == vehicleDef.MaterialCount);
        }

        // Overlays
        if (vehicleDef.drawProperties != null && !vehicleDef.drawProperties.overlays.NullOrEmpty())
        {
          for (int i = 0; i < vehicleDef.drawProperties.overlays.Count; i++)
          {
            GraphicOverlay overlay = vehicleDef.drawProperties.overlays[i];
            if (!overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
              continue;

            Expect.IsTrue($"Overlay[{i}] Target Cached", RGBMaterialPool.TargetCached(overlay));
            Expect.IsTrue($"Overlay[{i}] Materials Allocated",
              RGBMaterialPool.GetAll(overlay)?.Length == overlay.MaterialCount);
          }
        }

        // Turrets
        CompProperties_VehicleTurrets compTurrets =
          vehicleDef.GetCompProperties<CompProperties_VehicleTurrets>();
        if (compTurrets is not null && !compTurrets.turrets.NullOrEmpty())
        {
          foreach (VehicleTurret turret in compTurrets.turrets)
          {
            if (!turret.NoGraphic &&
              turret.turretDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
            {
              Expect.IsTrue($"{turret.key ?? turret.turretDef.defName} TargetCached",
                RGBMaterialPool.TargetCached(turret));
              Expect.IsTrue($"{turret.key ?? turret.turretDef.defName} Materials Allocated",
                RGBMaterialPool.GetAll(turret)?.Length == turret.MaterialCount);
            }

            if (!turret.TurretGraphics.NullOrEmpty())
            {
              foreach (VehicleTurret.TurretDrawData drawData in turret.TurretGraphics)
              {
                if (drawData.graphicData.shaderType.Shader.SupportsRGBMaskTex())
                {
                  Expect.IsTrue($"{turret.key ?? turret.turretDef.defName} DrawData TargetCached",
                    RGBMaterialPool.TargetCached(drawData));
                  Expect.IsTrue($"{turret.key ?? turret.turretDef.defName} Materials Allocated",
                    RGBMaterialPool.GetAll(drawData)?.Length == drawData.MaterialCount);
                }
              }
            }
          }
        }

        // Upgrades
        CompProperties_UpgradeTree compUpgrade =
          vehicleDef.GetCompProperties<CompProperties_UpgradeTree>();
        if (compUpgrade?.def != null && !compUpgrade.def.nodes.NullOrEmpty())
        {
          foreach (UpgradeNode node in compUpgrade.def.nodes)
          {
            List<GraphicOverlay> overlays = compUpgrade.TryGetOverlays(node);
            if (!overlays.NullOrEmpty())
            {
              foreach (GraphicOverlay overlay in overlays)
              {
                if (!overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
                  continue;

                Expect.IsTrue($"{node.key} TargetCached", RGBMaterialPool.TargetCached(overlay));
                Expect.IsTrue($"{node.key} Materials Allocated",
                  RGBMaterialPool.GetAll(overlay)?.Length == overlay.MaterialCount);
              }
            }
          }
        }
      }
    }

    [Test]
    private void PatternDefs()
    {
      foreach (PatternDef patternDef in DefDatabase<PatternDef>.AllDefsListForReading)
      {
        Expect.IsTrue($"{patternDef} TargetCached", RGBMaterialPool.TargetCached(patternDef));
        Expect.IsTrue($"{patternDef} Materials Allocated",
          RGBMaterialPool.GetAll(patternDef)?.Length == patternDef.MaterialCount);
      }
    }

    [Test]
    private void Total()
    {
      int count = 0;
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        // Base Vehicle
        if (vehicleDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
          count += vehicleDef.MaterialCount;

        // Overlays
        if (vehicleDef.drawProperties != null && !vehicleDef.drawProperties.overlays.NullOrEmpty())
        {
          foreach (GraphicOverlay overlay in vehicleDef.drawProperties.overlays)
          {
            if (overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
              count += overlay.MaterialCount;
          }
        }

        // Turrets
        CompProperties_VehicleTurrets compTurrets =
          vehicleDef.GetCompProperties<CompProperties_VehicleTurrets>();
        if (compTurrets is not null && !compTurrets.turrets.NullOrEmpty())
        {
          foreach (VehicleTurret turret in compTurrets.turrets)
          {
            if (!turret.NoGraphic &&
              turret.turretDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
            {
              count += turret.MaterialCount;
            }
            if (!turret.TurretGraphics.NullOrEmpty())
            {
              foreach (VehicleTurret.TurretDrawData drawData in turret.TurretGraphics)
              {
                if (drawData.graphicData.shaderType.Shader.SupportsRGBMaskTex())
                  count += drawData.MaterialCount;
              }
            }
          }
        }

        CompProperties_UpgradeTree compUpgrade =
          vehicleDef.GetCompProperties<CompProperties_UpgradeTree>();
        if (compUpgrade?.def != null && !compUpgrade.def.nodes.NullOrEmpty())
        {
          foreach (UpgradeNode node in compUpgrade.def.nodes)
          {
            List<GraphicOverlay> overlays = compUpgrade.TryGetOverlays(node);
            if (overlays.NullOrEmpty())
              continue;

            foreach (GraphicOverlay overlay in overlays)
            {
              if (overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
                count += overlay.MaterialCount;
            }
          }
        }
      }

      foreach (PatternDef patternDef in DefDatabase<PatternDef>.AllDefsListForReading)
      {
        count += patternDef.MaterialCount;
      }

      Expect.IsTrue("MaterialPool (Total Count)", count == RGBMaterialPool.TotalMaterials);
    }
  }
}