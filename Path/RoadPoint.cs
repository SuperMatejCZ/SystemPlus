﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemPlus.Path
{
    public class RoadPoint : Point
    {
        public RoadType type { get; }

        public RoadPoint(float _heigth, RoadType _type)
            : base(_heigth)
        {
            type = _type;
        }

        public enum RoadType : byte
        {
            Bridge = 1,
            Normal = 2,
            Tunnel = 3,
        }
    }
}
