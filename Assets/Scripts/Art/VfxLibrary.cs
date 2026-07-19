// 근거: 도트 시스템.md — 이펙트 종류(EffectType 11종). 팔레트 시스템.md — 색상이 의미를 전달한다.
// 게임 코드는 "화염 폭발 시트 3번"이 아니라 "타격 이펙트"를 요청한다 — 에셋 교체가 코드 수정 없이 가능하다.
// 하나의 연출은 여러 이펙트를 겹쳐 만든다 (예: 베기 = 궤적 + 스파크 + 충격파).
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 합성 이펙트의 레이어 1장. 오프셋·지연·회전을 달리해 겹치면 타격감이 크게 살아난다.
    /// </summary>
    [System.Serializable]
    public class VfxLayer
    {
        public VfxDefinition definition;

        [Tooltip("기준 위치로부터의 오프셋. 좌우 반전 시 x가 뒤집힌다.")]
        public Vector2 offset;

        [Tooltip("재생 지연(초). 0.03~0.08 정도로 어긋내면 층이 살아난다.")]
        [Min(0f)] public float delay;

        [Tooltip("이 레이어만의 추가 크기 배율.")]
        [Min(0.01f)] public float scaleMultiplier = 1f;

        [Tooltip("회전 각도(도).")]
        public float rotation;

        [Tooltip("매번 무작위 회전 — 반복 타격이 똑같아 보이지 않게 한다.")]
        public bool randomRotation;

        [Tooltip("색조. 흰색이면 원본 색 유지.")]
        public Color tint = Color.white;

        [Tooltip("재생 속도 배율 — 1보다 크면 빨라진다.")]
        [Min(0.1f)] public float speedMultiplier = 1f;
    }

    /// <summary>
    /// 상황별 이펙트 카탈로그. 게임 코드는 VfxId 문자열로 요청하고,
    /// 어떤 시트를 몇 장 겹칠지는 이 에셋이 결정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Art/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("게임 코드가 사용하는 식별자. VfxId 상수와 맞춘다.")]
            public string id;

            [Tooltip("겹쳐 재생할 이펙트 레이어들. 위에서부터 순서대로 재생된다.")]
            public List<VfxLayer> layers = new List<VfxLayer>();
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        /// <summary>id에 해당하는 합성 이펙트를 찾는다.</summary>
        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    _lookup[e.id] = e;
                }
            }

            return _lookup.TryGetValue(id, out var entry) ? entry : null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 툴이 항목을 채울 때 사용.</summary>
        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            _lookup = null;
        }
#endif
    }

    /// <summary>
    /// 이펙트 식별자 상수. 문자열 오타를 막고 어떤 이펙트가 필요한지 한눈에 보이게 한다.
    /// </summary>
    public static class VfxId
    {
        // 전투
        public const string HitNeutral = "hit.neutral";   // 일반 타격
        public const string HitCritical = "hit.critical"; // 치명타
        public const string HitBlood = "hit.blood";       // 출혈/피격
        public const string Slash = "slash";              // 검 베기
        public const string Explosion = "explosion";      // 폭발 (폭탄마)

        // 투사체
        public const string ProjectileFly = "projectile.fly";     // 비행 중
        public const string ProjectileImpact = "projectile.impact"; // 착탄

        // 이동
        public const string DashTrail = "dash.trail";     // 대쉬 잔상
        public const string JumpDust = "jump.dust";       // 점프 먼지
        public const string LandDust = "land.dust";       // 착지 먼지

        // 상태이상 — 캐릭터에 붙어 반복 재생된다
        public const string StatusBurn = "status.burn";
        public const string StatusPoison = "status.poison";
        public const string StatusFreeze = "status.freeze";
        public const string StatusShock = "status.shock";
        public const string StatusCurse = "status.curse";
        public const string StatusFear = "status.fear";
        public const string StatusConfusion = "status.confusion";

        // 회복/버프
        public const string Heal = "heal";
        public const string Buff = "buff";

        // 사망/스폰
        public const string Death = "death";
        public const string Spawn = "spawn";

        /// <summary>상태이상 종류 → 이펙트 id. 없으면 null.</summary>
        public static string ForStatus(TSWP.StatusEffects.StatusEffectType type)
        {
            switch (type)
            {
                case TSWP.StatusEffects.StatusEffectType.Burn: return StatusBurn;
                case TSWP.StatusEffects.StatusEffectType.Poison: return StatusPoison;
                case TSWP.StatusEffects.StatusEffectType.Freeze: return StatusFreeze;
                case TSWP.StatusEffects.StatusEffectType.Shock: return StatusShock;
                case TSWP.StatusEffects.StatusEffectType.Confusion: return StatusConfusion;
                case TSWP.StatusEffects.StatusEffectType.Fear: return StatusFear;
                case TSWP.StatusEffects.StatusEffectType.Bleed: return HitBlood;
                default: return null;
            }
        }
    }
}
