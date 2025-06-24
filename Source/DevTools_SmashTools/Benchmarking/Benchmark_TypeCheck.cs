using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DevTools.Benchmarking;
using Verse;

// ReSharper disable all

namespace SmashTools.Performance;

[BenchmarkClass("TypeCheck"), SampleSize(1000000)]
internal class Benchmark_TypeCheck
{
  [Benchmark(Label = "IsSubclass")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void TypeCheck_IsSubclass()
  {
    _ = typeof(ThingDef).IsSubclassOf(typeof(Def));
  }

  [Benchmark(Label = "IsAssignableFrom")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void TypeCheck_IsAssignableFrom()
  {
    _ = typeof(Def).IsAssignableFrom(typeof(ThingDef));
  }

  [Benchmark(Label = "IsSubclass Generic")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void TypeCheck_IsSubclassGeneric()
  {
    _ = typeof(ThingOwner<Thing>).IsSubclassOf(typeof(ThingOwner));
  }

  [Benchmark(Label = "IsAssignableFrom Generic")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void TypeCheck_IsAssignableFromGeneric()
  {
    _ = typeof(ThingOwner).IsAssignableFrom(typeof(ThingOwner<Thing>));
  }
}