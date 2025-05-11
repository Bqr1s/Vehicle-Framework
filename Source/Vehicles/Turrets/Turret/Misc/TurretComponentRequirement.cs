using SmashTools;

namespace Vehicles;

public class TurretComponentRequirement
{
  private string key;
  private float healthPercent;

  private VehicleComponent Component { get; set; }

  public string Label => Component.props.label;

  public bool MeetsRequirements { get; private set; } = true;

  public void RecacheComponent(VehiclePawn vehicle)
  {
    Component = vehicle.statHandler.GetComponent(key);
  }

  public void RegisterEvents(VehicleTurret turret)
  {
    turret.vehicle.AddEvent(VehicleEventDefOf.DamageTaken, OnHealthChanged);
    turret.vehicle.AddEvent(VehicleEventDefOf.Repaired, OnHealthChanged);
  }

  private void OnHealthChanged()
  {
    bool oldStatus = MeetsRequirements;
    MeetsRequirements = Component != null && Component.HealthPercent >= healthPercent;
  }

  public static TurretComponentRequirement CopyFrom(TurretComponentRequirement reference)
  {
    return new TurretComponentRequirement
    {
      key = reference.key,
      healthPercent = reference.healthPercent
    };
  }
}