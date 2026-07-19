// 근거: 맵 시스템.md — 각 맵은 고유한 생물 군계와 분위기를 가진다.
// 근거: 게임 성경.md — 스트리머를 위한 게임. 새 맵 진입은 "여기서부터 다른 세계"임을 각인시키는 순간이다.
// 근거: UI 시스템.md — 접근성(화면 흔들림/번쩍임 감소)을 존중해야 한다.
//
// Cinemachine/Timeline 없이 코드로 카메라를 움직인다 (뼈대 단계 외부 패키지 금지 — ARCHITECTURE.md §1).
// 맵마다 MapIntroData 에셋만 만들면 되고, 이 코드는 손대지 않는다.
using System;
using System.Collections;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Map
{
    /// <summary>
    /// 맵 진입 시네마틱 재생기. 씬에 1개 배치한다.
    /// 재생 중에는 플레이어 조작이 잠기고, 끝나면 풀린다.
    /// </summary>
    public class MapIntroManager : MonoBehaviour
    {
        public static MapIntroManager Instance { get; private set; }

        [Header("데이터")]
        [Tooltip("이 씬에서 재생할 인트로. 비우면 아무것도 하지 않는다.")]
        [SerializeField] private MapIntroData intro;

        [Tooltip("씬 시작과 동시에 자동 재생한다.")]
        [SerializeField] private bool playOnStart = true;

        [Header("참조")]
        [Tooltip("연출에 사용할 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("환경음/BGM 재생용. 없으면 자동 생성한다.")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource bgmSource;

        /// <summary>인트로 재생 중인가. 조작 잠금 판단에 쓴다.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>인트로가 끝났을 때(또는 스킵됐을 때) 발행. 조작 해제 시점.</summary>
        public event Action IntroFinished;

        // 연출 상태 — OnGUI가 읽는다
        private float _fadeAlpha = 1f;   // 1 = 완전 암전
        private float _titleAlpha;
        private string _title = "";
        private string _subtitle = "";
        private Color _titleColor = Color.white;
        private Color _subtitleColor = Color.white;
        private Color _fadeColor = Color.black;

        private Coroutine _routine;
        private Texture2D _solidTexture;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;

        // SYNC: 호스트 권위 — 멀티에서는 호스트가 시작 신호를 보내고 전원이 같은 시각에 재생한다.
        // 지금은 로컬 단독 실행이며, 네트워크 도입 시 PlayIntro를 RPC로 감싼다.
        // 전원이 스킵해야 넘어갈지, 한 명이 스킵하면 모두 넘어갈지는 기획 확인 필요.
        // NOTE(기획 확인 필요): 멀티 스킵 정책 미정 — 우선 로컬 스킵만 구현.

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
        }

        private void Start()
        {
            if (!playOnStart) return;
            TryPlay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_solidTexture != null) Destroy(_solidTexture);
        }

        // ── 재생 ──────────────────────────────────────────────────

        /// <summary>
        /// 인트로 재생을 시도한다. 이미 본 맵이면 건너뛰고 즉시 조작을 넘긴다.
        /// </summary>
        public bool TryPlay()
        {
            if (intro == null)
            {
                FinishImmediately();
                return false;
            }

            // 재입장 시 생략 — 처음 진입할 때만 재생한다.
            if (intro.playOnlyOnce && MapIntroHistory.HasSeen(intro.mapId))
            {
                FinishImmediately();
                return false;
            }

            Play(intro);
            return true;
        }

        /// <summary>특정 인트로를 강제 재생한다 (디버그/테스트용).</summary>
        public void Play(MapIntroData data)
        {
            if (data == null) return;

            if (_routine != null) StopCoroutine(_routine);

            intro = data;
            _routine = StartCoroutine(PlayRoutine(data));
        }

        /// <summary>
        /// 인트로 재생. 구간을 코루틴으로 쪼개지 않고 하나의 타임라인으로 돌린다 —
        /// 경과 시간만 있으면 암전·제목·카메라를 각각 계산할 수 있어 구조가 단순하고
        /// 스킵 처리도 한 곳에서 끝난다.
        /// </summary>
        private IEnumerator PlayRoutine(MapIntroData data)
        {
            IsPlaying = true;

            _title = data.title;
            _subtitle = data.subtitle;
            _titleColor = data.titleColor;
            _subtitleColor = data.subtitleColor;
            _fadeColor = data.fadeColor;
            _fadeAlpha = 1f;
            _titleAlpha = 0f;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;

            Vector3 camStart = Vector3.zero, camEnd = Vector3.zero;
            float zoomFrom = 5f, zoomTo = 5f;

            if (cam != null)
            {
                camStart = new Vector3(
                    cam.transform.position.x + data.cameraStartOffset.x,
                    cam.transform.position.y + data.cameraStartOffset.y,
                    cam.transform.position.z);

                camEnd = ResolveCameraEnd(camStart, data);

                zoomFrom = data.zoomFrom > 0f ? data.zoomFrom : cam.orthographicSize;
                zoomTo = data.zoomTo > 0f ? data.zoomTo : zoomFrom;

                cam.transform.position = camStart;
                cam.orthographicSize = zoomFrom;
            }

            // 암전 상태로 환경음이 먼저 들린다 — 소리가 먼저 오면 장소가 먼저 상상된다.
            PlayAmbient(data);
            StartBgm(data);

            // 카메라는 마지막 여운을 뺀 구간에 걸쳐 천천히 움직인다.
            float moveDuration = Mathf.Max(0.01f, data.TotalDuration - data.tailDuration);
            float total = data.TotalDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                if (ShouldSkip(data))
                {
                    yield return SkipTail(data);
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime; // 히트스톱·일시정지에 영향받지 않는다

                _fadeAlpha = EvaluateFade(elapsed, data);
                _titleAlpha = EvaluateTitle(elapsed, data);
                UpdateCamera(cam, camStart, camEnd, zoomFrom, zoomTo, elapsed / moveDuration, data);

                yield return null;
            }

            Finish(data);
        }

        /// <summary>경과 시간에 따른 암전 알파. 1 = 완전 암전.</summary>
        private static float EvaluateFade(float elapsed, MapIntroData data)
        {
            if (elapsed <= data.blackHold) return 1f;

            float t = elapsed - data.blackHold;
            if (t >= data.fadeInDuration) return 0f;

            return 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.fadeInDuration));
        }

        /// <summary>경과 시간에 따른 제목 알파.</summary>
        private static float EvaluateTitle(float elapsed, MapIntroData data)
        {
            float start = data.blackHold + data.fadeInDuration + data.titleDelay;
            if (elapsed <= start) return 0f;

            float t = elapsed - start;

            if (t < data.titleFadeIn)
                return Mathf.Clamp01(t / Mathf.Max(0.01f, data.titleFadeIn));

            t -= data.titleFadeIn;
            if (t < data.titleHold) return 1f;

            t -= data.titleHold;
            if (t < data.titleFadeOut)
                return 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.titleFadeOut));

            return 0f;
        }

        /// <summary>스킵 시 짧게 정리하고 끝낸다 — 갑자기 끊기지 않게 아주 짧은 페이드만 준다.</summary>
        private IEnumerator SkipTail(MapIntroData data)
        {
            const float quickFade = 0.15f;
            float start = _titleAlpha;
            float t = 0f;

            while (t < quickFade)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / quickFade);
                _titleAlpha = Mathf.Lerp(start, 0f, k);
                _fadeAlpha = Mathf.Lerp(_fadeAlpha, 0f, k);
                yield return null;
            }

            _titleAlpha = 0f;
            _fadeAlpha = 0f;
            Finish(data);
        }

        /// <summary>아무 키나 누르면 스킵 (ESC 포함). 마우스 클릭도 허용한다.</summary>
        private bool ShouldSkip(MapIntroData data)
        {
            if (data == null || !data.skippable) return false;
            return Input.anyKeyDown;
        }

        private void Finish(MapIntroData data)
        {
            if (data != null && !string.IsNullOrEmpty(data.mapId))
                MapIntroHistory.MarkSeen(data.mapId);

            _fadeAlpha = 0f;
            _titleAlpha = 0f;
            IsPlaying = false;
            _routine = null;

            IntroFinished?.Invoke();
        }

        private void FinishImmediately()
        {
            _fadeAlpha = 0f;
            _titleAlpha = 0f;
            IsPlaying = false;
            IntroFinished?.Invoke();
        }

        // ── 카메라 ────────────────────────────────────────────────

        private Vector3 ResolveCameraEnd(Vector3 start, MapIntroData data)
        {
            Vector3 end = start;
            switch (data.cameraMove)
            {
                case MapIntroCameraMove.PanRight: end.x += data.cameraDistance; break;
                case MapIntroCameraMove.PanLeft: end.x -= data.cameraDistance; break;
                case MapIntroCameraMove.PanUp: end.y += data.cameraDistance; break;
                case MapIntroCameraMove.PanDown: end.y -= data.cameraDistance; break;
            }
            return end;
        }

        private void UpdateCamera(Camera cam, Vector3 start, Vector3 end,
                                  float zoomFrom, float zoomTo, float progress, MapIntroData data)
        {
            if (cam == null) return;

            float k = data.cameraCurve != null
                ? data.cameraCurve.Evaluate(Mathf.Clamp01(progress))
                : Mathf.Clamp01(progress);

            cam.transform.position = Vector3.Lerp(start, end, k);

            if (data.cameraMove == MapIntroCameraMove.ZoomIn || data.cameraMove == MapIntroCameraMove.ZoomOut)
                cam.orthographicSize = Mathf.Lerp(zoomFrom, zoomTo, k);
        }

        // ── 사운드 ────────────────────────────────────────────────

        private void EnsureAudioSources()
        {
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.playOnAwake = false;
                ambientSource.loop = true;
            }

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }
        }

        private void PlayAmbient(MapIntroData data)
        {
            if (data.ambientSound == null || ambientSource == null) return;

            ambientSource.clip = data.ambientSound;
            ambientSource.volume = data.ambientVolume;
            ambientSource.Play();
        }

        private void StartBgm(MapIntroData data)
        {
            if (data.bgm == null || bgmSource == null) return;

            bgmSource.clip = data.bgm;
            bgmSource.volume = 0f;
            bgmSource.Play();
            StartCoroutine(FadeBgm(data.bgmVolume, data.bgmFadeIn));
        }

        private IEnumerator FadeBgm(float targetVolume, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (bgmSource != null)
                    bgmSource.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        // ── 화면 표시 ─────────────────────────────────────────────
        // TODO(UI): UGUI 캔버스로 교체 — 지금은 폰트 에셋 없이 동작하도록 IMGUI를 쓴다.
        //           실제 출시본은 픽셀 폰트(도트 시스템.md)를 적용한 Canvas로 옮긴다.

        private void OnGUI()
        {
            if (_fadeAlpha <= 0.001f && _titleAlpha <= 0.001f) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureGuiResources();

            // 암전 레이어
            if (_fadeAlpha > 0.001f)
            {
                Color fade = _fadeColor;
                fade.a = _fadeAlpha;
                GUI.color = fade;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _solidTexture);
                GUI.color = Color.white;
            }

            if (_titleAlpha <= 0.001f) return;

            // 제목 — 화면 중앙
            float centerY = Screen.height * 0.5f;

            _titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.09f);
            _subtitleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.028f);

            Color titleColor = _titleColor;
            titleColor.a *= _titleAlpha;

            Color subtitleColor = _subtitleColor;
            subtitleColor.a *= _titleAlpha;

            // 가독성을 위한 옅은 그림자 (팔레트 시스템.md — 순수 검정 대신 짙은 남색)
            var shadow = new Color(0.04f, 0.04f, 0.08f, 0.6f * _titleAlpha);

            var titleRect = new Rect(0f, centerY - Screen.height * 0.09f, Screen.width, Screen.height * 0.14f);
            var subtitleRect = new Rect(0f, centerY + Screen.height * 0.04f, Screen.width, Screen.height * 0.05f);

            GUI.color = shadow;
            GUI.Label(new Rect(titleRect.x + 3f, titleRect.y + 3f, titleRect.width, titleRect.height), _title, _titleStyle);

            GUI.color = titleColor;
            GUI.Label(titleRect, _title, _titleStyle);

            GUI.color = subtitleColor;
            GUI.Label(subtitleRect, _subtitle, _subtitleStyle);

            GUI.color = Color.white;
        }

        private void EnsureGuiResources()
        {
            if (_solidTexture == null)
            {
                _solidTexture = new Texture2D(1, 1);
                _solidTexture.SetPixel(0, 0, Color.white);
                _solidTexture.Apply();
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                _titleStyle.normal.textColor = Color.white;
            }

            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Normal,
                };
                _subtitleStyle.normal.textColor = Color.white;
            }
        }

#if UNITY_EDITOR
        /// <summary>테스트 씬 빌더가 데이터를 주입할 때 사용.</summary>
        public void SetIntro(MapIntroData data) => intro = data;
#endif
    }
}
