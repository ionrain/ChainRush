#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    // working model: bounded independent rooms keep off-screen actors in the workload.
    public static class PerformanceBenchLayout
    {
        public static Vector3 RoomOrigin(int room, float density) => new Vector3(room * 80f / density, 0, 0);
        public static int Room(int slot, int visible) => slot / visible;
        public static Vector3 Position(int slot, PerformanceBenchSettings settings)
        {
            int local = slot % settings.visiblePopulation;
            int columns = (int)Math.Ceiling(Math.Sqrt(settings.visiblePopulation));
            float spacing = 1.4f / settings.density;
            // Seed changes the two starting directions, not valid non-overlapping spawn positions.
            int row = local / columns;
            int column = local % columns;
            return RoomOrigin(Room(slot, settings.visiblePopulation), settings.density)
                + new Vector3((column - (columns - 1) * .5f) * spacing,
                    (row - (columns - 1) * .5f) * spacing, 0);
        }
        public static Vector3 Target(int room, bool right, float density) =>
            RoomOrigin(room, density) + new Vector3(right ? 16 / density : -16 / density, 0, 0);
        public static Vector3 ReplacementPosition(int slot, int life, int attempt, PerformanceBenchSettings settings)
        {
            if (attempt == 0) return Position(slot, settings);
            uint state = unchecked((uint)settings.seed ^ (uint)slot * 0x9e3779b9u
                ^ (uint)life * 0x85ebca6bu ^ (uint)attempt * 0xc2b2ae35u);
            state ^= state >> 16; state *= 0x7feb352du; state ^= state >> 15; state *= 0x846ca68bu; state ^= state >> 16;
            float x = ((state & 65535) / 65535f * 2 - 1) * 19 / settings.density;
            float y = ((state >> 16) / 65535f * 2 - 1) * 14 / settings.density;
            return RoomOrigin(Room(slot, settings.visiblePopulation), settings.density) + new Vector3(x, y, 0);
        }
        public static bool InitialDirection(int slot, int seed) => ((unchecked((uint)(slot * 1103515245 + seed)) >> 8) & 1) != 0;
    }
}
#endif
