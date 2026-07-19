// 근거: UI 시스템.md — UI 크기/투명도 조절 가능, 접근성(색약/자막/확대/흔들림 감소/플래시 감소) 지원.
//       설정 변경은 모든 패널이 즉시 반영해야 하므로 변경 이벤트로 브로드캐스트한다.
using System;
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>
    /// UI/접근성 설정의 저장·로드와 변경 통지. JsonUtility로 persistentDataPath에 보관한다.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string FileName = "tswp_ui_settings.json";

        [Serializable]
        private class SettingsPayload
        {
            public UISettings ui = new UISettings();
            public AccessibilitySettings accessibility = new AccessibilitySettings();
        }

        private SettingsPayload _payload = new SettingsPayload();

        public UISettings Ui => _payload.ui;
        public AccessibilitySettings Accessibility => _payload.accessibility;

        /// <summary>설정이 바뀔 때마다 발행 — 모든 패널이 구독해 uiScale/투명도 등을 반영한다.</summary>
        public event Action SettingsChanged;

        private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }

        public void Load()
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    string json = System.IO.File.ReadAllText(FilePath);
                    _payload = JsonUtility.FromJson<SettingsPayload>(json) ?? new SettingsPayload();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] 설정 로드 실패 — 기본값 사용: {e.Message}");
                _payload = new SettingsPayload();
            }

            _payload.ui?.Clamp();
            _payload.accessibility?.Clamp();
            SettingsChanged?.Invoke();
        }

        public void Save()
        {
            _payload.ui?.Clamp();
            _payload.accessibility?.Clamp();

            try
            {
                string json = JsonUtility.ToJson(_payload, true);
                System.IO.File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] 설정 저장 실패: {e.Message}");
            }

            SettingsChanged?.Invoke();
        }

        /// <summary>설정을 수정한 뒤 호출 — 저장과 통지를 함께 수행한다.</summary>
        public void ApplyAndSave() => Save();
    }
}
