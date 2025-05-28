using SmashTools;

namespace Vehicles;

public class GraphicDataOverlay
{
  public string identifier = null;

  public GraphicDataRGB graphicData;

  [SliderValues(MinValue = 0, MaxValue = 360, RoundDecimalPlaces = 0, Increment = 1)]
  public float rotation = 0;

  public bool dynamicShadows;

  public ComponentRequirement component;

  public bool renderUI = true;
}