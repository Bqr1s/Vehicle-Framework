using JetBrains.Annotations;
using SmashTools;
using Verse;

namespace Vehicles;

[PublicAPI]
public class Reactor_FuelLeak : Reactor, ITweakFields
{
  /// <summary>
  /// Health percent in which the component will start to leak.
  /// </summary>
  [TweakField(SettingsType = UISettingsType.SliderFloat)]
  [SliderValues(MinValue = 0, MaxValue = 1, Increment = 0.05f, RoundDecimalPlaces = 2)]
  [LoadAlias("maxHealth")]
  public float healthPercent = 0.8f;

  /// <summary>
  /// Rate of fuel leak at unit / second.
  /// </summary>
  /// <remarks><see cref="FloatRange.min"/> is the rate at <see cref="healthPercent"/> while <see cref="FloatRange.max"/> is the rate at 0% health.</remarks>
  [TweakField(SettingsType = UISettingsType.FloatBox)]
  [NumericBoxValues(MinValue = 0)]
  public FloatRange rate = new(1, 10);

  string ITweakFields.Category => string.Empty;

  string ITweakFields.Label => nameof(Reactor_FuelLeak);

  public override void Hit(VehiclePawn vehicle, VehicleComponent component, ref DamageInfo dinfo,
    VehicleComponent.Penetration penetration)
  {
  }

  void ITweakFields.OnFieldChanged()
  {
  }
}