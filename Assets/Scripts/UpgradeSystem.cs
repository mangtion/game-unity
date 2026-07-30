using System;
using System.Collections.Generic;
using UnityEngine;

namespace YiSunSin
{
    public struct UpgradeDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public int MaxStacks;
    }

    public static class UpgradeSystem
    {
        const int MaxStacksPerUpgrade = 5;

        public static readonly UpgradeDefinition[] Pool = new[]
        {
            new UpgradeDefinition { Id = "weaponDamage", Name = "공격력 강화", Description = "무기 데미지 +20%", MaxStacks = MaxStacksPerUpgrade },
            new UpgradeDefinition { Id = "fireRate", Name = "공격속도 강화", Description = "무기 발사 간격 -10%", MaxStacks = MaxStacksPerUpgrade },
            new UpgradeDefinition { Id = "moveSpeed", Name = "이동속도 강화", Description = "이동속도 +10%", MaxStacks = MaxStacksPerUpgrade },
            new UpgradeDefinition { Id = "maxHp", Name = "체력 강화", Description = "최대체력 +20", MaxStacks = MaxStacksPerUpgrade },
            new UpgradeDefinition { Id = "pickupRadius", Name = "습득 범위 강화", Description = "전공 훈장 습득 반경 +20%", MaxStacks = MaxStacksPerUpgrade },
        };

        public static List<UpgradeDefinition> PickChoices(
            Dictionary<string, int> ownedStacks, Func<float> rng, int count = 3)
        {
            var pool = new List<UpgradeDefinition>();
            foreach (var upgrade in Pool)
            {
                int owned = ownedStacks.TryGetValue(upgrade.Id, out var n) ? n : 0;
                if (owned < upgrade.MaxStacks) pool.Add(upgrade);
            }

            var picks = new List<UpgradeDefinition>();
            while (picks.Count < count && pool.Count > 0)
            {
                int index = Mathf.FloorToInt(rng() * pool.Count);
                if (index >= pool.Count) index = pool.Count - 1; // guards rng() == 1.0
                picks.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picks;
        }
    }
}
