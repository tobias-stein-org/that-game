using Unity.Mathematics;
using Unity.Entities;

namespace tg.combat
{
    namespace entities
    {
        public struct Stats : IComponentData
        {
            public const uint MIN_STAT_VALUE = 1;

            public static readonly Stats one = new Stats(1, 1);

            private uint3 SAI;
            private uint2 AD;

            public uint STR { get { return this.SAI.x; } set { this.SAI.x = math.max(value, Stats.MIN_STAT_VALUE); } }
            public uint AGI { get { return this.SAI.y; } set { this.SAI.y = math.max(value, Stats.MIN_STAT_VALUE); } }
            public uint INT { get { return this.SAI.z; } set { this.SAI.z = math.max(value, Stats.MIN_STAT_VALUE); } }
            public uint ATT { get { return this.AD.x;  } set { this.AD.x  = math.max(value, Stats.MIN_STAT_VALUE); } }
            public uint DEF { get { return this.AD.y;  } set { this.AD.y  = math.max(value, Stats.MIN_STAT_VALUE); } }

            public Stats(uint STR, uint AGI, uint INT, uint ATT, uint DEF)
            {
                this.SAI    = math.max(new uint3(STR, AGI, INT), Stats.MIN_STAT_VALUE);
                this.AD     = math.max(new uint2(ATT, DEF), Stats.MIN_STAT_VALUE);
            }

            public Stats(uint3 SAI, uint2 AD)
            {
                this.SAI = math.max(SAI, Stats.MIN_STAT_VALUE);
                this.AD  = math.max(AD, Stats.MIN_STAT_VALUE);
            }

            public override string ToString()                                                   { return $"STR: {this.STR} AGI: {this.AGI} INT: {this.INT} ATT: {this.ATT} DEF: {this.DEF}"; }


            /// <summary>
            /// Aplpy a multiplier on all stats values.
            /// </summary>
            /// <param name="stats"></param>
            /// <param name="multiplier"></param>
            /// <returns></returns>
            public static Stats         operator *(in Stats stats,  float multiplier)           { return new Stats((uint3)math.mul((float3)stats.SAI, (float3)multiplier), (uint2)math.mul((float2)stats.AD, (float2)multiplier)); }

            /// <summary>
            /// Add two stats to get a new stat with the sum of the two stats
            /// </summary>
            /// <param name="stats0"></param>
            /// <param name="stats1"></param>
            /// <returns></returns>
            public static Stats         operator +(in Stats stats0, in Stats stats1)            { return new Stats(stats0.SAI + stats1.SAI, stats0.AD + stats1.AD); }

            /// <summary>
            /// Add a change to the stats.
            /// </summary>
            /// <param name="stats0"></param>
            /// <param name="change"></param>
            /// <returns></returns>
            public static Stats         operator +(in Stats stats0, in (int3, int2) change)     { return new Stats((uint3)((int3)stats0.SAI + change.Item1), (uint2)((int2)stats0.AD + change.Item2)); }

            /// <summary>
            /// Get the difference between two stats.
            /// </summary>
            /// <param name="stats0"></param>
            /// <param name="stats1"></param>
            /// <returns></returns>
            public static (int3, int2)  operator -(in Stats stats0, in Stats stats1)            { return ((int3)stats0.SAI - (int3)stats1.SAI, (int2)stats0.AD - (int2)stats1.AD); }
        }

        public static class Mechanics
        {
            private static float sigmoidRate(float x, float scale = 1.0f, float steepness = 1.0f, float shift = 0.0f) { return scale / (1.0f + math.exp(steepness * (x + shift))); }

            private static float a_ab(uint stat0, uint stat1) { return (float)stat0 / (float)(stat0 + stat1); }
            private static float aa_ab(float stat0, float stat1) { return (stat0 * stat0) / (stat0 + stat1); }

            public static float accuracy(this Stats attacker, in Stats defender)
            {
                return a_ab(attacker.AGI, defender.AGI);
            }

            public static float evasion(this Stats defender, in Stats attacker)
            {
                return sigmoidRate(a_ab(defender.AGI, attacker.AGI) - a_ab(attacker.AGI, defender.AGI), 2.0f, -3.0f, -1.0f);
            }

            public static float physicalCriticalHitChance(this Stats attacker, in Stats defender)
            {
                return a_ab(attacker.STR, defender.STR);
            }

            public static float magicalCriticalHitChance(this Stats attacker, in Stats defender)
            {
                return a_ab(attacker.INT, defender.INT);
            }

            public static float physicalResistance(this Stats defender, in Stats attacker)
            {
                return a_ab(defender.STR, attacker.STR);
            }

            public static float magicalResistance(this Stats defender, in Stats attacker)
            {
                return a_ab(defender.INT, attacker.INT);
            }

            public static float physicalCriticalHitRate(this Stats attacker, in Stats defender)
            {
                return sigmoidRate(attacker.physicalCriticalHitChance(in defender) - defender.physicalResistance(in attacker), 2.0f, -3.0f, -1.0f);
            }

            public static float magicalCriticalHitRate(this Stats attacker, in Stats defender)
            {
                return sigmoidRate(attacker.magicalCriticalHitChance(in defender) - defender.magicalResistance(in attacker), 2.0f, -3.0f, -1.0f);
            }

            public static float advantage(this Stats attacker, in Stats defender)
            {
                float advA = (a_ab(attacker.STR, defender.STR) + a_ab(attacker.AGI, defender.AGI) + a_ab(attacker.INT, defender.INT) + a_ab(attacker.ATT, defender.ATT) + a_ab(attacker.DEF, defender.DEF)) / 5f;
                float advD = 1.0f - advA;

                return sigmoidRate(advA - advD);
            }

            public static float physicalAttackRate(this Stats attacker, in Stats defender)
            {
                return sigmoidRate((defender.physicalResistance(in attacker) * 0.25f) - a_ab(attacker.ATT, defender.DEF), steepness: 3.0f);
            }

            public static float magicalAttackRate(this Stats attacker, in Stats defender)
            {
                return sigmoidRate((defender.magicalResistance(in attacker) * 0.25f) - a_ab(attacker.ATT, defender.DEF), steepness: 3.0f);
            }
        }
    }
}
