using System;
using System.Collections.Generic;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Vehicles.Rendering;
using Verse;

namespace Vehicles;

[StaticConstructorOnStartup]
public partial class VehicleTurret
{
  /* --- Parsed --- */

  [TweakField]
  public VehicleTurretRender renderProperties = new();

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2 aimPieOffset = Vector2.zero;

  [TweakField(SettingsType = UISettingsType.IntegerBox)]
  public int drawLayer = 1;

  public string gizmoLabel;

  /* ----------------- */

  [Unsaved]
  protected PreRenderResults results;

  [Unsaved]
  protected List<PreRenderResults> subGraphicResults;

  // Cache all root draw pos on spawn
  [Unsaved]
  protected Vector3 rootDrawPos_North;

  [Unsaved]
  protected Vector3 rootDrawPos_East;

  [Unsaved]
  protected Vector3 rootDrawPos_South;

  [Unsaved]
  protected Vector3 rootDrawPos_West;

  [Unsaved]
  protected Vector3 rootDrawPos_NorthEast;

  [Unsaved]
  protected Vector3 rootDrawPos_SouthEast;

  [Unsaved]
  protected Vector3 rootDrawPos_SouthWest;

  [Unsaved]
  protected Vector3 rootDrawPos_NorthWest;

  public Texture2D currentFireIcon;
  protected Texture2D gizmoIcon;
  protected Texture2D mainMaskTex;

  [Unsaved]
  protected Texture2D cachedTexture;

  [Unsaved]
  protected Material cachedMaterial;

  [Unsaved]
  protected Graphic_Turret cachedGraphic;

  [Unsaved]
  protected GraphicDataRGB cachedGraphicData;

  [Unsaved]
  protected List<TurretDrawData> turretGraphics;

  [Unsaved]
  protected RotatingList<Texture2D> overheatIcons;

  public MaterialPropertyBlock PropertyBlock { get; private set; }

  public static MaterialPropertyBlock TargeterPropertyBlock { get; private set; }

