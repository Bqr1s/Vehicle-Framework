using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DevTools.Benchmarking;

// ReSharper disable all

namespace SmashTools.Performance;

[BenchmarkClass("Recursion"), SampleSize(10_000)]
internal class Benchmark_Recursion
{
  [Benchmark(Label = "Recursion")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void Recursion_TailCall(ref RecursionContext context)
  {
    ProcessNode(context.root);
  }

  [Benchmark(Label = "Stack Loop")]
  [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
  private static void Recursion_StackLoop(ref RecursionContext context)
  {
    DevTools.Utils.DoRecursive(context.root, ProcessNode, GetChildren);
  }

  private static void ProcessNode(Node node)
  {
    node.processed = true;
    foreach (Node childNode in node.children)
      ProcessNode(childNode);
  }

  private static IEnumerable<Node> GetChildren(Node node)
  {
    return node.children;
  }

  private readonly struct RecursionContext
  {
    public readonly Node root;

    public RecursionContext()
    {
      const int Depth = 15;

      root = new Node(Depth);
    }
  }

  private class Node
  {
    public List<Node> children = [];
    public bool processed;

    public Node(int depth)
    {
      if (depth > 0)
      {
        children.Add(new Node(--depth));
        children.Add(new Node(--depth));
      }
    }
  }
}