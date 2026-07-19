// 근거: 도트 시스템.md — 이펙트 종류(EffectType 11종). 팔레트 시스템.md — 색상이 의미를 전달한다.
// 게임 코드는 "화염 폭발 시트 3번"이 아니라 "타격 이펙트"를 요청한다 — 에셋 교체가 코드 수정 없이 가능하다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 상황별 이펙트 카탈로그. 게임 코드는 VfxId 문자열로 요청하고,
    /// 어떤 시트의 몇 번 행을 쓸지는 이 에셋이 결정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Art/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("게임 코드가 사용하는 식별자. VfxId 상수와 맞춘다.")]
            public string id;
            public VfxDefinition definition;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, VfxDefinition> _lookup;

        public VfxDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, VfxDefinition>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null || string.IsNullOrEmpty(e.id) || e.definition == null) continue;
                    _lookup[e.id] = e.definition;
                }
            }

            return _lookup.TryGetValue(id, out var def) ? def : null;
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

        // 이동
        public const string DashTrail = "dash.trail";     // 대쉬 잔상
        public const string JumpDust = "jump.dust";       // 점프 먼지
        public const string LandDust = "land.dust";       // 착지 먼지

        // 상태이상 (팔레트 색상 행과 대응)
        public const string StatusBurn = "status.burn";
        public const string StatusPoison = "status.poison";
        public const string StatusFreeze = "status.freeze";
        public const string StatusShock = "status.shock";
        public const string StatusCurse = "status.curse";

        // 회복/버프
        public const string Heal = "heal";
        public const string Buff = "buff";

        // 사망/스폰
        public const string Death = "death";
        public const string Spawn = "spawn";
    }
}
