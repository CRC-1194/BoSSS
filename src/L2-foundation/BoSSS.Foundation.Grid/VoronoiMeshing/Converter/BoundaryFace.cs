﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Foundation.Grid.Voronoi.Meshing.Converter
{
    struct BoundaryFace
    {
        public int Face;

        public int BoundaryEdgeNumber;

        public int ID;

        public int NeighborID;
    }
}
