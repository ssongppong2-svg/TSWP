// 근거: 직업 시스템.md — 모든 직업은 하나의 고유 패시브를 가진다.
// 패시브는 단순 능력치 증가보다 플레이 스타일을 변화시키는 효과를 우선한다
// → 로직을 데이터(SO)와 분리해 IPassiveBehaviour 전략 패턴으로 구현한다 (스펙 unityNotes ②).
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>
    /// 패시브 효과 로직 전략. 직업별 파생 PassiveDefinition이 CreateBehaviour로 구현체를 반환한다.
    /// 보유/틱 주체는 플레이어 직업 조립 지점(Player 측 — 추후 PassiveHolder 또는 PlayerStats)이다.
    /// 단순 스탯형 패시브는 OnAttach에서 Core.StatCollection에 modifier를 추가하는 방식으로도 구현 가능하지만,
    /// 문서 취지(새로운 플레이 방식 우선)에 따라 이벤트 구독형 로직을 권장한다.
    /// </summary>
    public interface IPassiveBehaviour
    {
        /// <summary>직업 조립 시 1회 호출 — 이벤트 구독/스탯 modifier 부여 등 초기화.</summary>
        void OnAttach(GameObject owner);

        /// <summary>해제 시(직업 재조립·오브젝트 정리 등) 호출 — 구독 해제/modifier 제거.</summary>
        void OnDetach(GameObject owner);

        /// <summary>매 프레임 갱신이 필요한 패시브만 사용 (필요 없으면 빈 구현).</summary>
        void Tick(float deltaTime);
    }

    /// <summary>직업당 정확히 1개의 패시브 정의. 데이터는 여기, 로직은 CreateBehaviour가 반환하는 전략.</summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Passive", fileName = "Passive_")]
    public class PassiveDefinition : ScriptableObject
    {
        [Header("식별")]
        [SerializeField] private string passiveId;
        [SerializeField] private string displayName;

        [TextArea]
        [Tooltip("효과 설명 — 플레이 스타일을 변화시키는 효과를 목표로 한다 (단순 능력치 증가 지양).")]
        [SerializeField] private string description;

        public string PassiveId => passiveId;
        public string DisplayName => displayName;
        public string Description => description;

        /// <summary>
        /// 효과 로직 생성 — 직업별 파생 SO가 오버라이드해 구현체를 반환한다 (전략 패턴).
        /// 기본 구현은 로직 없음(null 반환): 데이터 전용 뼈대. 호출측은 null 허용 처리한다.
        /// </summary>
        public virtual IPassiveBehaviour CreateBehaviour()
        {
            // TODO: 직업별 파생 SO(예: warrior/doctor/psycho 패시브)가 구현체를 반환한다.
            return null;
        }
    }
}
