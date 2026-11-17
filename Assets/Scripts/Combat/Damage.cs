using UnityEngine;

namespace Chopper
{
    public enum HitResult
    {
        /// <summary>충돌로 취급하지 않는다(무적 등). 총알은 그대로 진행.</summary>
        Ignored = 0,
        /// <summary>충돌했지만 피해는 없다. 총알은 소멸.</summary>
        Blocked = 1,
        Damaged = 2,
        Killed = 3,
    }

    public interface IDamageable
    {
        HitResult TakeDamage(int amount, Vector2 hitPoint);
    }

    /// <summary>플레이어 콜라이더와 겹쳤을 때 반응하는 대상(적 몸통, 아이템).</summary>
    public interface IPlayerContact
    {
        void OnPlayerContact(PlayerHealth player);
    }
}
