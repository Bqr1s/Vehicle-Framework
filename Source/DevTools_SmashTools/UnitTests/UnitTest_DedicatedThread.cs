using System;
using System.Threading;
using DevTools.UnitTesting;
using SmashTools.Performance;
using UnityEngine.Assertions;

namespace SmashTools.UnitTesting;

[UnitTest(TestType.MainMenu)]
[TestDescription(
  "DedicatedThread execution, wait handles, and validation for no polling-like behavior.")]
internal class UnitTest_DedicatedThread
{
  private const int ThreadJoinTimeout = 5000;
  private const int WaitTime = 1000;
  private const int ItemWorkMS = WaitTime / 10;

  [Test]
  private void Dispatcher()
  {
    DedicatedThread dedicatedThread = ThreadManager.CreateNew();
    // Should never start suspended
    Assert.IsFalse(dedicatedThread.IsSuspended);

    using ManualResetEventSlim mres = new(false);

    // No signal should be received, it should've already entered a blocked state while waiting 
    // for an item to enqueue.
    Expect.IsTrue(dedicatedThread.IsBlocked, "No Polling");

    // ReSharper disable AccessToDisposedClosure
    // NOTE - Yes this may seem a little dubious but we're using the wait handle to run this test
    // synchronously specifically so we can verify that this thread is processing items correctly.
    // That means signaling the thread to resume execution, but then wait for us to finish testing.
    AsyncLongOperationAction pollingOp = AsyncPool<AsyncLongOperationAction>.Get();
    pollingOp.OnValidate += () => !mres.WaitHandle.SafeWaitHandle.IsClosed;
    pollingOp.OnInvoke += () => SleepThread(ItemWorkMS, mres: mres);
    dedicatedThread.Enqueue(pollingOp);
    // ReSharper restore AccessToDisposedClosure

    // Signal should be received this time, enqueueing item will set the event handler and resume
    // the thread's execution.
    Expect.IsFalse(dedicatedThread.IsBlocked, "Execution Resumed");

    Expect.IsTrue(mres.Wait(TimeSpan.FromMilliseconds(WaitTime)), "WaitHandle Execution");
    mres.Reset();

    Assert.AreEqual(dedicatedThread.QueueCount, 0);
    Expect.IsTrue(dedicatedThread.IsBlocked, "Execution Waiting");

    EnqueueWorkItems(dedicatedThread, mres);
    Assert.IsTrue(dedicatedThread.QueueCount > 0);
    Assert.IsTrue(dedicatedThread.IsBlocked);

    // Stop will send an event to the wait handle to resume so that it may exit
    dedicatedThread.Stop();
    // Allow WaitTime limit for each item in queue, but it should take nowhere near this long.
    Expect.IsTrue(dedicatedThread.thread.Join(TimeSpan.FromMilliseconds(ThreadJoinTimeout)),
      "WaitHandle Stop Gracefully");
    mres.Reset();

    Expect.AreEqual(dedicatedThread.QueueCount, 0, "Stop Gracefully Queue Empty");
    Expect.IsTrue(dedicatedThread.Terminated, "Stop Gracefully Terminated");
    dedicatedThread.Release();

    // Start a new thread so we can check immediate stop
    dedicatedThread = ThreadManager.CreateNew();
    Assert.IsNotNull(dedicatedThread);
    EnqueueWorkItems(dedicatedThread, mres);
    Assert.IsTrue(dedicatedThread.QueueCount > 0);
    Assert.IsTrue(dedicatedThread.IsBlocked);

    // Stop will send an event to the wait handle to resume so that it may exit
    dedicatedThread.StopImmediately();
    Expect.IsTrue(dedicatedThread.thread.Join(TimeSpan.FromMilliseconds(ThreadJoinTimeout)),
      "WaitHandle Stop Immediately");
    mres.Reset();

    Expect.GreaterThan(dedicatedThread.QueueCount, 0, "Stop Immediately Queue Not Empty");
    Expect.IsTrue(dedicatedThread.Terminated, "Stop Immediately Terminated");
    dedicatedThread.Release();
  }

  private static void EnqueueWorkItems(DedicatedThread thread, ManualResetEventSlim resetEvent)
  {
    AsyncLongOperationAction workOp;
    for (int i = 0; i < 3; i++)
    {
      workOp = AsyncPool<AsyncLongOperationAction>.Get();
      workOp.OnInvoke += () => SleepThread(ItemWorkMS);
      thread.EnqueueSilently(workOp);
    }

    // Set wait handle in the last one so we can resume test execution
    workOp = AsyncPool<AsyncLongOperationAction>.Get();
    workOp.OnInvoke += () => SleepThread(ItemWorkMS, mres: resetEvent);
    thread.EnqueueSilently(workOp);
  }

  private static void SleepThread(int waitTime, ManualResetEventSlim mres = null)
  {
    // Simulate work so we can validate that consumer thread has unblocked
    Thread.Sleep(waitTime);
    mres?.Set();
  }
}