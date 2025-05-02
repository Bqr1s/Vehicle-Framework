using System.Collections.Generic;
using SmashTools;
using SmashTools.Animations;
using SmashTools.Rendering;
using Verse;

namespace Vehicles;

public sealed class GraphicOverlayRenderer : IParallelRenderer
{
  private readonly VehiclePawn vehicle;

  [AnimationProperty(Name = "Overlays")]
  private readonly List<GraphicOverlay> overlays = [];

  private readonly List<GraphicOverlay> extraOverlays = [];

  private readonly Dictionary<string, List<GraphicOverlay>> extraOverlayLookup = [];

  public GraphicOverlayRenderer(VehiclePawn vehicle)
  {
    this.vehicle = vehicle;
  }

  public List<GraphicOverlay> Overlays => overlays;

  public List<GraphicOverlay> ExtraOverlays => extraOverlays;

  public List<GraphicOverlay> AllOverlaysListForReading { get; } = [];

  private List<GraphicOverlay> RotatorOverlays { get; } = [];

  public void Init()
  {
    if (!vehicle.VehicleDef.drawProperties.overlays.NullOrEmpty())
    {
      overlays.Clear();
      foreach (GraphicDataOverlay graphicDataOverlay in vehicle.VehicleDef.drawProperties
       .graphicOverlays)
      {
        GraphicOverlay graphicOverlay = GraphicOverlay.Create(graphicDataOverlay, vehicle);
        overlays.Add(graphicOverlay);
        AllOverlaysListForReading.Add(graphicOverlay);
      }
      RecacheRotatorOverlays();
    }
  }

  private void RecacheRotatorOverlays()
  {
    RotatorOverlays.Clear();
    foreach (GraphicOverlay overlay in AllOverlaysListForReading)
    {
      if (overlay.Graphic is Graphic_Rotator)
        RotatorOverlays.Add(overlay);
    }
  }

  public void AddOverlay(string key, GraphicOverlay graphicOverlay)
  {
    extraOverlayLookup.AddOrInsert(key, graphicOverlay);
    extraOverlays.Add(graphicOverlay);
    AllOverlaysListForReading.Add(graphicOverlay);
    RecacheRotatorOverlays();
  }

  public void RemoveOverlays(string key)
  {
    if (extraOverlayLookup.ContainsKey(key))
    {
      foreach (GraphicOverlay graphicOverlay in extraOverlayLookup[key])
      {
        extraOverlays.Remove(graphicOverlay);
        AllOverlaysListForReading.Remove(graphicOverlay);
        graphicOverlay.Destroy();
      }
      extraOverlayLookup.Remove(key);
      RecacheRotatorOverlays();
    }
  }

  public void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData)
  {
    foreach (GraphicOverlay graphicOverlay in AllOverlaysListForReading)
    {
      graphicOverlay.DynamicDrawPhaseAt(phase, transformData);
    }
  }

  // Right now this is strictly for the old animation system which has applies the same
  // rotation rates for all Graphic_Rotator overlays. The new animator will remove the necessity
  // for all of this and directly right to the transform / acceleration rate of the overlay.
  public void AddRotation(float rotation)
  {
    foreach (GraphicOverlay graphicOverlay in RotatorOverlays)
    {
      graphicOverlay.acceleration +=
        ((Graphic_Rotator)graphicOverlay.Graphic).ModifyIncomingRotation(rotation);
    }
  }
}