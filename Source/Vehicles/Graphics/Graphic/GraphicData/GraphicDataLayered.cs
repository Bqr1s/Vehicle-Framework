using DevTools;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace Vehicles;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class GraphicDataLayered : GraphicData
{
  public const int SubLayerCount = 10;

  // Simple conversion factor for layering a sprite relative to the
  // vehicle's body position. Layer is relative to SubLayerCount.
  private int layer;

  private Vector3 originalDrawOffset;
  private Vector3? originalDrawOffsetNorth;
  private Vector3? originalDrawOffsetEast;
  private Vector3? originalDrawOffsetSouth;
  private Vector3? originalDrawOffsetWest;

  public bool AboveBody => layer >= 0;

  public virtual void CopyFrom(GraphicDataLayered graphicData)
  {
    base.CopyFrom(graphicData);
    layer = graphicData.layer;
    CacheDrawOffsets();
    RecacheLayerOffsets();
  }

  public virtual void Init(IMaterialCacheTarget target)
  {
    RecacheLayerOffsets();
  }

  private void CacheDrawOffsets()
  {
    originalDrawOffset = drawOffset;
    originalDrawOffsetNorth = drawOffsetNorth;
    originalDrawOffsetEast = drawOffsetEast;
    originalDrawOffsetSouth = drawOffsetSouth;
    originalDrawOffsetWest = drawOffsetWest;
  }

  public void RecacheLayerOffsets()
  {
    if (layer == 0)
      return;

    float layerOffset = layer * (Altitudes.AltInc / SubLayerCount);

    drawOffset = originalDrawOffset;
    drawOffset.y += layerOffset;

    if (drawOffsetNorth != null)
    {
      Assert.IsNotNull(originalDrawOffsetNorth);
      drawOffsetNorth = originalDrawOffsetNorth.Value;
      drawOffsetNorth = new Vector3(drawOffsetNorth.Value.x, drawOffsetNorth.Value.y + layerOffset,
        drawOffsetNorth.Value.z);
    }

    if (drawOffsetEast != null)
    {
      Assert.IsNotNull(originalDrawOffsetEast);
      drawOffsetEast = originalDrawOffsetEast.Value;
      drawOffsetEast = new Vector3(drawOffsetEast.Value.x, drawOffsetEast.Value.y + layerOffset,
        drawOffsetEast.Value.z);
    }

    if (drawOffsetSouth != null)
    {
      Assert.IsNotNull(originalDrawOffsetSouth);
      drawOffsetSouth = originalDrawOffsetSouth.Value;
      drawOffsetSouth = new Vector3(drawOffsetSouth.Value.x, drawOffsetSouth.Value.y + layerOffset,
        drawOffsetSouth.Value.z);
    }

    if (drawOffsetWest != null)
    {
      Assert.IsNotNull(originalDrawOffsetWest);
      drawOffsetWest = originalDrawOffsetWest.Value;
      drawOffsetWest = new Vector3(drawOffsetWest.Value.x, drawOffsetWest.Value.y + layerOffset,
        drawOffsetWest.Value.z);
    }
  }
}