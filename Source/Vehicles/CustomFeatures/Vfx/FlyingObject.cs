using System.Collections.Generic;
using JetBrains.Annotations;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles;

[PublicAPI]
public class FlyingObject : MoteThrown
{
  private readonly List<DrawProps> objects = [];

  public void Add(IParallelRenderer renderer, Rot8 orientation, float rotation)
  {
    objects.Add(new DrawProps(renderer, orientation, rotation));
  }

  public void Launch(Map map, Vector3 rootPos, float rotationRate, float speed, float angle)
  {
    Assert.IsFalse(objects.NullOrEmpty(), "No objects to launch.");
    this.rotationRate = rotationRate;
    exactPosition = rootPos;
    SetVelocity(angle, speed);
    GenSpawn.Spawn(this, rootPos.ToIntVec3(), map);
  }

  protected override void DrawAt(Vector3 drawLoc, bool flip = false)
  {
    exactPosition.y = def.altitudeLayer.AltitudeFor();
    foreach (DrawProps obj in objects)
    {
      TransformData transformData =
        new(exactPosition, obj.orientation, obj.rotation + exactRotation);
      obj.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, transformData, forceDraw: true);
    }
  }

  private readonly struct DrawProps(IParallelRenderer renderer, Rot8 orientation, float rotation)
  {
    public readonly IParallelRenderer renderer = renderer;
    public readonly Rot8 orientation = orientation;
    public readonly float rotation = rotation;
  }
}