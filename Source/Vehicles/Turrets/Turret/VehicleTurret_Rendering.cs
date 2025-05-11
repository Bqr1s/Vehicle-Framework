using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Vehicles.Rendering;
using Verse;
using Verse.Sound;

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

  public bool GizmoHighlighted { get; set; }

  public MaterialPropertyBlock PropertyBlock { get; private set; }

  public static MaterialPropertyBlock TargeterPropertyBlock { get; private set; }

  public int MaterialCount => 1;

  public string Name => $"{def}_{key}_{vehicle?.ThingID ?? "Def"}";

  public bool NoGraphic => def.graphicData is null;

  public float DrawLayerOffset => drawLayer * (Altitudes.AltInc / GraphicDataLayered.SubLayerCount);

  public PatternDef PatternDef
  {
    get
    {
      if (NoGraphic)
      {
        return PatternDefOf.Default;
      }

      if (vehicle == null)
      {
        return VehicleMod.settings.vehicles.defaultGraphics
           .TryGetValue(vehicleDef.defName, vehicleDef.graphicData)?.patternDef ??
          PatternDefOf.Default;
      }

      if (def.matchParentColor)
      {
        return vehicle?.PatternDef ?? PatternDefOf.Default;
      }

      return GraphicData.pattern;
    }
  }

  public Texture2D FireIcon
  {
    get
    {
      if (Find.TickManager.TicksGame % TicksPerOverheatingFrame == 0)
      {
        currentFireIcon = OverheatIcons.Next;
      }
      return currentFireIcon;
    }
  }

  protected RotatingList<Texture2D> OverheatIcons
  {
    get
    {
      if (overheatIcons.NullOrEmpty())
      {
        overheatIcons = VehicleTex.FireIcons.ToRotatingList();
      }
      return overheatIcons;
    }
  }

  public virtual Material Material
  {
    get
    {
      if (cachedMaterial is null)
        ResolveGraphics(vehicle);
      return cachedMaterial;
    }
  }

  public virtual Texture2D Texture
  {
    get
    {
      if (GraphicData.texPath.NullOrEmpty())
        return null;
      cachedTexture ??= ContentFinder<Texture2D>.Get(GraphicData.texPath);
      return cachedTexture;
    }
  }

  public virtual Texture2D MainMaskTexture
  {
    get
    {
      if (GraphicData.texPath.NullOrEmpty())
        return null;

      mainMaskTex ??=
        ContentFinder<Texture2D>.Get(GraphicData.texPath + Graphic_Turret.TurretMaskSuffix);
      return mainMaskTex;
    }
  }

  public virtual GraphicDataRGB GraphicData
  {
    get
    {
      if (cachedGraphicData is null)
        ResolveGraphics(vehicle);
      return cachedGraphicData;
    }
  }

  public virtual Graphic_Turret Graphic
  {
    get
    {
      if (cachedGraphic is null)
        ResolveGraphics(vehicle);
      return cachedGraphic;
    }
  }

  public virtual List<TurretDrawData> TurretGraphics
  {
    get { return turretGraphics; }
  }

  public virtual Texture2D GizmoIcon
  {
    get
    {
      if (gizmoIcon is null)
      {
        if (!string.IsNullOrEmpty(def.gizmoIconTexPath))
        {
          gizmoIcon = ContentFinder<Texture2D>.Get(def.gizmoIconTexPath);
        }
        else if (NoGraphic)
        {
          gizmoIcon = BaseContent.BadTex;
        }
        else
        {
          gizmoIcon = Texture ?? BaseContent.BadTex;
        }
      }

      return gizmoIcon;
    }
  }

  /// <summary>
  /// Smaller actions inside VehicleTurret gizmo
  /// </summary>
  public virtual IEnumerable<SubGizmo> SubGizmos
  {
    get
    {
      if (def.magazineCapacity > 0)
      {
        if (def.ammunition != null)
        {
          yield return SubGizmo_RemoveAmmo(this);
        }

        yield return SubGizmo_ReloadFromInventory(this);
      }

      yield return SubGizmo_FireMode(this);

      if (autoTargeting)
      {
        yield return SubGizmo_AutoTarget(this);
      }
    }
  }

  public virtual void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData)
  {
    if (NoGraphic)
      return;

    switch (phase)
    {
      case DrawPhase.EnsureInitialized:
        if (!def.graphics.NullOrEmpty())
          subGraphicResults = [];
        // Ensure meshes are created and cached
        for (int i = 0; i < 4; i++)
          _ = Graphic.MeshAt(new Rot4(i));
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
    // This is more or less the same implementation as Graphic_Rgb::ParallelGetPreRenderResults
    // The fixed North orientation, turret rotation, and additional offsetting makes it more
    // trouble than its worth to try and fetch then modify.
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
    if (vehicle is { Spawned: true } && def.graphicData.altLayerSpawned is { } altLayerSpawned)
    {
      render.position.y = altLayerSpawned.AltitudeFor();
      render.position.y += Graphic.DrawOffset(Rot4.North).y;
    }
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

      // This is more or less the same implementation as Graphic_Rgb::ParallelGetPreRenderResults
      // The fixed North orientation, turret rotation, and additional offsetting makes it more
      // trouble than its worth to try and fetch then modify.
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
      if (vehicle is { Spawned: true } && turretDrawData.graphicData.altLayerSpawned is
        { } altLayerSpawned)
      {
        render.position.y = altLayerSpawned.AltitudeFor();
        render.position.y += Graphic.DrawOffset(Rot4.North).y;
      }
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
    GraphicDataRGB data = def.graphicData;
    Vector2 size = vehicleDef.ScaleDrawRatio(data, rot, rect.size, iconScale: iconScale);
    Vector2 original = new(data.drawSize.x, data.drawSize.y);
    Vector2 scaleFactors = new(size.x / original.x, size.y / original.y);
    Vector3 drawOffset = data.DrawOffsetForRot(rot);
    Vector2 baseOffset = new(drawOffset.x * scaleFactors.x, -drawOffset.z * scaleFactors.y);
    Vector2 propOffset = renderProperties.OffsetFor(rot);
    Vector2 offset = new(propOffset.x * scaleFactors.x, -propOffset.y * scaleFactors.y);
    Vector2 position = rect.center + baseOffset + offset;
    if (attachedTo != null)
    {
      Rect parentRect = attachedTo.ScaleUIRectFor(vehicleDef, rect, rot, iconScale);
      position += parentRect.center - rect.center;
    }
    return new Rect(position - size * 0.5f, size);
  }

  (int width, int height) IBlitTarget.TextureSize(in BlitRequest request)
  {
    return Texture != null ? (Texture.width, Texture.height) : (0, 0);
  }

  IEnumerable<RenderData> IBlitTarget.GetRenderData(Rect rect, BlitRequest request)
  {
    if (!NoGraphic)
    {
      Rect turretRect =
        VehicleGraphics.TurretRect(rect, vehicleDef, this, request.rot);
      bool canMask = Graphic.Shader.SupportsMaskTex() || Graphic.Shader.SupportsRGBMaskTex();
      Material material = canMask ? Material : null;
      if (canMask && def.matchParentColor)
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
        if (canMask && def.matchParentColor)
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
      else if (def.turretType == TurretType.Static)
      {
        if (!groupKey.NullOrEmpty())
        {
          foreach (VehicleTurret turret in GroupTurrets)
          {
            Vector3 target =
              turret.TurretLocation.PointFromAngle(turret.MaxRange, turret.TurretRotation);
            float range = Vector3.Distance(turret.TurretLocation, target);
            GenDraw.DrawRadiusRing(target.ToIntVec3(),
              turret.CurrentFireMode.forcedMissRadius * (range / turret.def.maxRange));
          }
        }
        else
        {
          Vector3 target = TurretLocation.PointFromAngle(MaxRange, TurretRotation);
          float range = Vector3.Distance(TurretLocation, target);
          GenDraw.DrawRadiusRing(target.ToIntVec3(),
            CurrentFireMode.forcedMissRadius * (range / def.maxRange));
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

  public virtual void ResolveGraphics(VehiclePawn vehicle, bool forceRegen = false)
  {
    ResolveGraphics(vehicle.patternData, forceRegen: forceRegen);
  }

  public virtual void ResolveGraphics(VehicleDef vehicleDef, bool forceRegen = false)
  {
    PatternData patternData =
      VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
        vehicleDef.graphicData);
    ResolveGraphics(patternData, forceRegen: forceRegen);
  }

  public virtual void ResolveGraphics(PatternData patternData, bool forceRegen = false)
  {
    if (NoGraphic)
      return;

    if (cachedGraphicData is null || forceRegen)
    {
      cachedGraphic = GenerateGraphicData(this, this, def.graphicData, patternData,
        ref cachedGraphicData);
      if (!def.graphics.NullOrEmpty())
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
      foreach (VehicleTurretRenderData renderData in def.graphics)
      {
        turretGraphics.Add(new TurretDrawData(this, renderData));
      }
    }

    for (int i = 0; i < def.graphics.Count; i++)
    {
      VehicleTurretRenderData renderData = def.graphics[i];
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
      if (turret.def.matchParentColor)
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

  public virtual void InitTurretMotes(Vector3 loc)
  {
    if (def.motes.NullOrEmpty())
      return;

    foreach (AnimationProperties moteProps in def.motes)
    {
      Vector3 moteLoc = loc;
      if (!loc.ShouldSpawnMotesAt(vehicle.Map))
        continue;

      try
      {
        float altitudeLayer = moteProps.moteDef.altitudeLayer.AltitudeFor();
        Vector3 offset = moteProps.offset.RotatedBy(TurretRotation);
        moteLoc += new Vector3(offset.x, altitudeLayer + offset.y, offset.z);
        Mote mote = (Mote)ThingMaker.MakeThing(moteProps.moteDef);
        mote.exactPosition = moteLoc;
        mote.exactRotation = moteProps.exactRotation.RandomInRange;
        mote.instanceColor = moteProps.color;
        mote.rotationRate = moteProps.rotationRate;
        mote.Scale = moteProps.scale;
        if (mote is MoteThrown thrownMote)
        {
          float thrownAngle = TurretRotation + moteProps.angleThrown.RandomInRange;
          if (thrownMote is MoteThrownExpand expandMote)
          {
            if (expandMote is MoteThrownSlowToSpeed accelMote)
            {
              accelMote.SetDecelerationRate(moteProps.deceleration.RandomInRange,
                moteProps.fixedAcceleration, thrownAngle);
            }

            expandMote.growthRate = moteProps.growthRate.RandomInRange;
          }

          thrownMote.SetVelocity(thrownAngle, moteProps.speedThrown.RandomInRange);
        }

        if (mote is MoteCannonPlume cannonMote)
        {
          cannonMote.cyclesLeft = moteProps.cycles;
          cannonMote.animationType = moteProps.animationType;
          cannonMote.angle = TurretRotation;
        }

        mote.def = moteProps.moteDef;
        mote.PostMake();
        GenSpawn.Spawn(mote, moteLoc.ToIntVec3(), vehicle.Map);
      }
      catch (Exception ex)
      {
        SmashLog.Error(
          $"Failed to spawn mote at {loc}. MoteDef = <field>{moteProps.moteDef?.defName ?? "Null"}</field> Exception = {ex}");
      }
    }
  }

  /// <summary>
  /// Caches VehicleTurret draw location based on <see cref="renderProperties"/>, <see cref="attachedTo"/> draw loc is not cached, as rotating can alter the final draw location
  /// </summary>
  public void RecacheRootDrawPos()
  {
    if (GraphicData != null)
    {
      rootDrawPos_North = TurretDrawLocFor(Rot8.North, fullLoc: false);
      rootDrawPos_East = TurretDrawLocFor(Rot8.East, fullLoc: false);
      rootDrawPos_South = TurretDrawLocFor(Rot8.South, fullLoc: false);
      rootDrawPos_West = TurretDrawLocFor(Rot8.West, fullLoc: false);
      rootDrawPos_NorthEast = TurretDrawLocFor(Rot8.NorthEast, fullLoc: false);
      rootDrawPos_SouthEast = TurretDrawLocFor(Rot8.SouthEast, fullLoc: false);
      rootDrawPos_SouthWest = TurretDrawLocFor(Rot8.SouthWest, fullLoc: false);
      rootDrawPos_NorthWest = TurretDrawLocFor(Rot8.NorthWest, fullLoc: false);
    }
  }

  public Vector3 TurretOffset(Rot8 rot)
  {
    return rot.AsInt switch
    {
      // North
      0 => rootDrawPos_North,
      // East
      1 => rootDrawPos_East,
      // South
      2 => rootDrawPos_South,
      // West
      3 => rootDrawPos_West,
      // NorthEast
      4 => rootDrawPos_NorthEast,
      // SouthEast
      5 => rootDrawPos_SouthEast,
      // SouthWest
      6 => rootDrawPos_SouthWest,
      // NorthWest
      7 => rootDrawPos_NorthWest,
      _ => throw new NotImplementedException("Invalid Rot8")
    };
  }

  public static SubGizmo SubGizmo_RemoveAmmo(VehicleTurret turret)
  {
    return new SubGizmo(
      drawGizmo: delegate(Rect rect)
      {
        //Widgets.DrawTextureFitted(rect, BGTex, 1);
        if (turret.loadedAmmo != null)
        {
          //Only modify alpha
          using (new TextBlock(new Color(GUI.color.r, GUI.color.g, GUI.color.b,
            turret.IconAlphaTicked)))
          {
            Widgets.DrawTextureFitted(rect, turret.loadedAmmo.uiIcon, 1);
          }

          Rect ammoCountRect = new(rect);
          string ammoCount = turret.vehicle.inventory.innerContainer
           .Where(td => td.def == turret.loadedAmmo).Select(t => t.stackCount).Sum().ToStringSafe();
          ammoCountRect.y += ammoCountRect.height / 2;
          ammoCountRect.x += ammoCountRect.width - Text.CalcSize(ammoCount).x;
          Widgets.Label(ammoCountRect, ammoCount);
        }
        else if (turret.def.genericAmmo && turret.def.ammunition.AllowedDefCount > 0)
        {
          ThingDef ammoDef = turret.def.ammunition.AllowedThingDefs.FirstOrDefault();
          Assert.IsNotNull(ammoDef);
          Widgets.DrawTextureFitted(rect, ammoDef.uiIcon, 1);

          Rect ammoCountRect = new(rect);
          string ammoCount = turret.vehicle.inventory.innerContainer
           .Where(td => td.def == turret.def.ammunition.AllowedThingDefs.FirstOrDefault())
           .Select(t => t.stackCount).Sum().ToStringSafe();
          ammoCountRect.y += ammoCountRect.height / 2;
          ammoCountRect.x += ammoCountRect.width - Text.CalcSize(ammoCount).x;
          Widgets.Label(ammoCountRect, ammoCount);
        }
      },
      canClick: () => turret.shellCount > 0,
      onClick: delegate
      {
        turret.TryClearChamber();
        SoundDefOf.Artillery_ShellLoaded.PlayOneShot(new TargetInfo(turret.vehicle.Position,
          turret.vehicle.Map));
      },
      tooltip: turret.loadedAmmo?.LabelCap
    );
  }

  public static SubGizmo SubGizmo_ReloadFromInventory(VehicleTurret turret)
  {
    return new SubGizmo(
      drawGizmo: delegate(Rect rect) { Widgets.DrawTextureFitted(rect, VehicleTex.ReloadIcon, 1); },
      canClick: () => true,
      onClick: delegate
      {
        if (turret.def.ammunition is null)
        {
          turret.Reload();
        }
        else if (turret.def.genericAmmo)
        {
          if (!turret.vehicle.inventory.innerContainer.Contains(turret.def.ammunition
           .AllowedThingDefs.FirstOrDefault()))
          {
            Messages.Message("VF_NoAmmoAvailable".Translate(), MessageTypeDefOf.RejectInput);
          }
          else
          {
            turret.Reload(turret.def.ammunition.AllowedThingDefs.FirstOrDefault());
          }
        }
        else
        {
          List<FloatMenuOption> options = [];
          List<ThingDef> ammoAvailable = turret.vehicle.inventory.innerContainer
           .Where(d => turret.ContainsAmmoDefOrShell(d.def)).Select(t => t.def).Distinct().ToList();
          for (int i = ammoAvailable.Count - 1; i >= 0; i--)
          {
            ThingDef ammo = ammoAvailable[i];
            options.Add(new FloatMenuOption(ammoAvailable[i].LabelCap,
              delegate { turret.Reload(ammo, ammo != turret.savedAmmoType); }));
          }

          if (options.NullOrEmpty())
          {
            FloatMenuOption noAmmoOption =
              new("VF_VehicleTurrets_NoAmmoToReload".Translate(), null)
              {
                Disabled = true
              };
            options.Add(noAmmoOption);
          }

          Find.WindowStack.Add(new FloatMenu(options));
        }
      },
      tooltip: "VF_ReloadVehicleTurret".Translate()
    );
  }

  public static SubGizmo SubGizmo_FireMode(VehicleTurret turret)
  {
    return new SubGizmo(
      drawGizmo: delegate(Rect rect)
      {
        Widgets.DrawTextureFitted(rect, turret.CurrentFireMode.Icon, 1);
      },
      canClick: () => turret.def.fireModes.Count > 1,
      onClick: turret.CycleFireMode,
      tooltip: turret.CurrentFireMode.label
    );
  }

  public static SubGizmo SubGizmo_AutoTarget(VehicleTurret turret)
  {
    return new SubGizmo(
      drawGizmo: delegate(Rect rect)
      {
        Widgets.DrawTextureFitted(rect, VehicleTex.AutoTargetIcon, 1);
        Rect checkboxRect = new(rect.x + rect.width / 2, rect.y + rect.height / 2,
          rect.width / 2, rect.height / 2);
        GUI.DrawTexture(checkboxRect,
          turret.AutoTarget ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
      },
      canClick: () => turret.CanAutoTarget,
      onClick: turret.SwitchAutoTarget,
      tooltip: AutoTargetingTooltip()
    );

    string AutoTargetingTooltip()
    {
      StringBuilder tooltip = UIHelper.tooltipBuilder;
      tooltip.Clear();
      tooltip.AppendLine("VF_ToggleAutoTargeting".Translate());
      tooltip.AppendLine();
      tooltip.AppendLine();
      tooltip.AppendLine("VF_ToggleAutoTargetingDesc"
       .Translate((turret.AutoTarget ? "On".TranslateSimple() : "Off".TranslateSimple())
         .UncapitalizeFirst().Named("ONOFF"))
       .Resolve());
      string text = tooltip.ToString();
      tooltip.Clear();
      return text;
    }
  }

  [NoProfiling]
  public (Texture2D mainTex, Texture2D maskTex) GetTextures(Rot8 rot)
  {
    throw new NotImplementedException();
  }

  public readonly struct SubGizmo
  {
    public readonly Action<Rect> drawGizmo;
    public readonly Func<bool> canClick;
    public readonly Action onClick;
    public readonly string tooltip;

    public SubGizmo(Action<Rect> drawGizmo, Func<bool> canClick, Action onClick, string tooltip)
    {
      this.drawGizmo = drawGizmo;
      this.canClick = canClick;
      this.onClick = onClick;
      this.tooltip = tooltip;
    }

    public bool IsValid => onClick != null;

    public static SubGizmo None { get; private set; } = new();
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

    public string Name => $"{turret.def}_{turret.key}_{turret.vehicle?.ThingID ?? "Def"}";

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
}