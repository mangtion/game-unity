using System.Collections.Generic;
using UnityEngine;

namespace YiSunSin
{
    public static class UpgradeEffects
    {
        const float WeaponDamagePerStack = 0.2f;
        const float FireRatePerStack = 0.1f;
        const float MoveSpeedPerStack = 0.1f;
        const float MaxHpPerStack = 20f;
        const float PickupRadiusPerStack = 0.2f;
        const float MinFireInterval = 0.05f;

        static int StackCount(Dictionary<string, int> stacks, string id) =>
            stacks.TryGetValue(id, out var n) ? n : 0;

        public static float EffectiveWeaponDamage(Dictionary<string, int> stacks) =>
            GameConfig.Weapon.Damage * (1f + WeaponDamagePerStack * StackCount(stacks, "weaponDamage"));

        public static float EffectiveFireInterval(Dictionary<string, int> stacks)
        {
            float interval = GameConfig.Weapon.FireInterval *
                Mathf.Pow(1f - FireRatePerStack, StackCount(stacks, "fireRate"));
            return Mathf.Max(interval, MinFireInterval);
        }

        public static float EffectiveMoveSpeed(Dictionary<string, int> stacks) =>
            GameConfig.Player.MoveSpeed * (1f + MoveSpeedPerStack * StackCount(stacks, "moveSpeed"));

        public static float EffectiveMaxHp(Dictionary<string, int> stacks) =>
            GameConfig.Player.MaxHp + MaxHpPerStack * StackCount(stacks, "maxHp");

        public static float EffectivePickupRadius(Dictionary<string, int> stacks) =>
            GameConfig.XpGemPickupRadius * (1f + PickupRadiusPerStack * StackCount(stacks, "pickupRadius"));
    }
}
