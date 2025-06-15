using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using Verse.AI;

namespace Vehicles;

// CE hooks for compatibility
public partial class VehicleTurret
{
  /// <summary>
  /// (projectileDef, ammoDef, AmmoSetDef, origin, intendedTarget, launcher, shotAngle, shotRotation, shotHeight, shotSpeed)
  /// </summary>
  /// <returns>CE projectile</returns>
  public static
    Func<ThingDef, ThingDef, Def, Vector2, LocalTargetInfo, VehiclePawn, float, float, float, float,
      object> LaunchProjectileCE;

  /// <summary>
  /// (velocity, range, shooter, target, origin, flyOverhead, gravityModifier, sway, spread, recoil)
  /// </summary>
  /// <returns>2-angles</returns>
  public static
    Func<float, float, Thing, LocalTargetInfo, Vector3, bool, float, float, float, float, Vector2>
    ProjectileAngleCE;

  /// <summary>
  /// (ammoset name)
  /// </summary>
  /// <returns>AmmoSetDef</returns>
  public static Func<string, Def> LookupAmmosetCE;

  /// <summary>
  /// (projectileDef, ammoDef, AmmoSetDef, turret, recoilAmount)
  /// </summary>
  public static Action<ThingDef, ThingDef, Def, VehicleTurret, float> NotifyShotFiredCE;


  /// <summary>
  /// ammoDef, AmmoSetDef, spread
  /// </summary>
  /// <returns>(projectileCount, spread)</returns>
  public static Func<ThingDef, Def, float, Tuple<int, float>> LookupProjectileCountAndSpreadCE;
}