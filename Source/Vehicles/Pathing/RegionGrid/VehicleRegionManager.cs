namespace Vehicles;

public abstract class VehicleGridManager
{
  protected readonly VehicleMapping mapping;
  protected internal VehicleDef createdFor;

  protected VehicleGridManager(VehicleMapping mapping, VehicleDef createdFor)
  {
    this.mapping = mapping;
    this.createdFor = createdFor;
  }

  public VehicleDef CreatedFor => createdFor;

  public virtual void PostInit()
  {
  }
}