// 근거: 보스 시스템.md — 권장 패턴 구성 중 '특수 기술'. 보스 01 해치 퀸의 거미줄(UI Web).
//       거미줄은 플레이어 이동을 방해하고 화면에 표시된다 → 이동 방해는 상태이상(Slow/Root)으로,
//       화면 표시는 BossGaugeChannel.RaiseOverlay로 UI에 넘긴다(연출은 UI 소관, 로직은 여기).
// 상태이상 부여도 반드시 DamageSystem 파이프라인을 탄다 (면역/무적 규칙을 우회하지 않기 위함).
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.StatusEffects;

namespace TSWP.Bosses
{
    /// <summary>바닥에 거미줄 장판을 여러 개 깔고, 그 안의 플레이어를 지속적으로 둔화/속박하는 패턴.</summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Patterns/Web Field", fileName = "PatternBehaviour_Web_")]
    public sealed class WebPatternBehaviour : BossPatternBehaviour
    {
        [Header("장판 배치")]
        [Tooltip("생성할 거미줄 장판 개수.")]
        [SerializeField, Min(1)] private int fieldCount = 3; // TODO(밸런스): 문서 미정

        [Tooltip("장판 1개의 반경.")]
        [SerializeField, Min(0.1f)] private float fieldRadius = 2.5f; // TODO(밸런스): 문서 미정

        [Tooltip("true면 예고 시점의 플레이어 위치들에 깐다. 플레이어 수가 모자라면 보스 주변에 채운다.")]
        [SerializeField] private bool placeOnPlayers = true;

        [Tooltip("보스 주변에 채울 때의 배치 반경.")]
        [SerializeField, Min(0.1f)] private float scatterRadius = 4f; // TODO(밸런스): 문서 미정

        [Header("지속")]
        [Tooltip("장판 유지 시간(초).")]
        [SerializeField, Min(0.1f)] private float fieldDuration = 5f; // TODO(밸런스): 문서 미정

        [Tooltip("장판 안의 플레이어에게 효과를 다시 거는 간격(초).")]
        [SerializeField, Min(0.05f)] private float reapplyInterval = 0.5f; // TODO(밸런스): 문서 미정

        [Header("효과")]
        [Tooltip("이동 방해 상태이상 (Slow 또는 Root). 비면 피해만 들어간다.")]
        [SerializeField] private List<StatusEffectData> statusEffects = new List<StatusEffectData>();

        [Tooltip("재적용 1회당 피해. 0이면 상태이상만 건다.")]
        [SerializeField, Min(0f)] private float tickDamage = 0f; // TODO(밸런스): 문서 미정

        [Header("연출")]
        [Tooltip("장판 시각 프리팹(선택). 없으면 판정만 동작한다 — 로직은 연출에 의존하지 않는다.")]
        [SerializeField] private GameObject fieldVisualPrefab;

        [SerializeField] private string fieldVfxId;

        [Tooltip("화면 오버레이 최대 세기(0~1). 갇힌 플레이어 비율에 곱해 UI로 보낸다.")]
        [SerializeField, Range(0f, 1f)] private float overlayIntensity = 1f;

        public int FieldCount => fieldCount;
        public float FieldRadius => fieldRadius;
        public bool PlaceOnPlayers => placeOnPlayers;
        public float ScatterRadius => scatterRadius;
        public float FieldDuration => fieldDuration;
        public float ReapplyInterval => reapplyInterval;
        public List<StatusEffectData> StatusEffects => statusEffects;
        public float TickDamage => tickDamage;
        public GameObject FieldVisualPrefab => fieldVisualPrefab;
        public string FieldVfxId => fieldVfxId;
        public float OverlayIntensity => overlayIntensity;

        public override BossPatternRunner CreateRunner() => new WebFieldRunner(this);
    }

    /// <summary>WebPatternBehaviour의 실행 상태 (장판 좌표·재적용 타이머·생성한 연출 오브젝트).</summary>
    public sealed class WebFieldRunner : BossPatternRunner
    {
        private readonly WebPatternBehaviour _data;
        private readonly List<Vector2> _anchors = new List<Vector2>(4);
        private readonly List<GameObject> _visuals = new List<GameObject>(4);
        private readonly List<CombatEntity> _hitBuffer = new List<CombatEntity>(8);
        private readonly HashSet<CombatEntity> _caught = new HashSet<CombatEntity>();

