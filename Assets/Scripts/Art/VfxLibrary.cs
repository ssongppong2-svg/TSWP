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

        [Tooltip("위치 무작위 흔들림 반경(유닛). 같은 자리를 반복해서 때려도 이펙트가 매번 달라 보인다. 0이면 사용 안 함.")]
        [Min(0f)] public float positionJitter; // TODO(밸런스): 문서 미정 — 0.05~0.15 정도가 자연스럽다

        [Tooltip("크기 무작위 편차(±비율). 0.1이면 0.9~1.1배. 0이면 사용 안 함.")]
        [Range(0f, 0.9f)] public float scaleJitter; // TODO(밸런스): 문서 미정

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

        // ── 프리워밍 ──────────────────────────────────────────────
        // VfxDefinition.GetFrames()의 최초 호출은 Sprite.Create N회 + 시트 텍스처의
        // 동기 로드/GPU 업로드를 동반한다. 이걸 전투 중 첫 타격에 맞으면 눈에 띄게 끊긴다.
        // 로딩 시점에 미리 돌려 두면 실제 전투에서는 캐시 히트만 남는다.
        // 같은 (시트, 행) 정의는 여러 항목이 공유하므로 HasCachedFrames로 중복을 건너뛴다.

        /// <summary>
        /// 등록된 모든 정의의 프레임을 한 번에 만들어 둔다. 만든 정의 수를 반환한다.
        /// 이 호출 자체가 한 프레임을 잡아먹으므로 전투 중이 아닌 로딩 시점에 부른다.
        /// </summary>
        public int Prewarm()
        {
            int warmed = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.layers == null) continue;

                for (int j = 0; j < entry.layers.Count; j++)
                    if (WarmLayer(entry.layers[j])) warmed++;
            }

            return warmed;
        }

        /// <summary>
        /// 프리워밍을 여러 프레임에 나눠 처리한다. 정의가 많아 한 프레임에 다 하면 그 프레임이 튀는 경우에 쓴다.
        /// (VfxSpawner가 Awake에서 코루틴으로 돌린다.)
        /// </summary>
        public System.Collections.IEnumerator PrewarmRoutine(int definitionsPerFrame = 2)
        {
            int budget = Mathf.Max(1, definitionsPerFrame);
            int done = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.layers == null) continue;

                for (int j = 0; j < entry.layers.Count; j++)
                {
                    if (!WarmLayer(entry.layers[j])) continue;

                    if (++done < budget) continue;

                    done = 0;
                    yield return null;
                }
            }
        }

        /// <summary>이 레이어의 정의를 실제로 새로 만들었으면 true (이미 캐시돼 있었으면 false).</summary>
        private static bool WarmLayer(VfxLayer layer)
        {
            var definition = layer != null ? layer.definition : null;
            if (definition == null || definition.HasCachedFrames) return false;

            definition.GetFrames();
            return true;
        }

        /// <summary>모든 정의의 프레임 캐시를 비운다. 씬 언로드/에셋 교체 시 사용.</summary>
        public void ClearPrewarm()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.layers == null) continue;

                for (int j = 0; j < entry.layers.Count; j++)
                {
                    var layer = entry.layers[j];
                    if (layer != null && layer.definition != null) layer.definition.ClearCache();
                }
            }
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
