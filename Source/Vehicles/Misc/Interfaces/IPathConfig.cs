﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vehicles;

public interface IPathConfig
{
  bool UsesRegions { get; }
  bool MatchesReachability(IPathConfig other);
}