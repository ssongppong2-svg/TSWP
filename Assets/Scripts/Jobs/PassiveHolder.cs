// 근거: 직업 시스템.md — 모든 직업은 하나의 고유 패시브를 가진다.
// 근거: PassiveDefinition.cs 주석 — "보유/틱 주체는 플레이어 직업 조립 지점(Player 측 PassiveHolder)".
//   패시브 로직(IPassiveBehaviour)의 수명(부착/해제/틱)을 관리하는 컴포넌트를 Jobs 측에 둔다
//   (Player 폴더는 직업 데이터를 알지 않아도 되도록 — 직업 조립은 Jobs 소관).
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>
    /// 플레이어 오브젝트에 붙는 패시브 보유자. JobSelectionManager.ApplyJobTo가 SetPassive로 주입한다.
    /// 컴포넌트가 없어도 게임은 정상 동작한다 (패시브만 빠진다).
    /// </summary>
    [DisallowMultipleComponent]
    public class PassiveHolder : MonoBehaviour
    {
        [Tooltip("직업 조립 시 주입된다. 테스트용으로 직접 지정해도 된다.")]
        [SerializeField] private PassiveDefinition passive;

        private IPassiveBehaviour _behaviour;
        private bool _attached;

        /// <summary>현재 패시브 정의 (없으면 null).</summary>
        public PassiveDefinition Passive => passive;

        /// <summary>현재 살아 있는 패시브 로직 (정의가 로직을 제공하지 않으면 null).</summary>
        public IPassiveBehaviour Behaviour => _behaviour;

        private void OnEnable() => Attach();
        private void OnDisable() => Detach();

        private void Update()
        {
            // 매 프레임 갱신이 필요 없는 패시브는 Tick을 빈 구현으로 둔다.
            _behaviour?.Tick(Time.deltaTime);
        }

        /// <summary>직업 조립 시 패시브 주입. 기존 패시브는 정상적으로 해제된다.</summary>
        public void SetPassive(PassiveDefinition newPassive)
        {
            if (passive == newPassive && _attached) return;

            Detach();
            passive = newPassive;

            if (isActiveAndEnabled) Attach();
        }

        private void Attach()
        {
            if (_attached || passive == null) return;

            // 정의가 로직을 제공하지 않을 수 있다 (데이터 전용 패시브) — null 허용 계약.
            _behaviour = passive.CreateBehaviour();
            _attached = true;
            _behaviour?.OnAttach(gameObject);
        }

        private void Detach()
        {
            if (!_attached) return;

            _behaviour?.OnDetach(gameObject);
            _behaviour = null;
            _attached = false;
        }
    }
}
