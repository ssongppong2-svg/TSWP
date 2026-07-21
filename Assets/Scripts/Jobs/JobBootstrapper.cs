// 근거: 직업 시스템.md — 직업은 단일 플레이어 프리팹 + 데이터 주입으로 조립한다 (직업별 프리팹 분기 금지).
// 로비(Online.LobbyManager)가 아직 스텁이라 테스트 씬에서는 직업이 주입되지 않아
// Q를 눌러도 아무 일이 없다. 이 컴포넌트는 씬 재생 즉시 직업을 주입해 그 공백을 메운다.
// 정식 흐름(로비 → JobSelectionManager.ApplyJobTo)이 붙으면 이 컴포넌트는 제거하면 된다.
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>
    /// 플레이어 오브젝트에 붙여 시작 시 직업 데이터를 주입하는 테스트/부트스트랩 컴포넌트.
    /// 기본 공격 프로파일 · 액티브 스킬(Q) · 패시브가 한 번에 조립된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JobBootstrapper : MonoBehaviour
    {
        [Tooltip("주입할 직업. 비워 두면 아무것도 하지 않는다(로비가 주입하는 정식 흐름을 방해하지 않는다).")]
        [SerializeField] private JobDefinition job;

        [Tooltip("로비 선택 현황(JobSelectionManager)에도 등록할지. 직업 UI 확인용.")]
        [SerializeField] private bool registerToSelectionManager = true;

        /// <summary>현재 주입된 직업 (없으면 null).</summary>
        public JobDefinition Job => job;

        private void Start()
        {
            // Awake가 아닌 Start에서 주입한다 — SkillCaster/BasicAttacker/PlayerStats의 Awake가
            // 먼저 끝나야 SetSkill/SetProfile이 초기화된 상태 위에 얹힌다.
            Apply(job);
        }

        /// <summary>런타임 직업 교체 (테스트용). 쿨타임은 초기화된다.</summary>
        public void Apply(JobDefinition newJob)
        {
            if (newJob == null) return;
            job = newJob;

            JobSelectionManager.ApplyJob(job, gameObject);

            if (!registerToSelectionManager) return;

            var manager = JobSelectionManager.Instance;
            var entity = GetComponent<Combat.CombatEntity>();
            int playerId = entity != null ? entity.OwnerPlayerId : -1;
            if (manager != null && playerId >= 0)
            {
                manager.TrySelectJob(playerId, job.JobId); // 목록에 없는 jobId면 경고만 남고 조립은 이미 끝나 있다
            }
        }
    }
}
