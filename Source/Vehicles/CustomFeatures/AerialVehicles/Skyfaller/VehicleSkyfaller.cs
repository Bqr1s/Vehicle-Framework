using System.Collections.Generic;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Vehicles
{
  [StaticConstructorOnStartup]
  public abstract class VehicleSkyfaller : Thing, IThingHolder, IRoofCollapseAlert, ISustainerTarget
  {
    protected static MaterialPropertyBlock shadowPropertyBlock = new MaterialPropertyBlock();

    public float angle;
    protected Vector3 launchProtocolDrawPos;

    protected Material cachedShadowMaterial;

    protected bool anticipationSoundPlayed;

    public VehiclePawn vehicle;

    public override Vector3 DrawPos => launchProtocolDrawPos;

    protected Vector3 RootPos => vehicle.TrueCenter(Position, base.DrawPos.y);

    public ThingWithComps Thing => vehicle;

    TargetInfo ISustainerTarget.Target => this;

    MaintenanceType ISustainerTarget.MaintenanceType => MaintenanceType.PerTick;

    private Material ShadowMaterial
    {
      get
      {
        if (cachedShadowMaterial is null && !def.skyfaller.shadow.NullOrEmpty())
        {
          cachedShadowMaterial =
            MaterialPool.MatFrom(def.skyfaller.shadow, ShaderDatabase.Transparent);
        }
        return cachedShadowMaterial;
      }
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
      if (outChildren == null)
      {
        return;
      }
      outChildren.AddRange(vehicle.handlers);
    }

    public ThingOwner GetDirectlyHeldThings()
    {
      return vehicle.inventory.innerContainer;
    }

    protected override void Tick()
    {
      vehicle.CompVehicleLauncher.launchProtocol.Tick();
      vehicle.DoTick();
      // Manually tick handlers since vehicle is despawned but pawns aren't world pawns
      // so they aren't ticking from WorldPawns.
      // TODO - we might be able to just have BaseTickOptimized call TickHandlers regardless
      // of spawn status. But that assumes passengers are never world pawns.
      vehicle.TickHandlers();
    }

    protected virtual void LeaveMap()
    {
      Destroy();
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
      base.DeSpawn(mode);
      vehicle.ReleaseSustainerTarget();
    }

    protected virtual void DrawDropSpotShadow()
    {
      Material shadowMaterial = ShadowMaterial;
      if (shadowMaterial is null)
      {
        return;
      }
      //TODO - draw shadow at DrawPos but z-axis is left on ground and size decreases through curve
      DrawDropSpotShadow(DrawPos, Rotation, shadowMaterial, def.skyfaller.shadowSize,
        vehicle.CompVehicleLauncher.launchProtocol.TicksPassed);
    }

    public static void DrawDropSpotShadow(Vector3 center, Rot4 rot, Material material,
      Vector2 shadowSize, int ticksToLand)
    {
      if (rot.IsHorizontal)
      {
        Gen.Swap(ref shadowSize.x, ref shadowSize.y);
      }
      ticksToLand = Mathf.Max(ticksToLand, 0);
      Vector3 pos = center;
      pos.y = AltitudeLayer.Shadows.AltitudeFor();
      float num = 1f + ticksToLand / 100f;
      Vector3 s = new Vector3(num * shadowSize.x, 1f, num * shadowSize.y);
      Color white = Color.white;
      if (ticksToLand > 150)
      {
        white.a = Mathf.InverseLerp(200f, 150f, ticksToLand);
      }
      shadowPropertyBlock.SetColor(ShaderPropertyIDs.Color, white);
      Matrix4x4 matrix = default;
      matrix.SetTRS(pos, rot.AsQuat, s);
      Graphics.DrawMesh(MeshPool.plane10Back, matrix, material, 0, null, 0, shadowPropertyBlock);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      launchProtocolDrawPos = RootPos;
      if (vehicle.IsWorldPawn())
      {
        Find.WorldPawns.RemovePawn(vehicle);
        foreach (Pawn pawn in vehicle.AllPawnsAboard)
        {
          if (pawn.IsWorldPawn())
          {
            Find.WorldPawns.RemovePawn(pawn);
          }
        }
      }
      vehicle.SetSustainerTarget(this);
      vehicle.ResetRenderStatus(); //Reset required for recaching handler lists. Loading save file will not recache these since vehicle will be despawned initially
    }

    public override void ExposeData()
    {
      base.ExposeData();

      Scribe_Values.Look(ref angle, "angle", 0f, false);
      Scribe_Deep.Look(ref vehicle, "vehicle");
    }

    RoofCollapseResponse IRoofCollapseAlert.Notify_OnBeforeRoofCollapse()
    {
      return RoofCollapseResponse.None;
    }
  }
}