using System.Collections.Generic;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles.Rendering;

public static class RenderHelper
{
  private static readonly List<PlanetTile> cachedEdgeTiles = [];

  private static int cachedEdgeTilesForCenter = -1;
  private static int cachedEdgeTilesForRadius = -1;
  private static int cachedEdgeTilesForWorldSeed = -1;

  public static void DrawLinesBetweenTargets(VehiclePawn vehicle, Job curJob, JobQueue jobQueue)
  {
    Vector3 a = vehicle.Position.ToVector3Shifted();
    if (vehicle.vehiclePather.curPath != null)
    {
      a = vehicle.vehiclePather.Destination.CenterVector3;
    }
    else if (curJob != null && curJob.targetA.IsValid && (!curJob.targetA.HasThing ||
      (curJob.targetA.Thing.Spawned && curJob.targetA.Thing != vehicle &&
        curJob.targetA.Thing.Map == vehicle.Map)))
    {
      GenDraw.DrawLineBetween(a, curJob.targetA.CenterVector3, AltitudeLayer.Item.AltitudeFor());
      a = curJob.targetA.CenterVector3;
    }
    for (int i = 0; i < jobQueue.Count; i++)
    {
      if (jobQueue[i].job.targetA.IsValid)
      {
        if (!jobQueue[i].job.targetA.HasThing || (jobQueue[i].job.targetA.Thing.Spawned &&
          jobQueue[i].job.targetA.Thing.Map == vehicle.Map))
        {
          Vector3 centerVector = jobQueue[i].job.targetA.CenterVector3;
          GenDraw.DrawLineBetween(a, centerVector, AltitudeLayer.Item.AltitudeFor());
          a = centerVector;
        }
      }
      else
      {
        List<LocalTargetInfo> targetQueueA = jobQueue[i].job.targetQueueA;
        if (targetQueueA != null)
        {
          for (int j = 0; j < targetQueueA.Count; j++)
          {
            if (!targetQueueA[j].HasThing || (targetQueueA[j].Thing.Spawned &&
              targetQueueA[j].Thing.Map == vehicle.Map))
            {
              Vector3 centerVector2 = targetQueueA[j].CenterVector3;
              GenDraw.DrawLineBetween(a, centerVector2, AltitudeLayer.Item.AltitudeFor());
              a = centerVector2;
            }
          }
        }
      }
    }
  }

  /// <summary>
  /// Allow for optional overriding of mote saturation on map while being able to throw any MoteThrown <paramref name="mote"/>
  /// </summary>
  /// <seealso cref="MoteThrown"/>
  /// <param name="loc"></param>
  /// <param name="map"></param>
  /// <param name="mote"></param>
  /// <param name="overrideSaturation"></param>
  public static Mote ThrowMoteEnhanced(Vector3 loc, Map map, MoteThrown mote,
    bool overrideSaturation = false)
  {
    if (!loc.ShouldSpawnMotesAt(map) || (overrideSaturation && map.moteCounter.Saturated))
    {
      return null;
    }

    GenSpawn.Spawn(mote, loc.ToIntVec3(), map, WipeMode.Vanish);
    return mote;
  }

  /// <summary>
  /// Create rotated Mesh where <paramref name="rot"/> [1:3] indicates number of 90 degree rotations
  /// </summary>
  /// <param name="size"></param>
  /// <param name="rot"></param>
  public static Mesh NewPlaneMesh(Vector2 size, int rot)
  {
    Vector3[] vertices = new Vector3[4];
    Vector2[] uv = new Vector2[4];
    int[] triangles = new int[6];
    vertices[0] = new Vector3(-0.5f * size.x, 0f, -0.5f * size.y);
    vertices[1] = new Vector3(-0.5f * size.x, 0f, 0.5f * size.y);
    vertices[2] = new Vector3(0.5f * size.x, 0f, 0.5f * size.y);
    vertices[3] = new Vector3(0.5f * size.x, 0f, -0.5f * size.y);
    switch (rot)
    {
      case 1:
        uv[0] = new Vector2(1f, 0f);
        uv[1] = new Vector2(0f, 0f);
        uv[2] = new Vector2(0f, 1f);
        uv[3] = new Vector2(1f, 1f);
        break;
      case 2:
        uv[0] = new Vector2(1f, 1f);
        uv[1] = new Vector2(1f, 0f);
        uv[2] = new Vector2(0f, 0f);
        uv[3] = new Vector2(0f, 1f);
        break;
      case 3:
        uv[0] = new Vector2(0f, 1f);
        uv[1] = new Vector2(1f, 1f);
        uv[2] = new Vector2(1f, 0f);
        uv[3] = new Vector2(0f, 0f);
        break;
      default:
        uv[0] = new Vector2(0f, 0f);
        uv[1] = new Vector2(0f, 1f);
        uv[2] = new Vector2(1f, 1f);
        uv[3] = new Vector2(1f, 0f);
        break;
    }
    triangles[0] = 0;
    triangles[1] = 1;
    triangles[2] = 2;
    triangles[3] = 0;
    triangles[4] = 2;
    triangles[5] = 3;
    Mesh mesh = new Mesh();
    mesh.name = "NewPlaneMesh()";
    mesh.vertices = vertices;
    mesh.uv = uv;
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    return mesh;
  }

