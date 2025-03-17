using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Debugging;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestMaterialPool : UnitTestMapTest
  {
    public override TestType ExecuteOn => TestType.GameLoaded;

    public override string Name => "MaterialPool_Instances";

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      UTResult result = new();

      // VehicleGraphic
      using MaterialPoolWatcher vehicleMats = new();

      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"MaterialPool_{vehicle.def} (Spawned)", vehicle.Spawned);

      int targets = 0;
      int materialCount = 0;

      // Turrets
      // NOTE - CompVehicleTurrets initializes all turrets and their graphics PostSpawn, so
      // material allocations will be tracked alongside main body graphic. We can still check
      // allocations before calling VehicleGraphic since they won't be cached until the first
      // VehicleGraphic invocation.
      if (vehicle.CompVehicleTurrets != null && !vehicle.CompVehicleTurrets.turrets.NullOrEmpty())
      {
        foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.turrets)
        {
          // Turret graphic is created in ctor, we need to force regenerate to
          // log results in MaterialPoolWatcher and track material lifetime.
          turret.ResolveCannonGraphics(vehicle.patternData, forceRegen: true);
          if (!turret.NoGraphic &&
            turret.turretDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
          {
            targets++;
            materialCount += turret.MaterialCount;
          }

          if (!turret.TurretGraphics.NullOrEmpty())
          {
            foreach (VehicleTurret.TurretDrawData drawData in turret.TurretGraphics)
            {
              if (drawData.graphicData.shaderType.Shader.SupportsRGBMaskTex())
              {
                targets++;
                materialCount += turret.MaterialCount;
              }
            }
          }
        }

        result.Add($"MaterialPool_{vehicle.def} (Add Turret MaterialCacheTarget)",
          vehicleMats.CacheTargets == targets);
        result.Add($"MaterialPool_{vehicle.def} (Add Turret Graphic)",
          vehicleMats.MaterialsAllocated == materialCount);
      }

      if (vehicle.VehicleDef.graphicData.shaderType.Shader.SupportsRGBMaskTex())
      {
        targets++; // Only 1 vehicle instance
        materialCount += vehicle.MaterialCount;
      }

      _ = vehicle.VehicleGraphic; // Force graphic to be cached before any upgrade calls
      result.Add($"MaterialPool_{vehicle.def} (Add MaterialCacheTarget)",
        vehicleMats.CacheTargets == targets);
      result.Add($"MaterialPool_{vehicle.def} (Add Graphic)",
        vehicleMats.MaterialsAllocated == materialCount);

      // Overlays
      if (vehicle.overlayRenderer.Overlays.Count > 0)
      {
        using MaterialPoolWatcher overlayMats = new();
        int overlayMaterialCount = 0;
        int overlayTargets = 0;
        foreach (GraphicOverlay overlay in vehicle.overlayRenderer.Overlays)
        {
          if (overlay.data.graphicData.shaderType.Shader.SupportsRGBMaskTex())
          {
            overlayTargets++;
            overlayMaterialCount += overlay.MaterialCount;
          }

          _ = overlay.Graphic;
        }

        result.Add($"MaterialPool_{vehicle.def} Overlay (Add MaterialCacheTarget)",
          overlayMats.CacheTargets == overlayTargets);
        result.Add($"MaterialPool_{vehicle.def} Overlay (Add Graphic)",
          overlayMats.MaterialsAllocated == overlayMaterialCount);
      }

      // UpgradeTree
      if (vehicle.CompUpgradeTree != null)
      {
        // Upgrades can add overlays and turrets from any upgrade type including types
        // from other mods, so tracking expected material allocations will depend on the
        // vehicles being tested with. Instead, we track target and material count before
        // and after the upgrade to ensure they're all freed when upgrades are reset.
        foreach (UpgradeNode node in vehicle.CompUpgradeTree.Props.def.nodes)
        {
          using MaterialPoolWatcher upgradeMats = new();
          vehicle.CompUpgradeTree.FinishUnlock(node);
          vehicle.CompUpgradeTree.ResetUnlock(node);
          result.Add($"MaterialPool_{vehicle.def} {node.label} (Node Upgrades Destroyed)",
            upgradeMats.AllocationsEqualized);
        }

        // Unlock again so we can test cleanup with vehicle destroy
        foreach (UpgradeNode node in vehicle.CompUpgradeTree.Props.def.nodes)
        {
          vehicle.CompUpgradeTree.FinishUnlock(node);
        }
      }

      // Final cleanup of vehicle, any unhandled material allocations should still be
      // handled here. It's imperative that all materials are destroyed once the vehicle
      // is destroyed. There won't be any further attempts so the materials will persist.
      vehicle.Destroy();
      result.Add($"MaterialPool_{vehicle.def} (VehicleGraphic Destroyed)", vehicleMats.AllFree);

      return result;
    }
  }
}