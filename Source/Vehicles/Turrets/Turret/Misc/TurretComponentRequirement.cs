namespace Vehicles;

public class TurretComponentRequirement
{
  private string key;
  private float healthPercent;

  private VehicleComponent Component { get; set; }

  public string Label => Component.props.label;

  public bool MeetsRequirements => Component != null && Component.HealthPercent >= healthPercent;

  public void RecacheComponent(VehiclePawn vehicle)
  {
    Component = vehicle.statHandler.GetComponent(key);
  }

  public static TurretComponentRequirement CopyFrom(TurretComponentRequirement reference)
  {
    return new TurretComponentRequirement()
    {
      key = reference.key,
      healthPercent = reference.healthPercent
    };
  }
}