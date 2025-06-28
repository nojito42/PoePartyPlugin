using ExileCore.PoEMemory;
using System;
using System.Linq;

namespace PoePartyPlugin.Memory
{
    public static class PatternExtensions
    {
        public static readonly byte[] CastSkillBytes = [0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10, 0x56, 0x57, 0x41, 0x54, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x50];

        public static readonly string CastSkillMask = "xxxxxxxxxxxxxxxxxxxxxx";

        //48 89 5C 24 10 57 48 83 ec 30 0f -> cast skill with position
        public static readonly byte[] CastSkillWithPositionBytes = [0x48, 0x89, 0x5C, 0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x0F];
        public static readonly string CastSkillWithPositionMask = "xxxxxxxxxxx";

        //48 89 5c 24 08 48 89 74 24 10 48 89 7c 24 18 55 48 8d 6c 24 a9 48 81 ec 00 01 00 00 -> gem level up
        public static readonly byte[] GemLevelUpBytes = [0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x48, 0x89, 0x7C, 0x24, 0x18, 0x55, 0x48, 0x8D, 0x6C, 0x24, 0xA9, 0x48, 0x81, 0xEC, 0x00, 0x01, 0x00, 0x00];
        public static readonly string GemLevelUpMask = "xxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        public static long FindPattern(this PoePartyPlugin p, byte[] bytes, string mask, string name)
        {
            var result = p.GameController.Memory.FindPatterns(new Pattern(bytes, mask, name));
            if (result == null || result.Count() == 0)
            {
                throw new Exception($"Pattern {name} not found");
            }
            return result[0];
        }
    }
}
