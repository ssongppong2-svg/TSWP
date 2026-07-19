// 근거: 직업 시스템.md — 플레이어는 게임 시작 전(로비) 직업을 선택한다.
// 동일 직업 중복 선택 항상 허용: 8명의 폭탄마/의사/용사 전원 동일 직업 가능 — 제한 로직을 두지 않는다 (스펙 unityNotes ⑤).
// 선택 정보는 멀티플레이 동기화 대상. Online.LobbyPlayerState.selectedJobId와 이 맵이 같은 정보를 바라본다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>
    /// 로비 단계 직업 선택 관리자. 게임 시작 시 ApplyJobTo로 플레이어 오브젝트에 직업 데이터를 주입한다
    /// (직업별 프리팹 분기 대신 단일 플레이어 프리팹 + 데이터 주입 — 스펙 unityNotes ③).
    /// </summary>
    public class JobSelectionManager : MonoBehaviour
    {
        public static JobSelectionManager Instance { get; private set; }

        [Tooltip("선택 가능한 전체 직업 목록 (로비 UI 표시용). 알려진 jobId: warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho")]
        [SerializeField] private List<JobDefinition> availableJobs = new List<JobDefinition>();

        // 플레이어별 선택 직업. // SYNC: 호스트 권위, 추후 NGO NetworkVariable (로비 전원에게 선택 현황 표시)
        private readonly Dictionary<int, JobDefinition> _playerJobMap = new Dictionary<int, JobDefinition>();

        // 로비 UI 갱신용 로컬 이벤트 — GameEvents에는 직업 선택 이벤트가 없고 수정이 금지되어 있으므로
        // (인게임 HUD가 아닌 로비 한정 정보) 로비 UI는 이 이벤트를 직접 구독한다.
        public event Action<int, string> JobSelected;     // playerId, jobId
        public event Action<int> SelectionCleared;        // playerId

        /// <summary>로비 UI 표시용 전체 직업 목록.</summary>
        public IReadOnlyList<JobDefinition> AvailableJobs => availableJobs;

        /// <summary>플레이어별 선택 현황 스냅샷 (읽기 전용).</summary>
        public IReadOnlyDictionary<int, JobDefinition> PlayerJobMap => _playerJobMap;

        private void Awake()
        {
            // 매니저 싱글턴 간단형 (ARCHITECTURE.md §3-4). DontDestroyOnLoad는 GameFlowManager만.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 직업 선택. jobId가 목록에 없으면 false.
        /// 중복 선택 검사는 의도적으로 하지 않는다 — 동일 직업 중복 선택 항상 허용 (8명 전원 동일 직업 가능).
        /// 선택 시점 검증(게임 시작 전만 허용)은 로비 흐름(Online.LobbyManager)이 담당한다.
        /// </summary>
        public bool TrySelectJob(int playerId, string jobId)
        {
            JobDefinition definition = FindJob(jobId);
            if (definition == null)
            {
                Debug.LogWarning($"[JobSelectionManager] 알 수 없는 jobId: {jobId}", this);
                return false;
            }

            _playerJobMap[playerId] = definition; // 재선택 = 덮어쓰기
            JobSelected?.Invoke(playerId, definition.JobId);
            return true;
        }

        /// <summary>선택 해제 (로비 이탈 등).</summary>
        public void ClearSelection(int playerId)
        {
            if (_playerJobMap.Remove(playerId))
            {
                SelectionCleared?.Invoke(playerId);
            }
        }

        /// <summary>전체 초기화 (로비 재시작/뒷풀이 → 로비 복귀).</summary>
        public void ClearAll()
        {
            var ids = new List<int>(_playerJobMap.Keys);
            _playerJobMap.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                SelectionCleared?.Invoke(ids[i]);
            }
        }

        public bool HasSelected(int playerId) => _playerJobMap.ContainsKey(playerId);

        public JobDefinition GetSelectedJob(int playerId) =>
            _playerJobMap.TryGetValue(playerId, out JobDefinition definition) ? definition : null;

        /// <summary>
        /// 게임 시작 시 플레이어 오브젝트에 직업 데이터 주입 (단일 플레이어 프리팹 + 데이터 주입 조립).
        /// </summary>
        public void ApplyJobTo(int playerId, GameObject playerObject)
        {
            JobDefinition definition = GetSelectedJob(playerId);
            if (definition == null || playerObject == null)
            {
                return;
            }

            if (playerObject.TryGetComponent(out SkillCaster caster))
            {
                caster.SetSkill(definition.ActiveSkill);
            }

            if (playerObject.TryGetComponent(out BasicAttacker attacker))
            {
                attacker.SetProfile(definition.BasicAttack);
            }

            // TODO: 패시브 부착 — definition.Passive.CreateBehaviour()로 전략을 생성해 보관/틱할 주체
            //       (Player 측 PassiveHolder 또는 PlayerStats) 결정 후 연결.
            // TODO: 직업 대표색/아이콘 → Art.JobColorConfig(jobId 키)·UI 오버헤드 표시 연동.
        }

        private JobDefinition FindJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return null;
            }

            for (int i = 0; i < availableJobs.Count; i++)
            {
                JobDefinition candidate = availableJobs[i];
                if (candidate != null && candidate.JobId == jobId)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
