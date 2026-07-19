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

            const float width = 260f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, 210f), GUIContent.none, _boxStyle);

            GUILayout.Label("TSWP 프로토타입 테스트", _labelStyle);
            GUILayout.Space(4f);
            GUILayout.Label("A / D : 이동", _labelStyle);
            GUILayout.Label("Space : 점프", _labelStyle);
            GUILayout.Label("Shift : 달리기", _labelStyle);
            GUILayout.Label("좌클릭 : 기본 공격", _labelStyle);
            GUILayout.Label("E : 상호작용", _labelStyle);

            GUILayout.Space(6f);

            if (player != null)
            {
                GUILayout.Label($"접지: {(player.IsGrounded ? "O" : "X")}   방향: {(player.FacingSign > 0 ? "→" : "←")}", _labelStyle);
                GUILayout.Label($"이동속도: {player.MoveSpeed:0.#}", _labelStyle);
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
