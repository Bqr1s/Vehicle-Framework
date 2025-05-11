using System;
using System.Collections.Generic;
using DevTools.UnitTesting;
using SmashTools.Performance;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace SmashTools.UnitTesting;

[UnitTest(TestType.MainMenu)]
[TestDescription("Render texture utils.")]
internal class UnitTest_RenderTexture
{
  private readonly List<RenderTexture> objectsToDestroy = [];

  private RenderTexture renderTexture;

  [SetUp]
  private void ClearObjectList()
  {
    objectsToDestroy.Clear();
  }

  [Test, ExecutionPriority(Priority.First)]
  private void CreateFormatted()
  {
    renderTexture = RenderTextureUtil.CreateRenderTexture(2, 2);

    Expect.IsTrue(renderTexture.IsCreated(), "RenderTexture GPU Allocated");
    Expect.AreEqual(renderTexture.depth, 0, "No Depth");
    Expect.IsTrue(SystemInfo.SupportsRenderTextureFormat(renderTexture.format),
      "RenderTexture Format Supported");

    Expect.Throws<ArgumentException>(delegate { _ = RenderTextureUtil.CreateRenderTexture(0, 0); },
      "Invalid RenderTexture Throws");
  }

  [Test]
  private void Buffer()
  {
    RenderTexture rtA = RenderTextureUtil.CreateRenderTexture(2, 2);
    RenderTexture rtB = RenderTextureUtil.CreateRenderTexture(2, 2);
    RenderTextureBuffer buffer = new(rtA, rtB);

    Assert.IsTrue(rtA.IsCreated());
    Assert.IsTrue(rtB.IsCreated());

    Expect.ReferencesAreEqual(buffer.Read, rtA, "Read No Swap Before");
    Expect.ReferencesAreEqual(buffer.Read, rtA, "Read No Swap After");
    Expect.ReferencesAreEqual(buffer.GetWrite(), rtB, "GetWrite Swap and Return");
    Expect.ReferencesAreEqual(buffer.Write, rtA, "Write Swapped");
    Expect.ReferencesAreEqual(buffer.Read, rtB, "Read Swapped");
    Expect.ReferencesAreNotEqual(buffer.Read, buffer.Write, "No Overwritten Assignment");
    _ = buffer.GetWrite();
    Expect.ReferencesAreEqual(buffer.Read, rtA, "Swap 2 Reset Read");
    Expect.ReferencesAreEqual(buffer.Write, rtB, "Swap 2 Reset Write");

    // Dispose will queue the texture object for destruction but we still have 1 frame to validate
    // GPU allocations were released.
    buffer.Dispose();

    Expect.AreEqual(buffer.Read.GetNativeDepthBufferPtr(), IntPtr.Zero, "Read GPU Memory Freed");
    Expect.AreEqual(buffer.Write.GetNativeDepthBufferPtr(), IntPtr.Zero, "Write GPU Memory Freed");
    Expect.AreEqual(rtA.GetNativeDepthBufferPtr(), IntPtr.Zero, "rtA GPU Memory Freed");
    Expect.AreEqual(rtB.GetNativeDepthBufferPtr(), IntPtr.Zero, "rtB GPU Memory Freed");
  }

  [Test]
  private void Idler()
  {
    const float ExpiryTime = 999; // seconds

    using (new Test.Group("Texture"))
    {
      RenderTextureIdler idler = new(RenderTextureUtil.CreateRenderTexture(2, 2), ExpiryTime);
      Expect.IsTrue(UnityThread.InUpdateQueue(idler.UpdateLoop), "Idler Timer Started");
      Expect.IsTrue(idler.Read.IsCreated(), "Read Allocated");
      Expect.ReferencesAreEqual(idler.Read, idler.GetWrite(), "Read/Write Equal");

      idler.SetTimeDirect(100);
      bool continue100 = idler.UpdateLoop();
      Expect.IsTrue(continue100, "UpdateLoop Continuing");
      Expect.IsTrue(idler.Read.IsCreated(), "SetTimer 100 Read Retained");

      idler.SetTimeDirect(9999);
      bool continue9999 = idler.UpdateLoop();
      Expect.IsFalse(continue9999, "UpdateLoop Stopping");
      UnityThread.RemoveUpdate(idler.UpdateLoop);
      Expect.AreEqual(idler.Read.GetNativeDepthBufferPtr(), IntPtr.Zero, "Idler GPU Memory Freed");
    }
    using (new Test.Group("Buffer"))
    {
      RenderTexture rtA = RenderTextureUtil.CreateRenderTexture(2, 2);
      RenderTexture rtB = RenderTextureUtil.CreateRenderTexture(2, 2);
      RenderTextureBuffer buffer = new(rtA, rtB);
      RenderTextureIdler idler = new(buffer, ExpiryTime);

      Expect.IsTrue(UnityThread.InUpdateQueue(idler.UpdateLoop), "Idler Timer Started");
      Expect.IsTrue(idler.Read.IsCreated(), "Read Allocated");
      Expect.IsTrue(idler.Write.IsCreated(), "Write Allocated");
      Expect.ReferencesAreNotEqual(idler.Read, idler.GetWrite(), "Read/Write NotEqual");

      idler.SetTimeDirect(100);
      bool continue100 = idler.UpdateLoop();
      Expect.IsTrue(continue100, "UpdateLoop Continuing");
      Expect.IsTrue(idler.Read.IsCreated(), "SetTimer 100 Read Retained");
      Expect.IsTrue(idler.Write.IsCreated(), "SetTimer 100 Write Retained");

      idler.SetTimeDirect(9999);
      bool continue9999 = idler.UpdateLoop();
      Expect.IsFalse(continue9999, "UpdateLoop Stopping");
      // If continue9999 is false, Dispose will be called but we need to call DestroyImmediately for
      // this test so we don't have to wait another frame to check it was freed natively.
      UnityThread.RemoveUpdate(idler.UpdateLoop);

      Expect.AreEqual(idler.Read.GetNativeDepthBufferPtr(), IntPtr.Zero,
        "Idler Read GPU Memory Freed");
      Expect.AreEqual(idler.Write.GetNativeDepthBufferPtr(), IntPtr.Zero,
        "Idler Write GPU Memory Freed");
    }
  }

  [TearDown]
  private void DestroyAllObjects()
  {
    renderTexture.Release();
    Object.Destroy(renderTexture);
    renderTexture = null;

    for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
    {
      RenderTexture rt = objectsToDestroy[i];
      // RenderTextures should already be destroyed here but by gathering all newly instantiated
      // test objects separate from the object pool we can verify independently that all
      // objects created from the object pool will be destroyed when dumped.
      Expect.IsNull(rt);
      if (rt)
      {
        rt?.Release();
        Object.Destroy(rt);
      }
    }
    objectsToDestroy.Clear();
  }
}