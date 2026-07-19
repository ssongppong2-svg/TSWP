// 근거: 적 시스템.md — 적은 CombatEntity와 동일한 전투 규칙을 따르고, 처치 시 경험치/골드/아이템을 드롭한다.
//       적도 상태이상과 환경(낙사/용암/독/폭발/얼음)의 영향을 받는다.
// 근거: Combat/CombatEntity.cs:180 계약 — "적: EnemyController가 구독해 처치 보상·오브젝트 정리 수행".
//       시체를 남겨 두면 콜라이더가 OverlapCircle 결과를 오염시키고 메모리도 단조 증가한다.
// 데이터(EnemyData) → 런타임 인스턴스 조립 담당. AI 판단은 EnemyAI가 분리 담당한다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Items;
using TSWP.StatusEffects;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 런타임 컨트롤러. CombatEntity(체력/피격/사망)를 조합하고 사망 시 드롭·보상을 처리한다.
    /// SpawnManager가 프리팹 인스턴스화 후 Initialize를 호출한다.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public class EnemyController : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("스폰 시 SpawnManager가 주입한다. 인스펙터 값은 단독 테스트용 폴백.")]
        [SerializeField] private EnemyData data;

        private CombatEntity _combat;
        private StatusEffectController _status;
        private EnemyHealthBar _healthBar;
        private EnemyDifficultyScaling _scaling;
        private System.Random _rng;
        private bool _deathHandled;

        /// <summary>처치 기여 플레이어 — 마지막으로 피해를 준 플레이어에게 귀속. (-1 = 미귀속)</summary>
        private int _lastDamagerPlayerId = -1;

        public EnemyData Data => data;
        public CombatEntity Combat => _combat;
        public StatusEffectController Status => _status;

        /// <summary>난이도 배율이 적용된 공격력 배율 — EnemyAI가 DamageInfo 구성 시 곱한다.</summary>
        public float AttackMultiplier => _scaling != null ? _scaling.attackMultiplier : 1f;

        /// <summary>난이도 배율이 적용된 패턴 속도 — 1보다 크면 쿨타임이 짧아진다.</summary>
        public float PatternSpeedMultiplier => _scaling != null ? _scaling.patternSpeedMultiplier : 1f;

        private void Awake()
        {
            _combat = GetComponent<CombatEntity>();
            _status = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                _combat.Damaged += OnDamaged;
                _combat.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_combat != null)
            {
                _combat.Damaged -= OnDamaged;
                _combat.Died -= OnDied;
            }
        }

        /// <summary>
        /// 스폰 직후 초기화. 난이도 배율은 체력/공격력/패턴 속도 3종만 적용한다 (기믹·행동 불변).
        /// </summary>
        /// <param name="enemyData">적 정의 SO</param>
        /// <param name="difficulty">방장이 선택한 난이도</param>
        /// <param name="rng">드롭 추첨용 시드 난수 (RunManager 시드 파생 — 멀티 동기화)</param>
        public void Initialize(EnemyData enemyData, Difficulty difficulty, System.Random rng)
        {
            data = enemyData;
            _rng = rng ?? new System.Random();

            if (data == null)
            {
                Debug.LogError("[EnemyController] EnemyData가 없습니다.", this);
                return;
            }

            _scaling = data.GetScaling(difficulty);
            _deathHandled = false;

            if (_combat != null)
            {
                _combat.SetTeam(TeamType.Enemies);
                _combat.SetOwnerPlayerId(-1); // 비플레이어
                // keepRatio: true — 스폰 시점의 체력 비율(=100%)을 유지한다.
                // false로 두면 프리팹 직렬화 체력(기본 100)에서 하향 클램프만 되어
                // data.maxHp가 더 큰 적이 '깎인 체력'으로 등장한다 (감사 §2).
                _combat.SetMaxHp(data.maxHp * _scaling.hpMultiplier, keepRatio: true);
            }

            // 면역 상태이상 주입 — 일부 적/엘리트는 특정 상태이상에 면역 (상태이상 시스템.md)
            if (_status != null)
            {
                var immunitySource = data.grade == EnemyGrade.Elite || data.grade == EnemyGrade.MiniBoss
                    ? ImmunitySource.Elite
                    : ImmunitySource.Passive;
                for (int i = 0; i < data.statusImmunities.Count; i++)
                    _status.AddImmunity(data.statusImmunities[i], immunitySource);
            }

            SetupHealthBar();

            _lastDamagerPlayerId = -1;
        }

        /// <summary>
        /// 머리 위 체력바 준비. 프리팹에 이미 있으면 그것을 쓰고, 없으면 붙인다.
        /// showHealthBar가 꺼져 있으면 기존 체력바도 비활성화한다.
        /// </summary>
        private void SetupHealthBar()
        {
            if (_healthBar == null) _healthBar = GetComponentInChildren<EnemyHealthBar>(true);

            if (data == null || !data.showHealthBar)
            {
                if (_healthBar != null) _healthBar.gameObject.SetActive(false);
                return;
            }

            if (_healthBar == null) _healthBar = gameObject.AddComponent<EnemyHealthBar>();
            _healthBar.gameObject.SetActive(true);
            _healthBar.SetOwner(_combat);
        }

        private void Start()
        {
            // 인스펙터에 data를 직접 꽂아 둔 단독 배치(Initialize 미호출) 대응 — 체력바는 그래도 나와야 한다.
            if (_healthBar == null) SetupHealthBar();
        }

        /// <summary>처치 귀속을 위해 마지막 가해 플레이어를 기록한다.</summary>
        private void OnDamaged(DamageInfo info)
        {
            if (info.Source != null && info.Source.OwnerPlayerId >= 0)
                _lastDamagerPlayerId = info.Source.OwnerPlayerId;
        }

        private void OnDied(CombatEntity entity)
        {
            if (_deathHandled) return; // 보상/드롭 이중 지급 방지
            _deathHandled = true;

            if (data != null)
            {
                // 처치 보상(경험치/골드/아이템). EnemyKilled 이벤트는 KillReward.Grant가 단일 경로로 발행한다
                // — 여기서 GameEvents.RaiseEnemyKilled를 중복 호출하지 말 것.
                data.killReward?.Grant(_lastDamagerPlayerId, data.enemyId);
                DropLoot();
            }

            // 데이터가 없어도 정리는 반드시 수행한다 — 시체가 남으면 탐지 결과가 오염된다.
            PlayDeathVfx();
            BeginDeathCleanup();
        }

        /// <summary>사망 이펙트 재생. 연출 매니저가 없으면 조용히 생략된다.</summary>
        private void PlayDeathVfx()
        {
            var spawner = Art.VfxSpawner.Instance;
            if (spawner == null) return;

            string vfxId = data != null && !string.IsNullOrEmpty(data.deathVfxId) ? data.deathVfxId : Art.VfxId.Death;
            spawner.Play(vfxId, transform.position);
        }

        /// <summary>
        /// 사망 정리 — 즉시 파괴하지 않고 짧은 연출 후 제거한다.
        /// 단, 콜라이더/AI/물리는 즉시 끈다: 시체가 남아 탐지·오버랩 결과를 오염시키면 안 된다.
        /// (Destroy를 이 프레임에 부르면 진행 중인 피해 파이프라인이 파괴된 오브젝트를 만진다 → 지연 파괴)
        /// </summary>
        private void BeginDeathCleanup()
        {
            var ai = GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;

            if (_healthBar != null) _healthBar.Hide();

            var colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = false;

            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false; // 콜라이더가 꺼졌으므로 바닥을 뚫고 떨어지지 않게 한다
            }

            // data가 없는 단독 배치도 정리되어야 하므로 기본값(0.4초)으로 폴백한다.
            float linger = data != null ? Mathf.Max(0f, data.deathLingerDuration) : 0.4f;
            bool fade = data == null || data.fadeOutOnDeath;

            if (fade && linger > 0f && isActiveAndEnabled)
                StartCoroutine(FadeOutRoutine(linger));

            // linger가 0이어도 Destroy는 프레임 끝에 처리되므로 파이프라인 도중 파괴되지 않는다.
            Destroy(gameObject, linger);
        }

        /// <summary>사망 연출 — 스프라이트를 서서히 투명하게. 렌더러가 없으면 아무 일도 하지 않는다.</summary>
        private IEnumerator FadeOutRoutine(float duration)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) yield break;

            var startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) startColors[i] = renderers[i].color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 히트스톱(timeScale 저하) 중에도 연출은 흘러야 한다 — VfxSpawner와 동일한 기준.
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    Color c = startColors[i];
                    c.a *= alpha;
                    r.color = c;
                }
                yield return null;
            }
        }

        /// <summary>드롭 테이블 추첨 후 월드에 아이템을 스폰한다.</summary>
        private void DropLoot()
        {
            if (data.dropTable == null) return;

            var rng = _rng ?? new System.Random();

            int gold = data.dropTable.RollGold(rng);
            if (gold > 0 && _lastDamagerPlayerId >= 0)
                GameEvents.RaiseGoldGained(_lastDamagerPlayerId, gold);

            var drops = new List<ItemDefinition>();
            data.dropTable.RollDrops(rng, data.rareDropMultiplier, drops);

            if (drops.Count == 0) return;

            // 드롭은 소유권 없이 월드에 놓인다 — 먼저 집는 플레이어가 소유자 (아이템 시스템.md)
            var dropManager = ItemDropManager.Instance;
            if (dropManager == null)
            {
                // TODO: ItemDropManager 부트스트랩 이전이면 드롭이 소실된다 — 씬 구성 시 매니저 배치 필요.
                Debug.LogWarning("[EnemyController] ItemDropManager가 없어 드롭을 생성하지 못했습니다.", this);
                return;
            }

            for (int i = 0; i < drops.Count; i++)
                dropManager.SpawnDrop(drops[i], (Vector2)transform.position);
        }
    }
}
