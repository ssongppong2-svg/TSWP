// 근거: 상태이상 시스템.md — "모든 상태이상은 명확한 효과를 가져야 한다".
// 근거: UI 시스템.md — 2초 안에 이해할 수 있어야 한다.
// 화면 후처리(색수차)만으로는 "무엇에 걸렸는지"를 알 수 없고, 남이 걸린 상태는 아예 안 보인다.
// 캐릭터에 이펙트를 붙이고 머리 위에 이름표를 띄워, 나와 팀원 모두의 상태를 즉시 알아채게 한다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.StatusEffects;

namespace TSWP.Art
{
    /// <summary>
    /// 상태이상의 월드 표현. StatusEffectController를 구독해 걸린 동안 이펙트를 붙이고
    /// 머리 위에 상태 이름을 표시한다.
    /// </summary>
    [RequireComponent(typeof(StatusEffectController))]
    public class StatusEffectVisual : MonoBehaviour
    {
        [Header("부착 이펙트")]
        [Tooltip("캐릭터 기준 이펙트 위치.")]
        [SerializeField] private Vector3 effectOffset = new Vector3(0f, 0.2f, 0f);

        [Header("이름표")]
        [Tooltip("머리 위 상태 이름 표시 여부.")]
        [SerializeField] private bool showLabels = true;

        [SerializeField] private Vector3 labelWorldOffset = new Vector3(0f, 1.1f, 0f);

        private StatusEffectController _controller;

        /// <summary>상태이상 종류별로 붙어 있는 이펙트 인스턴스.</summary>
        private readonly Dictionary<StatusEffectType, VfxPlayer> _attached = new();

        /// <summary>표시할 상태 목록 (이름표용).</summary>
        private readonly List<StatusEffectType> _activeTypes = new();

        private GUIStyle _labelStyle;

        private void Awake()
        {
            _controller = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            _controller.EffectApplied += OnApplied;
            _controller.EffectRemoved += OnRemoved;
        }

        private void OnDisable()
        {
            _controller.EffectApplied -= OnApplied;
            _controller.EffectRemoved -= OnRemoved;
            ClearAll();
        }

        private void OnApplied(StatusEffectInstance instance)
        {
            if (instance == null || instance.Data == null) return;

            StatusEffectType type = instance.Data.EffectType;

            if (!_activeTypes.Contains(type)) _activeTypes.Add(type);

            // 이미 붙어 있으면 다시 붙이지 않는다 (같은 상태 재적용 = 지속시간 갱신).
            if (_attached.ContainsKey(type)) return;

            string vfxId = VfxId.ForStatus(type);
            if (string.IsNullOrEmpty(vfxId)) return;

            var spawner = VfxSpawner.Instance;
            if (spawner == null) return;

            var player = spawner.PlayAttached(vfxId, transform, effectOffset);
            if (player != null) _attached[type] = player;
        }

        private void OnRemoved(StatusEffectType type)
        {
            _activeTypes.Remove(type);

            if (_attached.TryGetValue(type, out var player))
            {
                if (player != null) player.Stop();
                _attached.Remove(type);
            }
        }

        private void ClearAll()
        {
            foreach (var pair in _attached)
                if (pair.Value != null) pair.Value.Stop();

            _attached.Clear();
            _activeTypes.Clear();
        }

        /// <summary>머리 위 상태 이름 — 아이콘 에셋이 준비되기 전까지의 명확한 표시 수단.</summary>
        private void OnGUI()
        {
            if (!showLabels || _activeTypes.Count == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + labelWorldOffset);
            if (screen.z < 0f) return;

            EnsureStyle();

            float y = Screen.height - screen.y;
            for (int i = 0; i < _activeTypes.Count; i++)
            {
                var type = _activeTypes[i];
                _labelStyle.normal.textColor = GetStatusColor(type);

                GUI.Label(new Rect(screen.x - 60f, y - i * 16f, 120f, 16f),
                          GetStatusName(type), _labelStyle);
            }
        }

        private void EnsureStyle()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
            };
        }

        /// <summary>상태이상별 색 — 팔레트 시스템.md의 색상 의미를 따른다.</summary>
        private static Color GetStatusColor(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Burn: return new Color(1f, 0.55f, 0.2f);      // 화염 = 주황
                case StatusEffectType.Poison: return new Color(0.45f, 0.9f, 0.35f); // 독 = 초록
                case StatusEffectType.Freeze: return new Color(0.5f, 0.85f, 1f);    // 빙결 = 하늘
                case StatusEffectType.Shock: return new Color(1f, 0.95f, 0.3f);     // 감전 = 노랑
                case StatusEffectType.Confusion: return new Color(0.8f, 0.45f, 1f); // 혼란 = 보라
                case StatusEffectType.Fear: return new Color(0.65f, 0.35f, 0.95f);  // 공포 = 보라
                case StatusEffectType.Bleed: return new Color(0.95f, 0.25f, 0.35f); // 출혈 = 빨강
                case StatusEffectType.Stun: return new Color(1f, 0.9f, 0.4f);
                case StatusEffectType.Silence: return new Color(0.7f, 0.7f, 0.8f);
                default: return Color.white;
            }
        }

        /// <summary>한글 표기 — 상태이상 시스템.md의 이름을 그대로 쓴다.</summary>
        private static string GetStatusName(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Burn: return "화상";
                case StatusEffectType.Poison: return "중독";
                case StatusEffectType.Freeze: return "빙결";
                case StatusEffectType.Shock: return "감전";
                case StatusEffectType.Bleed: return "출혈";
                case StatusEffectType.Fear: return "공포";
                case StatusEffectType.Confusion: return "혼란";
                case StatusEffectType.Silence: return "침묵";
                case StatusEffectType.Slow: return "둔화";
                case StatusEffectType.Root: return "속박";
                case StatusEffectType.Stun: return "기절";
                case StatusEffectType.Weak: return "약화";
                case StatusEffectType.Vulnerable: return "취약";
                case StatusEffectType.HealBlock: return "회복 불가";
                case StatusEffectType.Knockback: return "넉백";
                case StatusEffectType.Launch: return "공중";
                default: return type.ToString();
            }
        }
    }
}
