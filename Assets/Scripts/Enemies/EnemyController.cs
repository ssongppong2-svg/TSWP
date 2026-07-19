// 근거: 적 시스템.md — 적은 CombatEntity와 동일한 전투 규칙을 따르고, 처치 시 경험치/골드/아이템을 드롭한다.
//       적도 상태이상과 환경(낙사/용암/독/폭발/얼음)의 영향을 받는다.
// 데이터(EnemyData) → 런타임 인스턴스 조립 담당. AI 판단은 EnemyAI가 분리 담당한다.
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
        private EnemyDifficultyScaling _scaling;
        private System.Random _rng;

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

            if (_combat != null)
            {
                _combat.SetTeam(TeamType.Enemies);
                _combat.SetOwnerPlayerId(-1); // 비플레이어
                _combat.SetMaxHp(data.maxHp * _scaling.hpMultiplier);
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

            _lastDamagerPlayerId = -1;
        }

        /// <summary>처치 귀속을 위해 마지막 가해 플레이어를 기록한다.</summary>
        private void OnDamaged(DamageInfo info)
        {
            if (info.Source != null && info.Source.OwnerPlayerId >= 0)
                _lastDamagerPlayerId = info.Source.OwnerPlayerId;
        }

        private void OnDied(CombatEntity entity)
        {
            if (data == null) return;

            // 처치 보상(경험치/골드/아이템). EnemyKilled 이벤트는 KillReward.Grant가 단일 경로로 발행한다
            // — 여기서 GameEvents.RaiseEnemyKilled를 중복 호출하지 말 것.
            data.killReward?.Grant(_lastDamagerPlayerId, data.enemyId);

            DropLoot();
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
