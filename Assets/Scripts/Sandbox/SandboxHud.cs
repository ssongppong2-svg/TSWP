// 테스트 전용 — 조작법과 플레이어 상태를 화면에 표시한다.
// 실제 HUD는 TSWP.UI가 담당한다 (캔버스 3계층). 이 파일은 프로토타입 검증용이다.
using UnityEngine;
using TSWP.Combat;
using TSWP.Player;

namespace TSWP.Sandbox
{
    /// <summary>IMGUI로 조작 안내와 상태를 그리는 임시 HUD.</summary>
    public class SandboxHud : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        private CombatEntity _entity;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        public void SetPlayer(PlayerController target)
        {
            player = target;
            _entity = target != null ? target.GetComponent<CombatEntity>() : null;
        }

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null) _entity = player.GetComponent<CombatEntity>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            const float width = 280f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, 300f), GUIContent.none, _boxStyle);

            GUILayout.Label("TSWP 프로토타입 테스트", _labelStyle);
            GUILayout.Space(4f);
            GUILayout.Label("A / D : 이동      Space : 점프", _labelStyle);
            GUILayout.Label("Shift : 달리기    우클릭 : 대쉬", _labelStyle);
            GUILayout.Label("좌클릭 : 공격     E : 상호작용", _labelStyle);
            GUILayout.Space(3f);
            GUILayout.Label("1 혼란 · 2 공포 · 3 감전 · 4 중독", _labelStyle);
            GUILayout.Label("0 : 상태이상 해제", _labelStyle);
            GUILayout.Space(3f);
            GUILayout.Label("← 왼쪽: 추적 근접 적 (빨강)", _labelStyle);
            GUILayout.Label("→ 오른쪽: 원거리 저격수 (보라)", _labelStyle);

            GUILayout.Space(6f);

            if (player != null)
            {
                GUILayout.Label($"접지: {(player.IsGrounded ? "O" : "X")}   방향: {(player.FacingSign > 0 ? "→" : "←")}", _labelStyle);
                GUILayout.Label($"이동속도: {player.MoveSpeed:0.#}", _labelStyle);

                // 대쉬 상태 — 쿨타임 진행률을 막대로 표시
                string dashState = player.IsDashing ? "대쉬 중!" : (player.DashCooldownProgress >= 1f ? "준비됨" : "충전 중");
                GUILayout.Label($"대쉬: {dashState}", _labelStyle);

                Rect bar = GUILayoutUtility.GetRect(width - 40f, 8f);
                GUI.Box(bar, GUIContent.none);
                var fill = new Rect(bar.x, bar.y, bar.width * player.DashCooldownProgress, bar.height);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
            }

            if (_entity != null)
                GUILayout.Label($"체력: {_entity.CurrentHp:0} / {_entity.MaxHp:0}", _labelStyle);

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _labelStyle.normal.textColor = Color.white;
        }
    }
}