  /// <summary>
  /// Create mesh with varying length of vertices rather than being restricted to 4
  /// </summary>
  /// <param name="size"></param>
  public static Mesh NewTriangleMesh(Vector2 size)
  {
    Vector3[] vertices = new Vector3[3];
    Vector2[] uv = new Vector2[3];
    int[] triangles = new int[3];

    vertices[0] = new Vector3(-0.5f * size.x, 0, 1 * size.y);
    vertices[1] = new Vector3(0.5f * size.x, 0, 1 * size.y);
    vertices[2] = new Vector3(0, 0, 0);

    uv[0] = vertices[0];
    uv[1] = vertices[1];
    uv[2] = vertices[2];

    triangles[0] = 0;
    triangles[1] = 1;
    triangles[2] = 2;

    Mesh mesh = new Mesh();
    mesh.name = "TriangleMesh";
    mesh.vertices = vertices;
    mesh.uv = uv;
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    return mesh;
  }

  /// <summary>
  /// Create triangle mesh with a cone like arc for an FOV effect
  /// </summary>
  /// <remarks><paramref name="arc"/> should be within [0:360]</remarks>
  /// <param name="size"></param>
  /// <param name="arc"></param>
  public static Mesh NewConeMesh(float distance, int arc)
  {
    float currentAngle = arc / -2f;
    Vector3[] vertices = new Vector3[arc + 2];
    Vector2[] uv = new Vector2[vertices.Length];
    int[] triangles = new int[arc * 3];

    vertices[0] = Vector3.zero;
    uv[0] = Vector3.zero;
    int t = 0;
    for (int i = 1; i <= arc; i++)
    {
      vertices[i] = vertices[0].PointFromAngle(distance, currentAngle);
      uv[i] = vertices[i];
      currentAngle += 1;

      triangles[t] = 0;
      triangles[t + 1] = i;
      triangles[t + 2] = i + 1;
      t += 3;
    }

    Mesh mesh = new Mesh();
    mesh.name = "ConeMesh";
    mesh.vertices = vertices;
    mesh.uv = uv;
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    return mesh;
  }

  /// <summary>
  /// Draw ring around edge tile cells given <paramref name="center"/> and <paramref name="radius"/>
  /// </summary>
  /// <param name="center"></param>
  /// <param name="radius"></param>
  /// <param name="material"></param>
  public static void DrawWorldRadiusRing(PlanetTile center, int radius, Material material)
  {
    if (radius < 0)
    {
      return;
    }
    if (cachedEdgeTilesForCenter != center || cachedEdgeTilesForRadius != radius ||
      cachedEdgeTilesForWorldSeed != Find.World.info.Seed)
    {
      cachedEdgeTilesForCenter = center;
      cachedEdgeTilesForRadius = radius;
      cachedEdgeTilesForWorldSeed = Find.World.info.Seed;
      cachedEdgeTiles.Clear();
      center.Layer.Filler.FloodFill(center, _ => true, delegate(PlanetTile tile, int dist)
      {
        if (dist > radius + 1)
        {
          return true;
        }
        if (dist == radius + 1)
        {
          cachedEdgeTiles.Add(tile);
        }
        return false;
      });

      WorldGrid worldGrid = Find.WorldGrid;
      Vector3 c = worldGrid.GetTileCenter(center);
      Vector3 n = c.normalized;
      cachedEdgeTiles.Sort(delegate(PlanetTile a, PlanetTile b)
      {
        float num = Vector3.Dot(n,
          Vector3.Cross(worldGrid.GetTileCenter(a) - c, worldGrid.GetTileCenter(b) - c));
        if (Mathf.Abs(num) < 0.0001f)
        {
          return 0;
        }
        if (num < 0f)
        {
          return -1;
        }
        return 1;
      });
    }
    GenDraw.DrawWorldLineStrip(cachedEdgeTiles, material, 5f);
  }
}