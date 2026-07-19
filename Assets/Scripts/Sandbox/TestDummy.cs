// 테스트 전용 — 공격이 실제로 적중하는지 눈으로 확인하기 위한 허수아비.
// 실제 적 구현은 TSWP.Enemies가 담당한다. 이 파일은 프로토타입 검증용이며 나중에 삭제해도 된다.
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Sandbox
{
    /// <summary>
    /// 피격 시 색이 번쩍이고 체력을 머리 위에 표시하는 훈련용 허수아비.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TestDummy : MonoBehaviour
    {
        [SerializeField] private Color normalColor = new Color(0.85f, 0.3f, 0.3f);
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private float flashSeconds = 0.12f;

        [Tooltip("사망 후 이 시간이 지나면 자동으로 부활한다 (반복 테스트용). 0 이하면 부활하지 않음.")]
        [SerializeField] private float autoReviveSeconds = 3f;

        private CombatEntity _entity;
        private SpriteRenderer _renderer;
        private float _flashTimer;
        private float _reviveTimer;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.color = normalColor;
        }

        private void OnEnable()
        {
            _entity.Damaged += OnDamaged;
            _entity.Died += OnDied;
        }

        private void OnDisable()
        {
            _entity.Damaged -= OnDamaged;
            _entity.Died -= OnDied;
        }

        private void OnDamaged(DamageInfo info)
        {
            _flashTimer = flashSeconds;
            Debug.Log($"[허수아비] 피해 {info.TotalDamage:0.#} — 남은 체력 {_entity.CurrentHp:0}/{_entity.MaxHp:0}");
        }

        private void OnDied(CombatEntity entity)
        {
            Debug.Log("[허수아비] 쓰러짐");
            _renderer.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _reviveTimer = autoReviveSeconds;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                _renderer.color = _flashTimer > 0f ? hitColor : normalColor;
            }

            if (_reviveTimer > 0f)
            {
                _reviveTimer -= Time.deltaTime;
                if (_reviveTimer <= 0f)
                {
                    _entity.Revive();
                    _renderer.color = normalColor;
                    Debug.Log("[허수아비] 부활 — 다시 때려보세요");
                }
            }
        }

        /// <summary>머리 위 체력 표시.</summary>
        private void OnGUI()
        {
            if (Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.9f);
            if (screen.z < 0f) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(screen.x - 50f, Screen.height - screen.y - 10f, 100f, 20f),
                      $"{_entity.CurrentHp:0} / {_entity.MaxHp:0}", style);
        }
    }
}
