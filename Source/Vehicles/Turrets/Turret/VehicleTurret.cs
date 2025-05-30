﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles.Rendering;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Vehicles;

[PublicAPI]
public partial class VehicleTurret : IExposable, ILoadReferenceable, ITweakFields,
                                     IEventManager<VehicleTurretEventDef>, IMaterialCacheTarget,
                                     IParallelRenderer, IBlitTarget, ITransformable
{
  public const int AutoTargetInterval = 60;
  public const int TicksPerOverheatingFrame = 15;
  public const int TicksTillBeginCooldown = 60;
  public const float MaxHeatCapacity = 100;
  public const int DefaultMaxRange = 9999;

  private static readonly List<IntVec3> projectileDestCells = [];

  /* --- Parsed --- */

  public int uniqueID = -1;
  public string parentKey;
  public string key;
  public string groupKey;

  [Unsaved]
  public VehicleTurret reference;

  [TweakField]
  [LoadAlias("turretDef")]
  public VehicleTurretDef def;

  public TargetLock targeting = TargetLock.Pawn | TargetLock.Thing;

  [TweakField(SettingsType = UISettingsType.Checkbox)]
  public bool targetPersists;

  [TweakField(SettingsType = UISettingsType.Checkbox)]
  public bool autoTargeting = true;

  [TweakField(SettingsType = UISettingsType.Checkbox)]
  public bool manualTargeting = true;

  [TweakField(SettingsType = UISettingsType.SliderEnum)]
  public DeploymentType deployment = DeploymentType.None;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2 angleRestricted = Vector2.zero;

  public float defaultAngleRotated;

  public ComponentRequirement component;

  /* ----------------- */

  public string upgradeKey;

  public LocalTargetInfo targetInfo;

  protected float restrictedTheta;

  public ThingDef loadedAmmo;
  public ThingDef savedAmmoType;

  public int shellCount;

  protected bool autoTargetingActive;

  private int reloadTicks;
  private int burstTicks;

  protected int currentFireMode;
  public float currentHeatRate;
  protected bool triggeredCooldown;
  protected int ticksSinceLastShot;
  public bool queuedToFire;

  protected Rot4 parentRotCached;
  protected float parentAngleCached;

  protected int burstsTillWarmup;

  [Unsaved]
  protected float rotationTargeted = float.NaN;

  [Unsaved]
  protected int ticksRotating;

  [Unsaved]
  public VehiclePawn vehicle;

  [Unsaved]
  public VehicleDef vehicleDef;

  /// Necessary to separate from vehicle for def-contained turrets
  /// <see cref="CompProperties_VehicleTurrets"/> for PatternData
  [Unsaved]
  public VehicleTurret attachedTo;

  [Unsaved]
  public List<VehicleTurret> childTurrets = [];

  [Unsaved]
  protected List<VehicleTurret> groupTurrets;

  [Unsaved]
  public TurretRestrictions restrictions;

  [Unsaved]
  public Turret_RecoilTracker recoilTracker;

  [Unsaved]
  public Turret_RecoilTracker[] recoilTrackers;

  private static readonly List<(Thing, int)> thingsToTakeReloading = [];

  /// <summary>
  /// Init from CompProperties
  /// </summary>
  public VehicleTurret()
  {
  }

  /// <summary>
  /// Init from save file
  /// </summary>
  public VehicleTurret(VehiclePawn vehicle)
  {
    this.vehicle = vehicle;
    vehicleDef = vehicle.VehicleDef;
  }

  /// <summary>
  /// Newly Spawned
  /// </summary>
  /// <param name="vehicle"></param>
  /// <param name="reference">VehicleTurret as defined in xml</param>
  public VehicleTurret(VehiclePawn vehicle, VehicleTurret reference)
  {
    this.vehicle = vehicle;
    vehicleDef = vehicle.VehicleDef;

    uniqueID = Find.UniqueIDsManager.GetNextThingID();
    def = reference.def;

    gizmoLabel = reference.gizmoLabel;

    key = reference.key;

    targetPersists =
      reference.def.turretType != TurretType.Static && reference.targetPersists;
    autoTargeting = reference.def.turretType != TurretType.Static && reference.autoTargeting;
    manualTargeting =
      reference.def.turretType != TurretType.Static && reference.manualTargeting;

    currentFireMode = 0;
    currentFireIcon = OverheatIcons.FirstOrDefault();
    ticksSinceLastShot = 0;
    burstsTillWarmup = 0;

    TurretRotation = reference.defaultAngleRotated;

    InitRecoilTrackers();
  }

  string ITweakFields.Label => nameof(VehicleTurret); ////def.LabelCap;

  string ITweakFields.Category => def.LabelCap;

  public bool TargetLocked { get; private set; }

  public int PrefireTickCount { get; private set; }

  public int CurrentTurretFiring { get; set; }

  public bool IsManned { get; private set; }

  public PawnStatusOnTarget CachedPawnTargetStatus { get; set; }

  public bool IsTargetable => def?.turretType == TurretType.Rotatable;

  public bool RotationAligned => Mathf.Approximately(transform.rotation, rotationTargeted);

  public bool TurretRestricted => restrictions?.Disabled ?? false;

  public virtual bool TurretDisabled =>
    TurretRestricted || !IsManned || !DeploymentSatisfied || ComponentDisabled;

  public bool ComponentDisabled => component is { MeetsRequirements: false } ||
    attachedTo is { ComponentDisabled: true };

  protected virtual bool TurretTargetValid => targetInfo.Cell.IsValid && !TurretDisabled;

  public bool CanAutoTarget => autoTargeting || VehicleMod.settings.debug.debugShootAnyTurret;

  public int WarmupTicks => Mathf.CeilToInt(def.warmUpTimer * 60f);

  public bool OnCooldown => triggeredCooldown;

  public bool CanOverheat => VehicleMod.settings.main.overheatMechanics &&
    def.cooldown is { heatPerShot: > 0 };

  public bool HasAmmo => def.ammunition is null || shellCount > 0;

  public bool ReadyToFire => groupKey.NullOrEmpty() ?
    (burstTicks <= 0 && ReloadTicks <= 0 && !TurretDisabled) :
    GroupTurrets.Any(t => t.burstTicks <= 0 && t.ReloadTicks <= 0 && !t.TurretDisabled);

  public bool FullAuto =>
    CurrentFireMode.ticksBetweenBursts.TrueMin == CurrentFireMode.ticksBetweenShots;

  public int ReloadTicks => reloadTicks;

  public EventManager<VehicleTurretEventDef> EventRegistry { get; set; }

  public bool DeploymentSatisfied
  {
    get
    {
      return deployment switch
      {
        DeploymentType.None => true,
        DeploymentType.Deployed => vehicle.CompVehicleTurrets is
          { Deployed: true, Deploying: false },
        DeploymentType.Undeployed => vehicle.CompVehicleTurrets is
          { Deployed: false, Deploying: false },
        _ => throw new NotImplementedException(nameof(DeploymentType))
      };
    }
  }

  public int MaxTicks
  {
    get
    {
      float maxTicks = def.reloadTimer * 60f;
      if (def.reloadTimerMultiplierPerCrewCount != null)
      {
        int count = vehicle.PawnsByHandlingType[HandlingType.Turret].Count;
        maxTicks *= def.reloadTimerMultiplierPerCrewCount.Evaluate(count);
      }

      return Mathf.CeilToInt(maxTicks);
    }
  }

  public ThingDef ProjectileDef
  {
    get { return loadedAmmo?.projectileWhenLoaded ?? def.projectile; }
  }

  public List<VehicleTurret> GroupTurrets
  {
    get
    {
      if (groupTurrets is null)
      {
        if (groupKey.NullOrEmpty())
        {
          groupTurrets = [this];
        }
        else
        {
          groupTurrets = vehicle.CompVehicleTurrets.Turrets.Where(t => t.groupKey == groupKey)
           .ToList();
        }
      }

      return groupTurrets;
    }
  }

  public virtual int MaxShotsCurrentFireMode
  {
    get
    {
      if (FullAuto)
      {
        if (!CanOverheat)
        {
          return CurrentFireMode.shotsPerBurst.TrueMax * 3;
        }

        return Mathf.CeilToInt(MaxHeatCapacity / def.cooldown.heatPerShot);
      }

      return CurrentFireMode.shotsPerBurst.RandomInRange;
    }
  }

  public int TicksPerShot
  {
    get { return CurrentFireMode.ticksBetweenShots; }
  }

  public float IconAlphaTicked
  {
    get
    {
      if (ReloadTicks <= 0)
      {
        return 1;
      }

      return Mathf.PingPong(ReloadTicks, 25) / 50f + 0.25f; //ping pong between 0.25 and 0.75 alpha
    }
  }

  public Vector3 TurretLocation
  {
    get
    {
      if (attachedTo != null)
      {
        // Don't use cached value if attached to parent turret (position may change with rotations)
        return vehicle.DrawPos + DrawPosition(vehicle.FullRotation);
      }
      return vehicle.DrawPos + TurretOffset(vehicle.FullRotation);
    }
  }

  public float TurretRotation
  {
    get
    {
      if (!IsTargetable && attachedTo is null)
        return defaultAngleRotated + vehicle.FullRotation.AsAngle;

      UpdateRotationLock();

      transform.rotation = transform.rotation.ClampAndWrap(0, 360);
      if (angleRestricted != Vector2.zero)
        transform.rotation = transform.rotation.Clamp(angleRestricted.x, angleRestricted.y);

      if (attachedTo != null)
        return transform.rotation + attachedTo.TurretRotation;
      return transform.rotation;
    }
    set { transform.rotation = value.ClampAndWrap(0, 360); }
  }

  public float TurretRotationTargeted
  {
    get { return rotationTargeted; }
    set
    {
      if (Mathf.Approximately(rotationTargeted, value))
        return;
      rotationTargeted = value.ClampAndWrap(0, 360);
    }
  }

  public FireMode CurrentFireMode
  {
    get
    {
      if (currentFireMode < 0 || currentFireMode >= def.fireModes.Count)
      {
        SmashLog.ErrorOnce(
          $"Unable to retrieve fire mode at index {currentFireMode}. Outside of bounds for <field>fireModes</field> defined in <field>def</field>. Defaulting to first fireMode.",
          GetHashCode() ^ currentFireMode);
        return def.fireModes.FirstOrDefault();
      }

      return def.fireModes[currentFireMode];
    }
    set
    {
      currentFireMode = def.fireModes.IndexOf(value);
      ResetPrefireTimer();
    }
  }

  public bool AutoTarget
  {
    get { return autoTargetingActive; }
    set
    {
      if (!CanAutoTarget || value == autoTargetingActive)
      {
        return;
      }

      autoTargetingActive = value;

      if (autoTargetingActive)
      {
        StartTicking();
      }
    }
  }

  public float MaxRange
  {
    get
    {
      if (def.maxRange <= 0)
      {
        return DefaultMaxRange;
      }
      return def.maxRange;
    }
  }

  public float MinRange
  {
    get { return def.minRange; }
  }

  /// <summary>
  /// Initialize non-serialized data to the turret, pulls in values from what's defined in xml.
  /// </summary>
  /// <param name="reference">Non-spawnable turret reference containing xml values.</param>
  public void Init(VehicleTurret reference)
  {
    this.reference = reference;
    groupKey = reference.groupKey;
    parentKey = reference.parentKey;

    renderProperties = new VehicleTurretRender(reference.renderProperties);
    if (reference.component != null)
    {
      component = ComponentRequirement.CopyFrom(reference.component);
    }
    aimPieOffset = reference.aimPieOffset;
    angleRestricted = reference.angleRestricted;
    restrictedTheta = (int)Mathf.Abs(angleRestricted.x - (angleRestricted.y + 360)).ClampAngle();

    defaultAngleRotated = reference.defaultAngleRotated;
    deployment = reference.deployment;

    drawLayer = reference.drawLayer;
    if (reference.def.restrictionType != null)
    {
      SetTurretRestriction(reference.def.restrictionType);
    }

    ResetAngle();
    LongEventHandler.ExecuteWhenFinished(() => PropertyBlock ??= new MaterialPropertyBlock());
  }

  public virtual void PostSpawnSetup(bool respawningAfterLoad)
  {
    LongEventHandler.ExecuteWhenFinished(RecacheRootDrawPos);
    component?.RegisterEvents(vehicle);
  }

  public void SetTurretRestriction(Type type)
  {
    if (!type.IsSubclassOf(typeof(TurretRestrictions)))
    {
      Log.Error("Trying to create TurretRestriction with non-matching type.");
      return;
    }

    restrictions = (TurretRestrictions)Activator.CreateInstance(type);
    restrictions.Init(vehicle, this);
  }

  public void RemoveTurretRestriction()
  {
    restrictions = null;
  }

  public bool IsDisabled(out string reason)
  {
    if (TurretRestricted)
    {
      reason = restrictions.DisableReason;
      return true;
    }
    if (!DeploymentSatisfied)
    {
      reason = deployment switch
      {
        DeploymentType.None       => string.Empty,
        DeploymentType.Deployed   => "VF_MustBeDeployed".Translate(),
        DeploymentType.Undeployed => "VF_MustBeUndeployed".Translate(),
        _                         => throw new NotImplementedException(nameof(DeploymentType))
      };
      return true;
    }
    if (ComponentDisabled)
    {
      VehicleTurret turretRestriction = this;
      while (turretRestriction != null)
      {
        if (turretRestriction.component is { MeetsRequirements: false })
        {
          reason = "VF_TurretComponentDisabled".Translate(turretRestriction.component.Label);
          return true;
        }
        turretRestriction = attachedTo;
      }
      throw new InvalidOperationException(nameof(IsDisabled));
    }
    reason = null;
    return false;
  }

  public void OnFieldChanged()
  {
    RecacheRootDrawPos();
  }

  public void RecacheMannedStatus()
  {
    IsManned = true;
    foreach (VehicleRoleHandler handler in vehicle.handlers)
    {
      if (handler.role.HandlingTypes.HasFlag(HandlingType.Turret) &&
        (handler.role.TurretIds.Contains(key) || handler.role.TurretIds.Contains(groupKey)))
      {
        if (!handler.RoleFulfilled)
        {
          IsManned = false;
          break;
        }
      }
    }
    // Only if debug shoot any turret = true do we set satisfied to true anyways.
    IsManned |= VehicleMod.settings.debug.debugShootAnyTurret;
  }

  public bool GroupsWith(VehicleTurret turret)
  {
    return !groupKey.NullOrEmpty() && groupKey == turret.groupKey;
  }

  public static float TurretRotationFor(Rot8 rot, float currentRotation)
  {
    return currentRotation + rot.AsAngle;
  }

  public virtual bool ActivateTimer(bool ignoreTimer = false)
  {
    if (ReloadTicks > 0 && !ignoreTimer)
    {
      return false;
    }

    reloadTicks = MaxTicks;
    TargetLocked = false;
    StartTicking();
    return true;
  }

  public virtual void ActivateBurstTimer()
  {
    burstTicks = CurrentFireMode.ticksBetweenBursts.RandomInRange;
    burstsTillWarmup--;

    if (burstsTillWarmup <= 0)
    {
      ResetPrefireTimer();
    }
  }

  public void StartTicking()
  {
    vehicle.CompVehicleTurrets.QueueTicker(this);
  }

  /// <summary>
  /// Should only be called in the event that this turret needs to stop ticking unconditionally, otherwise let <see cref="CompVehicleTurrets"/> dequeue
  /// when it's determined this turret no longer requires ticking.
  /// </summary>
  public void StopTicking()
  {
    vehicle.CompVehicleTurrets.DequeueTicker(this);
  }

  public virtual bool Tick()
  {
    bool cooldownTicked = TurretCooldownTick();
    bool reloadTicked = TurretReloadTick();
    bool rotationTicked = TurretRotationTick();
    bool targeterTicked = TurretTargeterTick();
    bool autoTicked = TurretAutoTick();
    bool recoilTicked = false;
    if (recoilTracker != null)
    {
      recoilTicked = recoilTracker.RecoilTick();
    }

    if (!recoilTrackers.NullOrEmpty())
    {
      for (int i = 0; i < def.graphics.Count; i++)
      {
        recoilTicked |= recoilTrackers[i]?.RecoilTick() ?? false;
      }
    }

    //Keep ticking until no longer needed
    return cooldownTicked || reloadTicked || autoTicked || rotationTicked || targeterTicked ||
      recoilTicked;
  }

  protected virtual bool TurretCooldownTick()
  {
    if (CanOverheat)
    {
      if (currentHeatRate > 0)
      {
        ticksSinceLastShot++;
      }

      if (currentHeatRate > MaxHeatCapacity)
      {
        triggeredCooldown = true;
        currentHeatRate = MaxHeatCapacity;
        EventRegistry[VehicleTurretEventDefOf.Cooldown].ExecuteEvents();
      }
      else if (currentHeatRate <= 0)
      {
        currentHeatRate = 0;
        triggeredCooldown = false;
        return false;
      }

      if (ticksSinceLastShot >= TicksTillBeginCooldown)
      {
        float dissipationRate = def.cooldown.dissipationRate;
        if (triggeredCooldown)
        {
          dissipationRate *= def.cooldown.dissipationCapMultiplier;
        }

        currentHeatRate -= dissipationRate;
      }

      return true;
    }

    return false;
  }

  protected virtual bool TurretReloadTick()
  {
    if (vehicle.Spawned && !queuedToFire)
    {
      if (ReloadTicks > 0 && !OnCooldown)
      {
        reloadTicks--;
        return true;
      }

      if (burstTicks > 0)
      {
        burstTicks--;
        return true;
      }
    }

    return false;
  }

  protected virtual bool TurretAutoTick()
  {
    if (vehicle.Spawned && !queuedToFire && AutoTarget)
    {
      if (Find.TickManager.TicksGame % AutoTargetInterval == 0)
      {
        if (TurretDisabled)
        {
          return false;
        }

        if (!targetInfo.IsValid && TurretTargeter.Turret != this && ReloadTicks <= 0 && HasAmmo)
        {
          if (this.TryGetTarget(out LocalTargetInfo autoTarget,
            additionalFlags: TargetScanFlags.NeedAutoTargetable))
          {
            AlignToAngleRestricted(TurretLocation.AngleToPoint(autoTarget.Thing.DrawPos));
            SetTarget(autoTarget);
          }
        }
      }

      return true;
    }

    return false;
  }

  protected virtual bool TurretRotationTick()
  {
    if (ComponentDisabled)
    {
      ResetAngle();
      return false;
    }
    bool tick = false;
    if (TargetLocked)
    {
      AlignToTargetRestricted();
      tick = true;
    }
    if (RotationAligned)
    {
      ticksRotating = 0;
      return tick;
    }
    if (def.autoSnapTargeting)
    {
      TurretRotation = TurretRotationTargeted;
      return true;
    }

    if (Math.Abs(TurretRotation - TurretRotationTargeted) < def.rotationSpeed + 0.1f)
    {
      TurretRotation = TurretRotationTargeted;
    }
    else
    {
      int rotationDir;
      if (TurretRotation < TurretRotationTargeted)
      {
        rotationDir = Mathf.Abs(TurretRotation - TurretRotationTargeted) < 180 ? 1 : -1;
      }
      else
      {
        rotationDir = Mathf.Abs(TurretRotation - TurretRotationTargeted) < 180 ? -1 : 1;
      }
      float delta = def.rotationDelta > 0 ?
        Ext_Math.SmoothStep(0, 1, ticksRotating / (def.rotationDelta * 60)) :
        1;
      float rotateStep = delta * def.rotationSpeed * rotationDir;
      TurretRotation += rotateStep;
      ticksRotating++;
    }
    return true;
  }

  protected virtual bool TurretTargeterTick()
  {
    if (TurretTargetValid)
    {
      if (Mathf.Approximately(transform.rotation, TurretRotationTargeted) && !TargetLocked)
      {
        TargetLocked = true;
        ResetPrefireTimer();
      }
      else if (!TurretTargetValid)
      {
        SetTarget(LocalTargetInfo.Invalid);
        return TurretTargeter.Turret == this;
      }

      if (IsTargetable && !TargetingHelper.TargetMeetsRequirements(this, targetInfo, out _))
      {
        SetTarget(LocalTargetInfo.Invalid);
        TargetLocked = false;
        return TurretTargeter.Turret == this;
      }

      if (PrefireTickCount > 0)
      {
        TurretRotationTargeted = targetInfo.HasThing ?
          TurretLocation.AngleToPoint(targetInfo.Thing.DrawPos) :
          TurretLocation.ToIntVec3().AngleToCell(targetInfo.Cell);
        if (attachedTo != null)
        {
          TurretRotationTargeted -= attachedTo.TurretRotation;
        }
        if (def.autoSnapTargeting)
        {
          TurretRotation = TurretRotationTargeted;
        }

        if (TargetLocked && ReadyToFire)
        {
          PrefireTickCount--;
        }
      }
      else if (ReadyToFire)
      {
        if (IsTargetable && RotationAligned && (targetInfo.Pawn is null || !CheckTargetInvalid()))
        {
          GroupTurrets.ForEach(t => t.PushTurretToQueue());
        }
        else if (
          FullAuto &&
          queuedToFire) // Child turrets will want to continue firing as their parent rotates
        {
          GroupTurrets.ForEach(t => t.PushTurretToQueue());
        }
      }

      return true;
    }
    else if (IsTargetable)
    {
      return TurretTargeter.Turret == this;
    }

    return false;
  }

  public virtual CompVehicleTurrets.TurretData GenerateTurretData()
  {
    return new CompVehicleTurrets.TurretData()
    {
      shots = CurrentFireMode.shotsPerBurst.RandomInRange,
      ticksTillShot = 0,
      turret = this
    };
  }

  public virtual void PushTurretToQueue()
  {
    ActivateBurstTimer();
    vehicle.CompVehicleTurrets.QueueTurret(GenerateTurretData());
  }

  public bool TryFindShootLineFromTo(IntVec3 root, LocalTargetInfo target,
    out ShootLine resultingLine)
  {
    if (!TargetingHelper.TargetMeetsRequirements(this, target, out IntVec3 dest))
    {
      resultingLine = default;
      return false;
    }
    resultingLine = new ShootLine(root, dest);
    return true;
  }

  public virtual void FireTurret()
  {
    if (!vehicle.Spawned)
    {
      return;
    }

    float range = Vector3.Distance(TurretLocation, targetInfo.CenterVector3);
    LocalTargetInfo usedTarget = targetInfo;

    if (CurrentTurretFiring >= def.projectileShifting.Count)
      CurrentTurretFiring = 0;

    float horizontalOffset = !def.projectileShifting.NullOrEmpty() ?
      def.projectileShifting[CurrentTurretFiring] :
      0;
    Vector3 launchPos = TurretLocation +
      new Vector3(horizontalOffset, 1f, def.projectileOffset).RotatedBy(TurretRotation);

    if (def.ammunition != null)
    {
      ConsumeChamberedShot();
    }

    ThingDef projectileDef = ProjectileDef;
    if (LaunchProjectileCE is null)
    {
      Projectile projectileInstance =
        (Projectile)GenSpawn.Spawn(projectileDef, vehicle.Position, vehicle.Map);
      ProjectileHitFlags hitFlags = projectileInstance.HitFlags;
      Thing hitCover = null;
      if (CurrentFireMode.forcedMissRadius > 0)
      {
        // Forced miss - bypass accuracy checks completely
        int cellsInRadius = def.maxRange > 0 ?
          GenRadial.NumCellsInRadius(CurrentFireMode.forcedMissRadius * (range / def.maxRange)) :
          GenRadial.NumCellsInRadius(CurrentFireMode.forcedMissRadius);
        IntVec3 cell = usedTarget.Cell;
        cell += GenRadial.RadialPattern[Rand.Range(0, cellsInRadius)];
        usedTarget = cell;
      }
      else
      {
        if (!TryFindShootLineFromTo(TurretLocation.ToIntVec3(), targetInfo, out ShootLine line))
        {
          return;
        }
        TurretShotReport report =
          TurretShotReport.HitReportFor(vehicle, this, targetInfo, caster: null /* TODO */);
        hitCover = report.GetRandomCoverToMissInto();
        // Missed completely
        if (CurrentFireMode.canMiss && !Rand.Chance(report.AimOnTargetChanceWithSize))
        {
          line.ChangeDestToMissWild(report.AimOnTargetChance, projectileDef.projectile.flyOverhead,
            vehicle.Map);
          hitFlags = ProjectileHitFlags.NonTargetWorld;
          if (Rand.Chance(0.5f))
            hitFlags |= ProjectileHitFlags.NonTargetPawns;

          usedTarget = line.Dest;
        }
        // Missed and hit cover
        else if (targetInfo.Thing is { def.CanBenefitFromCover: true } &&
          !Rand.Chance(report.PassCoverChance))
        {
          hitFlags = ProjectileHitFlags.NonTargetWorld | ProjectileHitFlags.NonTargetPawns;
          usedTarget = hitCover;
        }
        // Hit target
        else
        {
          hitFlags = ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetPawns;
          if (!targetInfo.HasThing || targetInfo.Thing.def.Fillage == FillCategory.Full)
          {
            hitFlags |= ProjectileHitFlags.NonTargetWorld;
          }
          usedTarget = targetInfo.HasThing ? targetInfo.Thing : line.Dest;
        }
      }

      if (def.projectileSpeed > 0 || def.attachProjectileFlag != null)
      {
        CompTurretProjectileProperties projectileProps = new(projectileInstance)
        {
          speed = def.projectileSpeed > 0 ?
            def.projectileSpeed :
            projectileInstance.def.projectile.speed,
          hitflags = def.attachProjectileFlag
        };
        if (!projectileInstance.TryAddComp(projectileProps))
          Log.Error($"Failed to attach modified properties to {projectileDef}");
      }
      projectileInstance.Launch(vehicle, launchPos, usedTarget, targetInfo, hitFlags,
        equipment: vehicle, targetCoverDef: hitCover?.def);
    }
    else
    {
      // TODO - CE might want accuracy values, right now they're only working with the forced miss radius.
      FireTurretCE(projectileDef, launchPos);
    }
    EventRegistry[VehicleTurretEventDefOf.ShotFired].ExecuteEvents();
    PostTurretFire();
    InitTurretMotes(launchPos);
  }

  public virtual void PostTurretFire()
  {
    def.shotSound?.PlayOneShot(new TargetInfo(vehicle.Position, vehicle.Map));
    vehicle.DrawTracker.recoilTracker.Notify_TurretRecoil(this,
      Ext_Math.RotateAngle(TurretRotation, 180));

    recoilTracker?.Notify_TurretRecoil(Ext_Math.RotateAngle(TurretRotation, 180));

    if (!recoilTrackers.NullOrEmpty())
    {
      foreach (Turret_RecoilTracker rTracker in recoilTrackers)
      {
        rTracker.Notify_TurretRecoil(Ext_Math.RotateAngle(TurretRotation, 180));
      }
    }

    ticksSinceLastShot = 0;
    if (CanOverheat)
    {
      currentHeatRate += def.cooldown.heatPerShot;
    }
  }

  private void InitRecoilTrackers()
  {
    if (def.recoil != null)
    {
      recoilTracker = new Turret_RecoilTracker(def.recoil);
    }

    if (!def.graphics.NullOrEmpty())
    {
      recoilTrackers = new Turret_RecoilTracker[def.graphics.Count];
      for (int i = 0; i < def.graphics.Count; i++)
      {
        if (def.graphics[i].recoil is { } recoilProperties)
        {
          recoilTrackers[i] = new Turret_RecoilTracker(recoilProperties);
        }
      }
    }
  }


  public bool AngleBetween(Vector3 position)
  {
    if (angleRestricted == Vector2.zero)
      return true;

    float rotationOffset = attachedTo?.TurretRotation ?? vehicle.Rotation.AsAngle + vehicle.Angle;

    float start = angleRestricted.x + rotationOffset;
    float end = angleRestricted.y + rotationOffset;

    if (start > 360)
      start -= 360;
    if (end > 360)
      end -= 360;

    float mid = (position - TurretLocation).AngleFlat();
    end = (end - start) < 0f ? end - start + 360 : end - start;
    mid = (mid - start) < 0f ? mid - start + 360 : mid - start;
    return mid < end;
  }

  public bool InRange(LocalTargetInfo target)
  {
    if (MinRange == 0 && Mathf.Approximately(MaxRange, DefaultMaxRange))
    {
      return true;
    }

    IntVec3 cell = target.Cell;
    Vector2 targetPos = new(cell.x, cell.z);
    Vector3 targetLoc = TurretLocation;
    float distance = Vector2.Distance(new Vector2(targetLoc.x, targetLoc.z), targetPos);

    return distance >= MinRange && distance <= MaxRange;
  }

  public void AlignToTargetRestricted()
  {
    TurretRotationTargeted = targetInfo.HasThing ?
      TurretLocation.AngleToPoint(targetInfo.Thing.DrawPos) :
      TurretLocation.ToIntVec3().AngleToCell(targetInfo.Cell);
    if (attachedTo != null)
      TurretRotationTargeted -= attachedTo.TurretRotation;
  }

  public void AlignToAngleRestricted(float angle)
  {
    float additionalAngle = attachedTo?.TurretRotation ?? 0;
    TurretRotationTargeted = angle - additionalAngle;
  }

  public virtual void Reload(ThingDef ammo = null, bool ignoreTimer = false)
  {
    if ((ammo == savedAmmoType || ammo is null) && shellCount == def.magazineCapacity)
    {
      return;
    }

    if (def.ammunition is null)
    {
      shellCount = def.magazineCapacity;
      return;
    }

    if (loadedAmmo is null || (ammo != null && shellCount < def.magazineCapacity) ||
      shellCount <= 0 || ammo != null)
    {
      if (ReloadInternal(ammo))
      {
        ActivateTimer(ignoreTimer);
      }
      else
      {
        Messages.Message("VF_NoAmmoAvailable".Translate(), MessageTypeDefOf.RejectInput);
      }
    }
  }

  /// <summary>
  /// Automatically reload magazine of VehicleTurret with first Ammo Type in inventory
  /// </summary>
  /// <returns>True if Cannon has been successfully reloaded.</returns>
  public virtual bool AutoReload()
  {
    ThingDef ammoType = vehicle.inventory.innerContainer.FirstOrDefault(t =>
        def.ammunition.Allows(t) || def.ammunition.Allows(t.def.projectileWhenLoaded))
    ?.def;
    if (ammoType != null)
    {
      return ReloadInternal(ammoType);
    }

    Debug.Warning($"Failed to auto-reload {def.label}");
    return false;
  }

  public void SetMagazineCount(int count)
  {
    shellCount = Mathf.Clamp(count, 0, def.magazineCapacity);
  }

  protected bool ReloadInternal(ThingDef ammo)
  {
    try
    {
      if (vehicle.inventory.innerContainer.Contains(savedAmmoType) ||
        vehicle.inventory.innerContainer.Contains(ammo))
      {
        //Remembers previously stored ammo for auto-loading feature
        Thing storedAmmo;
        if (ammo != null)
        {
          storedAmmo = vehicle.inventory.innerContainer.FirstOrFallback(x => x.def == ammo);
          savedAmmoType = ammo;
          TryClearChamber();
        }
        else if (savedAmmoType != null)
        {
          storedAmmo =
            vehicle.inventory.innerContainer.FirstOrFallback(x => x.def == savedAmmoType);
        }
        else
        {
          Log.Error("No saved or specified shell upon reload");
          return false;
        }

        int countToRefill = def.magazineCapacity - shellCount;
        int countToTake = Mathf.CeilToInt(countToRefill * def.chargePerAmmoCount);
        int countRefilled = 0;

        thingsToTakeReloading.Clear();
        {
          //Deterine which items (and how much) to take
          foreach (Thing thing in vehicle.inventory.innerContainer)
          {
            if (thing.def == storedAmmo.def)
            {
              int availableCount = thing.stackCount -
                thing.stackCount % Mathf.CeilToInt(def.chargePerAmmoCount);
              int takingFromThing = Mathf.Min(countToTake, availableCount);
              thingsToTakeReloading.Add((thing, takingFromThing));
              countToTake -= takingFromThing;
              if (countToTake <= 0) break;
            }
          }

          //Quick check to make sure to not even bother removing items from inventory if there is not enough to reload 1 shot minimum
          if (thingsToTakeReloading.Sum(pair => pair.Item2) < def.chargePerAmmoCount)
          {
            return false;
          }

          //Take items from inventory without going over the amount required
          for (int i = thingsToTakeReloading.Count - 1; i >= 0; i--)
          {
            if (thingsToTakeReloading.Sum(pair => pair.Item2) < def.chargePerAmmoCount)
            {
              break;
            }

            (Thing thing, int count) = thingsToTakeReloading[i];
            countRefilled += count;
            vehicle.TakeFromInventory(thing, count);
            thingsToTakeReloading.RemoveAt(i);
          }
        }
        thingsToTakeReloading.Clear();

        if (countRefilled % def.chargePerAmmoCount != 0)
        {
          Log.Warning(
            $"Taking more than necessary to reload {this}. This is not supposed to occur. CountRefilled={countRefilled} CountNeeded={countToRefill * def.chargePerAmmoCount}");
        }

        loadedAmmo = storedAmmo.def;
        shellCount = Mathf.CeilToInt(countRefilled / def.chargePerAmmoCount)
         .Clamp(0, def.magazineCapacity);
        EventRegistry[VehicleTurretEventDefOf.Reload].ExecuteEvents();
        def.reloadSound?.PlayOneShot(new TargetInfo(vehicle.Position, vehicle.Map));
      }
    }
    catch (Exception ex)
    {
      Log.Error(
        $"Unable to reload Cannon: {uniqueID} on Pawn: {vehicle.LabelShort}. Exception: {ex}");
      return false;
    }

    return true;
  }

  public void ConsumeChamberedShot()
  {
    shellCount--;
    if (shellCount <= 0 &&
      vehicle.inventory.innerContainer.FirstOrFallback(x => x.def == loadedAmmo) is null)
    {
      loadedAmmo = null;
      shellCount = 0;
    }
  }

  public virtual void TryClearChamber()
  {
    if (loadedAmmo != null && shellCount > 0)
    {
      Thing thing = ThingMaker.MakeThing(loadedAmmo);
      thing.stackCount = Mathf.CeilToInt(shellCount * def.chargePerAmmoCount);
      //vehicle.inventory.innerContainer.TryAdd(thing);
      vehicle.AddOrTransfer(thing);
      loadedAmmo = null;
      shellCount = 0;
      ActivateTimer(true);
    }
  }

  public void CycleFireMode()
  {
    SoundDefOf.Click.PlayOneShotOnCamera(vehicle.Map);
    currentFireMode++;
    if (currentFireMode >= def.fireModes.Count)
    {
      currentFireMode = 0;
    }
  }

  public virtual void SwitchAutoTarget()
  {
    if (CanAutoTarget)
    {
      SoundDefOf.Click.PlayOneShotOnCamera(vehicle.Map);
      AutoTarget = !AutoTarget;
      SetTarget(LocalTargetInfo.Invalid);
      if (AutoTarget)
      {
        StartTicking();
      }
    }
    else
    {
      Messages.Message("VF_AutoTargetingDisabled".Translate(), MessageTypeDefOf.RejectInput);
    }
  }

  public virtual void SetTarget(LocalTargetInfo target)
  {
    targetInfo = target;
    TargetLocked = false;
    if (target.Pawn is { } pawn)
    {
      if (pawn.Downed)
      {
        CachedPawnTargetStatus = PawnStatusOnTarget.Down;
      }
      else if (pawn.Dead)
      {
        CachedPawnTargetStatus = PawnStatusOnTarget.Dead;
      }
      else
      {
        CachedPawnTargetStatus = PawnStatusOnTarget.Alive;
      }
    }
    else
    {
      CachedPawnTargetStatus = PawnStatusOnTarget.None;
    }

    if (targetInfo.IsValid)
    {
      StartTicking();
    }
  }

  /// <summary>
  /// Set target only if cannonTarget is no longer valid or if target is cell based
  /// </summary>
  /// <returns>true if cannonTarget set to target, false if target is still valid</returns>
  public virtual bool CheckTargetInvalid(bool resetPrefireTimer = true)
  {
    if (targetInfo.IsValid && (targetInfo.HasThing || FullAuto))
    {
      if (targetInfo.Pawn != null)
      {
        if ((targetInfo.Pawn.Dead && CachedPawnTargetStatus != PawnStatusOnTarget.Dead) ||
          (targetInfo.Pawn.Downed && CachedPawnTargetStatus != PawnStatusOnTarget.Down))
        {
          SetTarget(LocalTargetInfo.Invalid);
          return true;
        }
      }
      else if (targetInfo.Thing is { HitPoints: <= 0 })
      {
        SetTarget(LocalTargetInfo.Invalid);
        return true;
      }

      return false;
    }

    return false;
  }

  public void ResetAngle()
  {
    TurretRotationTargeted = TurretRotation;
  }

  public void FlagForAlignment()
  {
    TurretRotationTargeted =
      TurretRotationFor(vehicle.FullRotation, defaultAngleRotated.ClampAndWrap(0, 360));
  }

  public virtual void ResetPrefireTimer()
  {
    PrefireTickCount = WarmupTicks;
    EventRegistry[VehicleTurretEventDefOf.Warmup].ExecuteEvents();
    burstsTillWarmup = CurrentFireMode.burstsTillWarmup;
  }

  public void UpdateRotationLock()
  {
    if (vehicle != null)
    {
      if (!targetInfo.IsValid && TurretTargeter.Turret != this &&
        !vehicle.CompVehicleTurrets.Deploying)
      {
        float angleDifference = vehicle.Angle - parentAngleCached;
        if (attachedTo is null)
        {
          transform.rotation +=
            90 * (vehicle.Rotation.AsInt - parentRotCached.AsInt) + angleDifference;
        }
        TurretRotationTargeted = transform.rotation;
      }
      parentRotCached = vehicle.Rotation;
      parentAngleCached = vehicle.Angle;
    }
  }

  public virtual string GetUniqueLoadID()
  {
    return "VehicleTurretGroup_" + uniqueID;
  }

  public override string ToString()
  {
    return $"{def}_{GetUniqueLoadID()}";
  }

  public bool ContainsAmmoDefOrShell(ThingDef ammoDef)
  {
    ThingDef projectile = null;
    if (ammoDef.projectileWhenLoaded != null)
    {
      projectile = ammoDef.projectileWhenLoaded;
    }

    return def.ammunition.Allows(ammoDef) || def.ammunition.Allows(projectile);
  }

  public virtual IEnumerable<string> ConfigErrors(VehicleDef vehicleDef)
  {
    if (def is null)
    {
      yield return "<field>def</field> is a required field for <type>VehicleTurret</type>."
       .ConvertRichText();
    }

    if (string.IsNullOrEmpty(key))
    {
      yield return "<field>key</field> must be included for each <type>VehicleTurret</type>"
       .ConvertRichText();
    }

    if (vehicleDef.GetCompProperties<CompProperties_VehicleTurrets>().turrets.Select(x => x.key)
     .GroupBy(y => y).Where(y => y.Count() > 1).Select(z => z.Key).NotNullAndAny())
    {
      yield return $"Duplicate turret key {key}";
    }
  }

  public virtual void OnDestroy()
  {
    RGBMaterialPool.Release(this);
    if (!turretGraphics.NullOrEmpty())
    {
      foreach (TurretDrawData turretDrawData in turretGraphics)
      {
        RGBMaterialPool.Release(turretDrawData);
      }
    }
  }

  public virtual void ExposeData()
  {
    Scribe_Values.Look(ref autoTargetingActive, nameof(autoTargetingActive));

    Scribe_Values.Look(ref reloadTicks, nameof(reloadTicks));
    Scribe_Values.Look(ref burstTicks, nameof(burstTicks));

    Scribe_Values.Look(ref uniqueID, nameof(uniqueID), defaultValue: -1);
    Scribe_Values.Look(ref key, nameof(key));
    Scribe_Values.Look(ref upgradeKey, nameof(upgradeKey), forceSave: true);

    Scribe_Defs.Look(ref def, nameof(def));
    Scribe_Deep.Look(ref transform, nameof(transform));

    Scribe_Values.Look(ref targetPersists, nameof(targetPersists));
    Scribe_Values.Look(ref autoTargeting, nameof(autoTargeting));
    Scribe_Values.Look(ref manualTargeting, nameof(manualTargeting));

    Scribe_Values.Look(ref queuedToFire, nameof(queuedToFire));
    Scribe_Values.Look(ref currentFireMode, nameof(currentFireMode));
    Scribe_Values.Look(ref currentHeatRate, nameof(currentHeatRate));
    Scribe_Values.Look(ref triggeredCooldown, nameof(triggeredCooldown));
    Scribe_Values.Look(ref ticksSinceLastShot, nameof(ticksSinceLastShot));
    Scribe_Values.Look(ref burstsTillWarmup, nameof(burstsTillWarmup));

    Scribe_Values.Look(ref restrictedTheta, nameof(restrictedTheta),
      defaultValue: (int)Mathf.Abs(angleRestricted.x - (angleRestricted.y + 360)).ClampAngle());

    Scribe_Defs.Look(ref loadedAmmo, nameof(loadedAmmo));
    Scribe_Defs.Look(ref savedAmmoType, nameof(savedAmmoType));
    Scribe_Values.Look(ref shellCount, nameof(shellCount));
    Scribe_Values.Look(ref gizmoLabel, nameof(gizmoLabel));

    Scribe_TargetInfo.Look(ref targetInfo, nameof(targetInfo),
      defaultValue: LocalTargetInfo.Invalid);

    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
      parentRotCached = vehicle.Rotation;
      parentAngleCached = vehicle.Angle;
      if (targetInfo.IsValid)
      {
        AlignToTargetRestricted(); //reassigns rotationTargeted for turrets currently turning
      }

      InitRecoilTrackers();
    }
  }
}