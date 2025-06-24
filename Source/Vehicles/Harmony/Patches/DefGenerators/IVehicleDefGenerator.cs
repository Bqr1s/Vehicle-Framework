using Verse;

namespace Vehicles;

public interface IVehicleDefGenerator<T> where T : Def
{
  bool TryGenerateImpliedDef(VehicleDef vehicleDef, out T impliedDef, bool hotReload);
}