        private float _reapplyTimer;
        private int _knownPlayerCount = 1;

        public WebFieldRunner(WebPatternBehaviour data) : base(data)
        {
            _data = data;
        }

        protected override void OnTelegraphStart(BossPatternContext ctx)
        {
            ResolveAnchors(ctx);
        }

        protected override void OnActiveStart(BossPatternContext ctx)
        {
            for (int i = 0; i < _anchors.Count; i++)
            {
                ctx.PlayVfx(_data.FieldVfxId, _anchors[i]);

                if (_data.FieldVisualPrefab == null) continue;
                var go = Object.Instantiate(_data.FieldVisualPrefab, _anchors[i], Quaternion.identity);
                _visuals.Add(go);
            }

            _reapplyTimer = 0f;
            ApplyWebEffects(ctx); // 깔리는 즉시 1회 적용
        }

        protected override bool OnActiveTick(BossPatternContext ctx, float deltaTime)
        {
            _reapplyTimer += deltaTime;
            if (_reapplyTimer >= _data.ReapplyInterval)
            {
                _reapplyTimer = 0f;
                ApplyWebEffects(ctx);
            }

            // 장판 지속은 '속도 배율'로 줄이지 않는다 — 광폭화가 방해 시간을 단축시키면 오히려 쉬워진다.
            return StageElapsed >= _data.FieldDuration;
        }

        protected override void OnCleanup(BossPatternContext ctx)
        {
            for (int i = 0; i < _visuals.Count; i++)
                if (_visuals[i] != null) Object.Destroy(_visuals[i]);
            _visuals.Clear();
            _anchors.Clear();
            _caught.Clear();

            // 오버레이를 반드시 끈다 — 빠뜨리면 화면에 거미줄이 영구히 남는다.
            BossGaugeChannel.RaiseOverlay(BossGaugeChannel.OverlayWeb, 0f);
        }

        private void ResolveAnchors(BossPatternContext ctx)
        {
            _anchors.Clear();

            var players = ctx.PlayerPositions;
            _knownPlayerCount = Mathf.Max(1, players.Count);

            if (_data.PlaceOnPlayers)
            {
                for (int i = 0; i < players.Count && _anchors.Count < _data.FieldCount; i++)
                    _anchors.Add(players[i]);
            }

            // 남는 개수는 보스 주변에 균등 배치 — 무작위가 아니라 결정론적 각도(멀티 동기화 유지).
            int remaining = _data.FieldCount - _anchors.Count;
            for (int i = 0; i < remaining; i++)
            {
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, remaining);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _data.ScatterRadius;
                _anchors.Add(ctx.BossPosition + offset);
            }
        }

        private void ApplyWebEffects(BossPatternContext ctx)
        {
            _caught.Clear();
            var effects = _data.StatusEffects.Count > 0 ? _data.StatusEffects : null;

            for (int a = 0; a < _anchors.Count; a++)
            {
                // 데미지 질의 — 숨은 관용(판정을 이펙트보다 약간 작게). 장판 가장자리 스침 무시.
                BossCombatUtil.CollectPlayersForDamage(_anchors[a], _data.FieldRadius, _hitBuffer);
                for (int i = 0; i < _hitBuffer.Count; i++)
                {
                    var target = _hitBuffer[i];
                    if (!_caught.Add(target)) continue; // 장판이 겹쳐도 한 틱에 한 번만

                    BossCombatUtil.ApplyHit(ctx.Boss, target, _data.TickDamage, _anchors[a],
                        knockbackForce: 0f, stunDuration: 0f, statusEffects: effects);
                }
            }

            // 갇힌 플레이어 비율을 화면 오버레이 세기로 보낸다.
            // NOTE(멀티): 지금은 전역 세기 1개다. 추후 '내 캐릭터가 갇혔는지'로 로컬 판정하도록 교체한다.
            float ratio = _caught.Count / (float)_knownPlayerCount;
            BossGaugeChannel.RaiseOverlay(BossGaugeChannel.OverlayWeb, ratio * _data.OverlayIntensity);
        }
    }
}
