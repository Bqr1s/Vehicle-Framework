using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DevTools;
using DevTools.UnitTesting;

namespace SmashTools.UnitTesting;

[Disabled]
[UnitTest(TestType.MainMenu)]
[TestDescription("Group of tests to verify Ludeon's code is not buggy.")]
internal sealed class UnitTest_LudeonCode
{
  [Test]
  private void DirectXmlLoader_XmlAssetsInModFolder()
  {
    const int TaskCount = 10;
    const int WorkSim = 100; // ms

    using SemaphoreSlim semaphore = new(Math.Min(Environment.ProcessorCount, 4));
    DevLog.WriteVerbose($"Processing {TaskCount} items on {semaphore.CurrentCount} threads.");
    DevLog.WriteVerbose($"Expected Estimate: {TaskCount * WorkSim / semaphore.CurrentCount}");
    Task[] tasks = new Task[TaskCount];
    for (int i = 0; i < tasks.Length; i++)
    {
      // ReSharper disable AccessToDisposedClosure
      int label = i;
      tasks[i] = Task.Run(async delegate
      {
        await semaphore.WaitAsync();
        try
        {
          await Task.Delay(WorkSim); // Do Work
          DevLog.WriteVerbose($"Finished {label}");
        }
        finally
        {
          semaphore.Release();
        }
      });
      // ReSharper restore AccessToDisposedClosure
    }
    Stopwatch sw = Stopwatch.StartNew();
    Task.WaitAll(tasks);
    sw.Stop();

    DevLog.WriteVerbose($"TotalElapsed: {sw.ElapsedMilliseconds}");

    Thread.Sleep(500); // See if any logs come in afterward
    DevLog.WriteVerbose("Test Complete");
  }
}