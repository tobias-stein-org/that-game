using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tg.level
{
    /// <summary>
    /// A commonly shared data object between level generation steps. Each step may add its own
    /// relevant level data to the partial struct and can access data declared by other steps. 
    /// </summary>
    public partial struct LevelData
    {
        public struct Layer : IEquatable<Layer>
        {
            #region Layers

            public static Layer                 Floor                               = new Layer(0);
            public static Layer                 Obstructable                        = new Layer(1);
            // add more layer here...

            public static int                   MAX_LAYERS { get; private set; }    = 0;

            #endregion

            #region C'tor, internal data and logical operators

            private readonly int                index;

            private Layer(int index)
            {
                this.index = index;
                Layer.MAX_LAYERS++;
            }

            public override int GetHashCode() { return this.index.GetHashCode(); }

            public bool Equals(Layer other) { return this.index == other.index; }

            public static implicit operator int(Layer layer) { return layer.index; }

            #endregion
        }
    }
}