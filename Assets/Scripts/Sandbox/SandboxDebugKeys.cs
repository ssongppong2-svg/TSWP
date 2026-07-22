// 테스트 전용 — 상태이상 화면 효과(색수차 등)를 눈으로 확인하기 위한 디버그 키.
// 실제 게임에서는 적·보스·환경이 상태이상을 부여한다. 이 파일은 프로토타입 검증용이다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Player;
using TSWP.StatusEffects;

namespace TSWP.Sandbox
{
    /// <summary>
    /// 숫자 키로 자신에게 상태이상을 걸어본다.
    /// 1 = 혼란, 2 = 공포, 3 = 감전, 4 = 중독, 0 = 전부 해제
    /// </summary>
    public class SandboxDebugKeys : MonoBehaviour
    {
        [SerializeField] private StatusEffectController target;

        [Tooltip("1~4번 키에 대응하는 상태이상 데이터.")]
        [SerializeField] private List<StatusEffectData> testEffects = new List<StatusEffectData>();

        public void SetTarget(StatusEffectController controller) => target = controller;
        public void SetEffects(List<StatusEffectData> effects) => testEffects = effects;

        private void Start()
        {
            if (target != null) return;

            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.GetComponent<StatusEffectController>();
        }

        private void Update()
        {
            if (target == null) return;

            for (int i = 0; i < testEffects.Count && i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;

                var data = testEffects[i];
                if (data == null) continue;

                target.ApplyEffect(data, gameObject);
                Debug.Log($"[디버그] 상태이상 적용: {data.DisplayNameKo}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                target.ClearAllEffects();
                Debug.Log("[디버그] 상태이상 전부 해제");
            }
        }
    }
}
