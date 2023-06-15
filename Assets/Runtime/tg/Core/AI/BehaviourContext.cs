using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace tg.ai
{
    internal static class BehaviourContextInternal
    {
        /// <summary>
        /// The upper limit of how many ai behaviours there can be used during the application run-time.
        /// </summary>
        internal const int MAX_BEHAVIOURS           = 8;

        /// <summary>
        /// Running sequence counter for any new derived context behaviour type.
        /// </summary>
        internal static int NEXT_BEHAVIOUR_INDEX    = 0;

        internal static int nextBehaviourIndex()
        {
            Unity.Assertions.Assert.IsTrue(BehaviourContextInternal.NEXT_BEHAVIOUR_INDEX < BehaviourContextInternal.MAX_BEHAVIOURS, $"You have reached the upper limit of '{MAX_BEHAVIOURS}' AI behaviours. If you need more increase the 'BehaviourContextInternal.MAX_BEHAVIOURS' limit.");
            return BehaviourContextInternal.NEXT_BEHAVIOUR_INDEX++;
        }

        /// <summary>
        /// Adds an ID() extension method to each concrete context behaviour class. This will allow the retrievabl of the
        /// type unique index.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int ID<T>(this IBehaviourContext<T> value) where T : IBehaviourContext<T> { return IBehaviourContext<T>.ID; }
    }

    /// <summary>
    /// Each context aware ai behaviour has to derive from this interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IBehaviourContext<T> : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// This will provide a unique sequence index (starting from 0) to each concrete (derived) ai context behaviour.
        /// </summary>
        internal static readonly int ID = BehaviourContextInternal.nextBehaviourIndex();
    }

    /// <summary>
    /// Purely internal used "dummy" context to store the final solved context.
    /// </summary>
    internal struct Solved : IBehaviourContext<Solved> { }


    [Serializable]
    public struct BehaviourContext
    {
        private bool _valid;

        public float
            v00, v01, v02, v03, v04, v05, v06, v07,
            v08, v09, v10, v11, v12, v13, v14, v15;

        public static readonly BehaviourContext zero    = new BehaviourContext(0);
        public static readonly BehaviourContext one     = new BehaviourContext(1);

        #region Constructor

        public BehaviourContext(
            float v00, float v01, float v02, float v03, float v04, float v05, float v06, float v07,
            float v08, float v09, float v10, float v11, float v12, float v13, float v14, float v15)
        {
            this._valid = true;
            this.v00    = v00;
            this.v01    = v01;
            this.v02    = v02;
            this.v03    = v03;
            this.v04    = v04;
            this.v05    = v05;
            this.v06    = v06;
            this.v07    = v07;
            this.v08    = v08;
            this.v09    = v09;
            this.v10    = v10;
            this.v11    = v11;
            this.v12    = v12;
            this.v13    = v13;
            this.v14    = v14;
            this.v15    = v15;
        }

        public BehaviourContext(float value)
        {
            this._valid = true;
            this.v00    = value;
            this.v01    = value;
            this.v02    = value;
            this.v03    = value;
            this.v04    = value;
            this.v05    = value;
            this.v06    = value;
            this.v07    = value;
            this.v08    = value;
            this.v09    = value;
            this.v10    = value;
            this.v11    = value;
            this.v12    = value;
            this.v13    = value;
            this.v14    = value;
            this.v15    = value;
        }

        #endregion

        public unsafe float this[int index]
        {
		    get
            {
			    Unity.Assertions.Assert.IsTrue(this.isValid, "BehaviourContext in invalid!");
			    Unity.Assertions.Assert.IsFalse((uint)index > 15u, $"index ({index}) must be between[0...16]");
			    fixed (float* ptr = &v00)
                {
				    return *(float*)((byte*)ptr + (nint)index * (nint)4);
			    }
		    }
		    set
            {
			    Unity.Assertions.Assert.IsTrue(this.isValid, "BehaviourContext in invalid!");
                Unity.Assertions.Assert.IsFalse((uint)index > 15u, $"index ({index}) must be between[0...16]");
			    fixed (float* ptr = &v00)
                {
                    ptr[index] = value;
                }
		    }
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public override string ToString () { return this._valid ? $"BehaviourContext({v00}, {v01}, {v02}, {v03}, {v04}, {v05}, {v06}, {v07}, {v08}, {v09}, {v10}, {v11}, {v12}, {v13}, {v14}, {v15})" : "BehaviourContext(Invalid)"; }

        public bool isValid { get { return this._valid; } }

        #region Standard arithmetic operations

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator + (BehaviourContext lhs, BehaviourContext rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 + rhs.v00,
                lhs.v01 + rhs.v01,
                lhs.v02 + rhs.v02,
                lhs.v03 + rhs.v03,
                lhs.v04 + rhs.v04,
                lhs.v05 + rhs.v05,
                lhs.v06 + rhs.v06,
                lhs.v07 + rhs.v07,
                lhs.v08 + rhs.v08,
                lhs.v09 + rhs.v09,
                lhs.v10 + rhs.v10,
                lhs.v11 + rhs.v11,
                lhs.v12 + rhs.v12,
                lhs.v13 + rhs.v13,
                lhs.v14 + rhs.v14,
                lhs.v15 + rhs.v15
           );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator + (BehaviourContext lhs, float rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 + rhs,
                lhs.v01 + rhs,
                lhs.v02 + rhs,
                lhs.v03 + rhs,
                lhs.v04 + rhs,
                lhs.v05 + rhs,
                lhs.v06 + rhs,
                lhs.v07 + rhs,
                lhs.v08 + rhs,
                lhs.v09 + rhs,
                lhs.v10 + rhs,
                lhs.v11 + rhs,
                lhs.v12 + rhs,
                lhs.v13 + rhs,
                lhs.v14 + rhs,
                lhs.v15 + rhs
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator - (BehaviourContext lhs, BehaviourContext rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 - rhs.v00,
                lhs.v01 - rhs.v01,
                lhs.v02 - rhs.v02,
                lhs.v03 - rhs.v03,
                lhs.v04 - rhs.v04,
                lhs.v05 - rhs.v05,
                lhs.v06 - rhs.v06,
                lhs.v07 - rhs.v07,
                lhs.v08 - rhs.v08,
                lhs.v09 - rhs.v09,
                lhs.v10 - rhs.v10,
                lhs.v11 - rhs.v11,
                lhs.v12 - rhs.v12,
                lhs.v13 - rhs.v13,
                lhs.v14 - rhs.v14,
                lhs.v15 - rhs.v15
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator - (BehaviourContext lhs, float rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 - rhs,
                lhs.v01 - rhs,
                lhs.v02 - rhs,
                lhs.v03 - rhs,
                lhs.v04 - rhs,
                lhs.v05 - rhs,
                lhs.v06 - rhs,
                lhs.v07 - rhs,
                lhs.v08 - rhs,
                lhs.v09 - rhs,
                lhs.v10 - rhs,
                lhs.v11 - rhs,
                lhs.v12 - rhs,
                lhs.v13 - rhs,
                lhs.v14 - rhs,
                lhs.v15 - rhs
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator * (BehaviourContext lhs, BehaviourContext rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 * rhs.v00,
                lhs.v01 * rhs.v01,
                lhs.v02 * rhs.v02,
                lhs.v03 * rhs.v03,
                lhs.v04 * rhs.v04,
                lhs.v05 * rhs.v05,
                lhs.v06 * rhs.v06,
                lhs.v07 * rhs.v07,
                lhs.v08 * rhs.v08,
                lhs.v09 * rhs.v09,
                lhs.v10 * rhs.v10,
                lhs.v11 * rhs.v11,
                lhs.v12 * rhs.v12,
                lhs.v13 * rhs.v13,
                lhs.v14 * rhs.v14,
                lhs.v15 * rhs.v15
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator * (BehaviourContext lhs, float rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 * rhs,
                lhs.v01 * rhs,
                lhs.v02 * rhs,
                lhs.v03 * rhs,
                lhs.v04 * rhs,
                lhs.v05 * rhs,
                lhs.v06 * rhs,
                lhs.v07 * rhs,
                lhs.v08 * rhs,
                lhs.v09 * rhs,
                lhs.v10 * rhs,
                lhs.v11 * rhs,
                lhs.v12 * rhs,
                lhs.v13 * rhs,
                lhs.v14 * rhs,
                lhs.v15 * rhs
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator / (BehaviourContext lhs, BehaviourContext rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 / rhs.v00,
                lhs.v01 / rhs.v01,
                lhs.v02 / rhs.v02,
                lhs.v03 / rhs.v03,
                lhs.v04 / rhs.v04,
                lhs.v05 / rhs.v05,
                lhs.v06 / rhs.v06,
                lhs.v07 / rhs.v07,
                lhs.v08 / rhs.v08,
                lhs.v09 / rhs.v09,
                lhs.v10 / rhs.v10,
                lhs.v11 / rhs.v11,
                lhs.v12 / rhs.v12,
                lhs.v13 / rhs.v13,
                lhs.v14 / rhs.v14,
                lhs.v15 / rhs.v15
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext operator / (BehaviourContext lhs, float rhs)
	    {
		    return new BehaviourContext
            (
                lhs.v00 / rhs,
                lhs.v01 / rhs,
                lhs.v02 / rhs,
                lhs.v03 / rhs,
                lhs.v04 / rhs,
                lhs.v05 / rhs,
                lhs.v06 / rhs,
                lhs.v07 / rhs,
                lhs.v08 / rhs,
                lhs.v09 / rhs,
                lhs.v10 / rhs,
                lhs.v11 / rhs,
                lhs.v12 / rhs,
                lhs.v13 / rhs,
                lhs.v14 / rhs,
                lhs.v15 / rhs
            );
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public static BehaviourContext lerp(BehaviourContext lhs, BehaviourContext rhs, float t)
	    {
		    return new BehaviourContext
            (
                math.lerp(lhs.v00, rhs.v00, t),
                math.lerp(lhs.v01, rhs.v01, t),
                math.lerp(lhs.v02, rhs.v02, t),
                math.lerp(lhs.v03, rhs.v03, t),
                math.lerp(lhs.v04, rhs.v04, t),
                math.lerp(lhs.v05, rhs.v05, t),
                math.lerp(lhs.v06, rhs.v06, t),
                math.lerp(lhs.v07, rhs.v07, t),
                math.lerp(lhs.v08, rhs.v08, t),
                math.lerp(lhs.v09, rhs.v09, t),
                math.lerp(lhs.v10, rhs.v10, t),
                math.lerp(lhs.v11, rhs.v11, t),
                math.lerp(lhs.v12, rhs.v12, t),
                math.lerp(lhs.v13, rhs.v13, t),
                math.lerp(lhs.v14, rhs.v14, t),
                math.lerp(lhs.v15, rhs.v15, t)
            );
	    }

        #endregion

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public float csum() { return v00 + v01 + v02 + v03 + v04 + v05 + v06 + v07 + v08 + v09 + v10 + v11 + v12 + v13 + v14 + v15; }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
	    public float dot(BehaviourContext rhs)
	    {
		    return
                (v00 * rhs.v00) +
                (v01 * rhs.v01) +
                (v02 * rhs.v02) +
                (v03 * rhs.v03) +
                (v04 * rhs.v04) +
                (v05 * rhs.v05) +
                (v06 * rhs.v06) +
                (v07 * rhs.v07) +
                (v08 * rhs.v08) +
                (v09 * rhs.v09) +
                (v10 * rhs.v10) +
                (v11 * rhs.v11) +
                (v12 * rhs.v12) +
                (v13 * rhs.v13) +
                (v14 * rhs.v14) +
                (v15 * rhs.v15);
	    }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public BehaviourContext normalize() 
        {
            float magnitute = math.sqrt((v00 * v00) + (v01 * v01) + (v02 * v02) + (v03 * v03) + (v04 * v04) + (v05 * v05) + (v06 * v06) + (v07 * v07) + (v08 * v08) + (v09 * v09) + (v10 * v10) + (v11 * v11) + (v12 * v12) + (v13 * v13) + (v14 * v14) + (v15 * v15));

            return magnitute > 1e-5f ? this / magnitute : this;
        }
    }

    /// <summary>
    /// Dynamic buffer component to store AI behaviour contexts.
    /// </summary>
    [InternalBufferCapacity(BehaviourContextInternal.MAX_BEHAVIOURS)]
    public struct BehaviourContextData : IBufferElementData
    {
        /// <summary>
        /// Behaviour context values.
        /// </summary>
        public BehaviourContext         context;

        /// <summary>
        /// Last frames behaviours context. This value is only accessed by the solver to store the last frames context.
        /// </summary>
        private BehaviourContext        context1;

        /// <summary>
        /// Weight is used by the solver when aggregating the final solved context.
        /// </summary>
        public float                    weight;

        /// <summary>
        /// Amount of blending from previous frames context.
        /// note: A value of 1.0f would result in no value changes at all.
        /// </summary>
        public float                    blend;

        public BehaviourContextData(float weight, float blend)
        {
            this.weight     = 1.0f;
            this.blend      = 0.0f;
            this.context    = default;
            this.context1   = default;
        }

        public static readonly BehaviourContextData Default = new BehaviourContextData(1.0f, 0.0f);
    }
}
