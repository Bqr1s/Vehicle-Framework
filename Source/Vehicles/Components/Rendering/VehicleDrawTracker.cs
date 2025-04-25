using SmashTools;
using SmashTools.Rendering;
using UnityEngine;

namespace Vehicles
{
  public class VehicleDrawTracker
  {
    private readonly VehiclePawn vehicle;
    private readonly VehicleRenderer renderer;
    public readonly VehicleTweener tweener;

    // TODO - Reimplement for vehicle specific "footprints"
    public VehicleTrackMaker trackMaker;
    public Vehicle_RecoilTracker recoilTracker;

    public VehicleDrawTracker(VehiclePawn vehicle)
    {
      this.vehicle = vehicle;
      tweener = new VehicleTweener(vehicle);
      renderer = new VehicleRenderer(vehicle);
      trackMaker = new VehicleTrackMaker(vehicle);
      recoilTracker = new Vehicle_RecoilTracker();
    }

    public Vector3 DrawPos
    {
      get
      {
        tweener.PreDrawPosCalculation();
        Vector3 vector = tweener.TweenedPos;
        vector.y = vehicle.def.Altitude;

        if (recoilTracker.Recoil > 0f)
        {
          vector = vector.PointFromAngle(recoilTracker.Recoil, recoilTracker.Angle);
        }
        return vector;
      }
    }

    public void ProcessPostTickVisuals(int ticksPassed)
    {
      if (!vehicle.Spawned)
        return;
      renderer.ProcessPostTickVisuals(ticksPassed);
      trackMaker.ProcessPostTickVisuals(ticksPassed);
      recoilTracker.ProcessPostTickVisuals(ticksPassed);
    }

    public void Draw(ref readonly TransformData transform)
    {
      renderer.RenderVehicle(in transform);
    }

    public void Notify_Spawned()
    {
      tweener.ResetTweenedPosToRoot();
    }
  }
}