  public virtual void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData)
  {
    if (NoGraphic)
      return;

    switch (phase)
    {
      case DrawPhase.EnsureInitialized:
        if (!turretDef.graphics.NullOrEmpty())
          subGraphicResults = [];
        break;
      case DrawPhase.ParallelPreDraw:
        results = ParallelPreRenderResults(in transformData, TurretRotation);
        if (subGraphicResults != null)
        {
          AddSubGraphicParallelPreRenderResults(in transformData, subGraphicResults);
        }
        break;
      case DrawPhase.Draw:
        // TODO - Check if we'll be rendering turrets with dynamic rotations, otherwise we'll
        // need to change this to be more flexible for unspawned vehicle rendering.
        if (!results.valid)
          results = ParallelPreRenderResults(in transformData,
            transformData.orientation.AsAngle + defaultAngleRotated);
        Draw();
        results = default;
        subGraphicResults?.Clear();
        break;
      default:
        throw new NotImplementedException(nameof(DrawPhase));
    }
  }

  protected virtual PreRenderResults ParallelPreRenderResults(
    ref readonly TransformData transformData, float turretRotation, bool addRecoil = true)
  {
    PreRenderResults render = new()
    {
      valid = true,
      draw = true
    };
    Vector3 rootPos = transformData.position + TurretDrawLocFor(transformData.orientation);
    if (addRecoil)
    {
      if (recoilTracker is { Recoil: > 0f })
      {
        rootPos += Vector3.zero.PointFromAngle(recoilTracker.Recoil, recoilTracker.Angle);
      }
      if (attachedTo?.recoilTracker.Recoil > 0f)
      {
        rootPos += Vector3.zero.PointFromAngle(attachedTo.recoilTracker.Recoil,
          attachedTo.recoilTracker.Angle);
      }
    }
    render.position = rootPos;
    render.quaternion = turretRotation.ToQuat();
    render.mesh = Graphic.MeshAt(transformData.orientation);
    render.material = Material;
    return render;
  }

  protected virtual void AddSubGraphicParallelPreRenderResults(
    ref readonly TransformData transformData, List<PreRenderResults> outList)
  {
    outList.Clear();
    for (int i = 0; i < TurretGraphics.Count; i++)
    {
      PreRenderResults render = new()
      {
        valid = true,
        draw = true
      };

      TurretDrawData turretDrawData = TurretGraphics[i];
      Turret_RecoilTracker subRecoilTracker = recoilTrackers[i];

      Vector3 rootPos =
        turretDrawData.DrawOffset(transformData.position, transformData.orientation);
      Vector3 recoilOffset = Vector3.zero;
      Vector3 parentRecoilOffset = Vector3.zero;
      if (subRecoilTracker is { Recoil: > 0f })
      {
        recoilOffset = Vector3.zero.PointFromAngle(subRecoilTracker.Recoil,
          subRecoilTracker.Angle);
      }
      if (attachedTo?.recoilTracker is { Recoil: > 0f })
      {
        parentRecoilOffset = Vector3.zero.PointFromAngle(attachedTo.recoilTracker.Recoil,
          attachedTo.recoilTracker.Angle);
      }
      render.position = rootPos + recoilOffset + parentRecoilOffset;
      render.quaternion = TurretRotation.ToQuat();
      render.mesh = turretDrawData.graphic.MeshAt(transformData.orientation);
      render.material = turretDrawData.graphic.MatAt(Rot4.North);

      outList.Add(render);
    }
  }

  public Vector3 TurretDrawLocFor(Rot8 rot, bool fullLoc = true)
  {
    float locationRotation = 0f;
    if (fullLoc && attachedTo != null)
    {
      locationRotation = TurretRotationFor(rot, attachedTo.TurretRotation);
    }

    Vector2 turretLoc = VehicleGraphics.TurretDrawOffset(rot, renderProperties, locationRotation,
      fullLoc ? attachedTo : null);
    Vector3 graphicOffset = Graphic?.DrawOffset(rot) ?? Vector3.zero;
    return new Vector3(graphicOffset.x + turretLoc.x, graphicOffset.y + DrawLayerOffset,
      graphicOffset.z + turretLoc.y);
  }

  public Rect ScaleUIRectFor(VehicleDef vehicleDef, Rect rect, Rot8 rot, float iconScale = 1)
  {
    // Scale to VehicleDef drawSize, vehicle will scale based on max dimension
    Vector2 size =
      vehicleDef.ScaleDrawRatio(turretDef.graphicData, rot, rect.size, iconScale: iconScale);
    float scalar = rect.width /
      Mathf.Max(vehicleDef.graphicData.drawSize.x, vehicleDef.graphicData.drawSize.y);

    Vector2 offset = renderProperties.OffsetFor(rot) * scalar;
    offset.y *= -1;

    Vector3 graphicOffset = turretDef.graphicData.DrawOffsetForRot(rot);
    Vector2 baseOffset = new Vector2(graphicOffset.x, graphicOffset.z) * scalar;

    Vector2 position = rect.center + baseOffset + offset;

    if (attachedTo != null)
    {
      Vector2 parentCenter = attachedTo.ScaleUIRectFor(vehicleDef, rect, rot, iconScale: iconScale)
       .center;
      position += parentCenter - rect.center;
      //float parentRotation = TurretRotationFor(rot, attachedTo.defaultAngleRotated);
      //offsetPosition = Ext_Math.RotatePointClockwise(offsetPosition, parentRotation);
    }

    return new Rect(position - size / 2, size);
  }

  IEnumerable<RenderData> IBlitTarget.GetRenderData(Rect rect, BlitRequest request)
  {
    if (!NoGraphic)
    {
      Rect turretRect =
        VehicleGraphics.TurretRect(rect, vehicleDef, this, request.rot, iconScale: request.scale);
      bool canMask = Graphic.Shader.SupportsMaskTex() || Graphic.Shader.SupportsRGBMaskTex();
      Material material = canMask ? Material : null;
      if (canMask && turretDef.matchParentColor)
      {
        RGBMaterialPool.SetProperties(this, request.patternData, Graphic.TexAt, Graphic.MaskAt);
      }
      RenderData turretRenderData = new(turretRect, Texture, material,
        PropertyBlock, GraphicData.drawOffset.y, defaultAngleRotated + request.rot.AsAngle);
      yield return turretRenderData;
    }
    if (!TurretGraphics.NullOrEmpty())
    {
      foreach (TurretDrawData turretDrawData in TurretGraphics)
      {
        Rect turretRect = VehicleGraphics.TurretRect(rect, vehicleDef, this, request.rot);
        Graphic_Turret graphic = turretDrawData.graphic;
        bool canMask = graphic.Shader.SupportsMaskTex() || graphic.Shader.SupportsRGBMaskTex();
        Material material = canMask ? graphic.MatAtFull(Rot8.North) : null;
        if (canMask && turretDef.matchParentColor)
        {
          RGBMaterialPool.SetProperties(turretDrawData, request.patternData, graphic.TexAt,
            graphic.MaskAt);
        }
        RenderData turretRenderData = new(turretRect, graphic.TexAt(Rot8.North),
          material, turretDrawData.PropertyBlock, turretDrawData.graphicData.drawOffset.y,
          defaultAngleRotated + request.rot.AsAngle);
        yield return turretRenderData;
      }
    }
  }

  protected virtual void Draw()
  {
    Graphics.DrawMesh(results.mesh, results.position, results.quaternion, results.material, 0);

    if (subGraphicResults != null)
    {
      foreach (PreRenderResults renderResults in subGraphicResults)
      {
        Graphics.DrawMesh(renderResults.mesh, renderResults.position, renderResults.quaternion,
          renderResults.material, 0);
      }
    }

    if (vehicle.Spawned)
    {
      DrawTargeter();
      DrawAimPie();
    }
  }

  protected virtual void DrawTargeter()
  {
    // TODO - clean up 
    TargeterPropertyBlock ??= new MaterialPropertyBlock();
    if (GizmoHighlighted || TurretTargeter.Turret == this)
    {
      if (angleRestricted != Vector2.zero)
      {
        VehicleGraphics.DrawAngleLines(TurretLocation, angleRestricted, MinRange, MaxRange,
          restrictedTheta, attachedTo?.TurretRotation ?? vehicle.FullRotation.AsAngle);
      }
      else if (turretDef.turretType == TurretType.Static)
      {
        if (!groupKey.NullOrEmpty())
        {
          foreach (VehicleTurret turret in GroupTurrets)
          {
            Vector3 target =
              turret.TurretLocation.PointFromAngle(turret.MaxRange, turret.TurretRotation);
            float range = Vector3.Distance(turret.TurretLocation, target);
            GenDraw.DrawRadiusRing(target.ToIntVec3(),
              turret.CurrentFireMode.spreadRadius * (range / turret.turretDef.maxRange));
          }
        }
        else
        {
          Vector3 target = TurretLocation.PointFromAngle(MaxRange, TurretRotation);
          float range = Vector3.Distance(TurretLocation, target);
          GenDraw.DrawRadiusRing(target.ToIntVec3(),
            CurrentFireMode.spreadRadius * (range / turretDef.maxRange));
        }
      }
      else
      {
        if (MaxRange > -1)
        {
          Vector3 pos = TurretLocation;
          pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
          float currentAlpha = 0.65f;
          if (currentAlpha > 0f)
          {
            Color value = Color.grey;
            value.a *= currentAlpha;
            TargeterPropertyBlock.SetColor(ShaderPropertyIDs.Color, value);
            Matrix4x4 matrix = default;
            matrix.SetTRS(pos, Quaternion.identity, new Vector3(MaxRange * 2f, 1f, MaxRange * 2f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, TexData.RangeMat((int)MaxRange), 0, null, 0,
              TargeterPropertyBlock);
          }
        }

        if (MinRange > 0)
        {
          Vector3 pos = TurretLocation;
          pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
          float currentAlpha = 0.65f;
          if (currentAlpha > 0f)
          {
            Color value = Color.red;
            value.a *= currentAlpha;
            TargeterPropertyBlock.SetColor(ShaderPropertyIDs.Color, value);
            Matrix4x4 matrix = default;
            matrix.SetTRS(pos, Quaternion.identity, new Vector3(MinRange * 2f, 1f, MinRange * 2f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, TexData.RangeMat((int)MinRange), 0, null, 0,
              TargeterPropertyBlock);
          }
        }
      }
    }
  }

  protected virtual void DrawAimPie()
  {
    if (TargetLocked && ReadyToFire && Find.Selector.SingleSelectedThing == vehicle)
    {
      float facing = targetInfo.Thing != null ?
        (targetInfo.Thing.DrawPos - TurretLocation).AngleFlat() :
        (targetInfo.Cell - TurretLocation.ToIntVec3()).AngleFlat;
      GenDraw.DrawAimPieRaw(
        TurretLocation +
        new Vector3(aimPieOffset.x, Altitudes.AltInc, aimPieOffset.y).RotatedBy(TurretRotation),
        facing, (int)(PrefireTickCount * 0.5f));
    }
  }

  public virtual void ResolveCannonGraphics(VehiclePawn vehicle, bool forceRegen = false)
  {
    ResolveCannonGraphics(vehicle.patternData, forceRegen: forceRegen);
  }

  public virtual void ResolveCannonGraphics(VehicleDef vehicleDef, bool forceRegen = false)
  {
    PatternData patternData =
      VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
        vehicleDef.graphicData);
    ResolveCannonGraphics(patternData, forceRegen: forceRegen);
  }

  // TODO 1.6 - Rename to ResolveGraphics
  public virtual void ResolveCannonGraphics(PatternData patternData, bool forceRegen = false)
  {
    if (NoGraphic)
    {
      return;
    }

    if (cachedGraphicData is null || forceRegen)
    {
      cachedGraphic = GenerateGraphicData(this, this, turretDef.graphicData, patternData,
        ref cachedGraphicData);
      if (!turretDef.graphics.NullOrEmpty())
      {
        SetLayerGraphics(patternData);
      }
    }

    if (cachedMaterial is null || forceRegen)
    {
      cachedMaterial = Graphic.MatAt(Rot8.North, vehicle);
    }
  }

  private void SetLayerGraphics(PatternData patternData)
  {
    if (turretGraphics.NullOrEmpty())
    {
      turretGraphics ??= [];
      foreach (VehicleTurretRenderData renderData in turretDef.graphics)
      {
        turretGraphics.Add(new TurretDrawData(this, renderData));
      }
    }

    for (int i = 0; i < turretDef.graphics.Count; i++)
    {
      VehicleTurretRenderData renderData = turretDef.graphics[i];
      TurretDrawData drawData = TurretGraphics[i];
      drawData.Set(renderData.graphicData, patternData);
    }
  }

  private static Graphic_Turret GenerateGraphicData(IMaterialCacheTarget cacheTarget,
    VehicleTurret turret, GraphicDataRGB copyGraphicData, PatternData patternData,
    ref GraphicDataRGB cachedGraphicData)
  {
    cachedGraphicData = new GraphicDataRGB();
    cachedGraphicData.CopyFrom(copyGraphicData);
    Graphic_Turret graphic;
    if ((cachedGraphicData.shaderType.Shader.SupportsMaskTex() ||
      cachedGraphicData.shaderType.Shader.SupportsRGBMaskTex()))
    {
      if (turret.turretDef.matchParentColor)
      {
        cachedGraphicData.CopyDrawData(patternData);
      }
      else
      {
        cachedGraphicData.CopyDrawData(copyGraphicData);
      }
    }

    if (cachedGraphicData.shaderType != null &&
      cachedGraphicData.shaderType.Shader.SupportsRGBMaskTex())
    {
      RGBMaterialPool.CacheMaterialsFor(cacheTarget, patternData.patternDef);
      cachedGraphicData.Init(cacheTarget);
      graphic = cachedGraphicData.Graphic as Graphic_Turret;
      Assert.IsNotNull(graphic);
      RGBMaterialPool.SetProperties(cacheTarget, cachedGraphicData, graphic.TexAt, graphic.MaskAt);
    }
    else
    {
      graphic = ((GraphicData)cachedGraphicData).Graphic as Graphic_Turret;
    }

    return graphic;
  }

  public class TurretDrawData : IMaterialCacheTarget
  {
    private readonly VehicleTurret turret;

    public Graphic_Turret graphic;
    public GraphicDataRGB graphicData;
    public VehicleTurretRenderData renderData;

    public TurretDrawData(VehicleTurret turret, VehicleTurretRenderData renderData)
    {
      this.turret = turret;
      this.renderData = renderData;
    }

    public int MaterialCount => 1;

    public PatternDef PatternDef => turret.PatternDef;

    public string Name => $"{turret.turretDef}_{turret.key}_{turret.vehicle?.ThingID ?? "Def"}";

    // TurretDrawData is already created on the main thread
    public MaterialPropertyBlock PropertyBlock { get; } = new();

    public void Set(GraphicDataRGB copyFrom, PatternData patternData)
    {
      graphic = GenerateGraphicData(this, turret, copyFrom, patternData, ref graphicData);
    }

    public Vector3 DrawOffset(Vector3 drawPos, Rot8 rot)
    {
      float locationRotation = 0f;
      if (turret.attachedTo != null)
      {
        locationRotation = TurretRotationFor(rot, turret.attachedTo.TurretRotation);
      }

      Vector3 graphicOffset = graphic.DrawOffset(rot);
      Vector2 rotatedPoint =
        Ext_Math.RotatePointClockwise(graphicOffset.x, graphicOffset.z, locationRotation);
      return new Vector3(drawPos.x + rotatedPoint.x, drawPos.y + graphicOffset.y,
        drawPos.z + rotatedPoint.y);
    }

    public override string ToString()
    {
      return $"TurretDrawData_{turret.key}_({graphicData.texPath})";
    }
  }

  protected struct PreRenderResults
  {
    public bool valid;
    public bool draw;
    public Mesh mesh;
    public Material material;
    public Vector3 position;
    public Quaternion quaternion;
  }
}