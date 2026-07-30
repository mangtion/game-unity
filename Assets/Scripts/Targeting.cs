using System.Collections.Generic;
using UnityEngine;

namespace YiSunSin
{
    public static class Targeting
    {
        public static EnemyController FindNearest(Vector2 position, IReadOnlyList<EnemyController> enemies)
        {
            EnemyController nearest = null;
            float nearestSqr = float.PositiveInfinity;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                float sqr = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = enemy;
                }
            }
            return nearest;
        }
    }
}
