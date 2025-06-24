using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Vehicles;

public static class VehicleDrawBatcher
{
  private static readonly Color32[] DefaultColors =
  [
    new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
    new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
    new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
    new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)
  ];

  private static readonly Vector2[] DefaultUvs =
  [
    new(0f, 0f),
    new(0f, 1f),
    new(1f, 1f),
    new(1f, 0f)
  ];

  private static readonly Vector2[] DefaultUvsFlipped =
  [
    new(1f, 0f),
    new(1f, 1f),
    new(0f, 1f),
    new(0f, 0f)
  ];

  public static void Batch(MapDrawLayer layer, Vector3 center, Vector2 size, Material mat,
    float rot = 0f, bool flipUv = false, Vector2[] uvs = null, Color32[] colors = null,
    float topVerticesAltitudeBias = 0.01f, float uvzPayload = 0f)
  {
    colors ??= DefaultColors;
    uvs ??= flipUv ? DefaultUvsFlipped : DefaultUvs;

    LayerSubMesh sm = layer.GetSubMesh(mat);
    int vertIndex = sm.verts.Count;
    sm.verts.Add(new Vector3(-0.5f * size.x, 0f, -0.5f * size.y));
    sm.verts.Add(new Vector3(-0.5f * size.x, topVerticesAltitudeBias, 0.5f * size.y));
    sm.verts.Add(new Vector3(0.5f * size.x, topVerticesAltitudeBias, 0.5f * size.y));
    sm.verts.Add(new Vector3(0.5f * size.x, 0f, -0.5f * size.y));
    bool flag = rot != 0f;
    if (flag)
    {
      float rotRad = rot * 0.017453292f;
      rotRad *= -1f;
      for (int i = 0; i < 4; i++)
      {
        float x = sm.verts[vertIndex + i].x;
        float z = sm.verts[vertIndex + i].z;
        float cosR = Mathf.Cos(rotRad);
        float sinR = Mathf.Sin(rotRad);
        float newX = x * cosR - z * sinR;
        float newZ = x * sinR + z * cosR;
        sm.verts[vertIndex + i] = new Vector3(newX, sm.verts[vertIndex + i].y, newZ);
      }
    }
    for (int j = 0; j < 4; j++)
    {
      List<Vector3> verts = sm.verts;
      int index = vertIndex + j;
      verts[index] += center;
      sm.uvs.Add(new Vector3(uvs[j].x, uvs[j].y, uvzPayload));
      sm.colors.Add(colors[j]);
    }
    sm.tris.Add(vertIndex);
    sm.tris.Add(vertIndex + 1);
    sm.tris.Add(vertIndex + 2);
    sm.tris.Add(vertIndex);
    sm.tris.Add(vertIndex + 2);
    sm.tris.Add(vertIndex + 3);
  }
}