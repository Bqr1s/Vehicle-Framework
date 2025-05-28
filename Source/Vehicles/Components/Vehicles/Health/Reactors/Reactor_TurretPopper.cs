using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using SmashTools;
using Verse;
using Verse.Sound;

namespace Vehicles;

[PublicAPI]
public class Reactor_TurretPopper : Reactor_Explosive, ITweakFields
{
  public string turretKey;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public float speed = 30;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public FloatRange angle = new(-5, 5);

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public FloatRange rotationRate = new(-35, 35);

  string ITweakFields.Category => string.Empty;

  string ITweakFields.Label => nameof(Reactor_Explosive);

  protected override TimedExplosion CreateExploder(VehiclePawn vehicle, VehicleComponent component)
  {
    TimedExplosion exploder = base.CreateExploder(vehicle, component);
    if (exploder == null)
      return null;
    exploder.explosionCallback = PopTurret;
    return exploder;
  }

  private void PopTurret(TimedExplosion explosion)
  {
    VehiclePawn vehicle = explosion.vehicle;
    VehicleTurret turret = vehicle.CompVehicleTurrets.GetTurret(turretKey);
    if (turret == null)
    {
      Log.Error($"Unable to find {turretKey} for turret pop.");
      return;
    }

    vehicle.statHandler.SetComponentHealth(turret.component.Component.props.key, 0);
    FlyingObject mote =
      (FlyingObject)ThingMaker.MakeThing(ThingDefOf_VehicleMotes.MoteLaunchedTurret);
    // Child turrets launch with the parent as well.
    mote.Add(turret, Rot8.North, turret.TurretRotation);
    foreach (VehicleTurret childTurret in turret.childTurrets)
      mote.Add(childTurret, Rot8.North, childTurret.TurretRotation);
    mote.Launch(vehicle.Map, vehicle.DrawPos, rotationRate.RandomInRange, speed,
      angle.RandomInRange);
  }

  void ITweakFields.OnFieldChanged()
  {
  }

  [DebugAction(VehicleHarmony.VehiclesLabel, "Pop Turret",
    actionType = DebugActionType.ToolMapForPawns)]
  private static void PopVehicleTurret(Pawn pawn)
  {
    if (pawn is not VehiclePawn vehicle || vehicle.CompVehicleTurrets == null)
    {
      SoundDefOf.ClickReject.PlayOneShotOnCamera();
      return;
    }

    foreach (VehicleComponent component in vehicle.statHandler.components)
    {
      if (component.props.reactors.NullOrEmpty())
        continue;

      Reactor_TurretPopper popper =
        (Reactor_TurretPopper)component.props.reactors.FirstOrDefault(
          reactor => reactor is Reactor_TurretPopper);
      if (popper != null)
      {
        // Only need to pop first turret for testing
        popper.SpawnExploder(vehicle, component);
        break;
      }
    }
  }
}