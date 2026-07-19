// 근거: 맵 시스템.md — 각 맵은 하나의 생물 군계(숲/동굴/설원 등)를 가지며 고유한 배경을 제공한다.
// 근거: 게임 성경.md — 스트리머를 위한 게임. 새 맵 진입은 "여기서부터 다른 세계"임을 각인시키는 순간이다.
// 맵마다 이 에셋만 만들면 인트로가 완성된다 — 코드 수정 없이 THE FOREST → THE GLACIER → ... 확장.
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>
    /// 인트로 카메라가 훑는 방식.
    /// 코드 기반 이동이라 Cinemachine/Timeline 없이도 동작한다(패키지 의존 없음 — ARCHITECTURE.md §1).
    /// </summary>
    public enum MapIntroCameraMove
    {
        /// <summary>고정 — 카메라가 움직이지 않는다.</summary>
        Static,
        /// <summary>오른쪽으로 천천히 이동 (숲: 나무와 안개를 훑는다).</summary>
        PanRight,
        /// <summary>왼쪽으로 이동.</summary>
        PanLeft,
        /// <summary>위로 이동 (설원·성: 높이를 강조).</summary>
        PanUp,
        /// <summary>아래로 이동 (심연: 아래로 내려가는 압박).</summary>
        PanDown,
        /// <summary>줌 아웃 — 좁은 곳에서 넓은 전경으로.</summary>
        ZoomOut,
        /// <summary>줌 인 — 전경에서 한 곳으로 좁혀 들어간다.</summary>
        ZoomIn,
    }

    /// <summary>
    /// 맵 인트로 1개의 모든 데이터. 맵마다 이 SO를 하나씩 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Map Intro", fileName = "MapIntro_")]
    public class MapIntroData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("맵 식별자. 이미 본 인트로인지 판단하는 저장 키로 쓰인다.")]
        public string mapId = "forest";

        [Header("문구")]
        [Tooltip("화면 중앙 대문자 제목. 예: THE FOREST")]
        public string title = "THE FOREST";

        [Tooltip("제목 아래 작은 글씨. 예: Map 01 / The Beginning")]
        public string subtitle = "Map 01";

        [Header("타이밍 (초)")]
        [Tooltip("암전 상태로 환경음만 들리는 시간. 소리가 먼저 오면 장소가 먼저 상상된다.")]
        [Min(0f)] public float blackHold = 1.2f;

        [Tooltip("암전에서 화면이 밝아지는 시간.")]
        [Min(0f)] public float fadeInDuration = 1.5f;

        [Tooltip("제목이 떠오르기까지 기다리는 시간 (화면이 밝아진 뒤).")]
        [Min(0f)] public float titleDelay = 0.8f;

        [Tooltip("제목이 나타나는 시간.")]
        [Min(0f)] public float titleFadeIn = 1.0f;

        [Tooltip("제목이 완전히 보이는 유지 시간. 문서 요구: 3~4초.")]
        [Min(0f)] public float titleHold = 3.5f;

        [Tooltip("제목이 사라지는 시간.")]
        [Min(0f)] public float titleFadeOut = 1.2f;

        [Tooltip("제목이 사라진 뒤 조작을 넘기기까지의 여운.")]
        [Min(0f)] public float tailDuration = 0.4f;

        /// <summary>인트로 전체 길이(초).</summary>
        public float TotalDuration =>
            blackHold + fadeInDuration + titleDelay + titleFadeIn + titleHold + titleFadeOut + tailDuration;

        [Header("카메라")]
        public MapIntroCameraMove cameraMove = MapIntroCameraMove.PanRight;

        [Tooltip("카메라가 훑는 거리(월드 유닛). Pan 계열에서 사용.")]
        public float cameraDistance = 8f;

        [Tooltip("카메라 시작 위치 오프셋 — 플레이어 기준. 인트로는 여기서 시작해 목표로 이동한다.")]
        public Vector2 cameraStartOffset = new Vector2(-4f, 1.5f);

        [Tooltip("줌 계열에서 사용할 시작/끝 직교 크기. 0이면 현재 값 유지.")]
        public float zoomFrom;
        public float zoomTo;

        [Tooltip("카메라 이동 곡선. 기본은 부드러운 가감속(EaseInOut).")]
        public AnimationCurve cameraCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("사운드")]
        [Tooltip("환경음 — 새소리·바람소리 등. 암전 중 가장 먼저 들린다.")]
        public AudioClip ambientSound;

        [Tooltip("환경음 볼륨.")]
        [Range(0f, 1f)] public float ambientVolume = 0.7f;

        [Tooltip("맵 BGM. 인트로 중 서서히 올라온다.")]
        public AudioClip bgm;

        [Range(0f, 1f)] public float bgmVolume = 0.6f;

        [Tooltip("BGM이 최대 볼륨에 도달하는 시간.")]
        [Min(0.1f)] public float bgmFadeIn = 3f;

        [Header("표시")]
        [Tooltip("제목 색. 팔레트 시스템.md — 기본 텍스트는 흰색.")]
        public Color titleColor = Color.white;

        [Tooltip("부제 색 — 제목보다 옅게.")]
        public Color subtitleColor = new Color(0.8f, 0.8f, 0.85f, 1f);

        [Tooltip("암전 색. 순수 검정 대신 짙은 남색을 권장한다(팔레트 시스템.md 그림자 규칙).")]
        public Color fadeColor = new Color(0.04f, 0.04f, 0.07f, 1f);

        [Header("규칙")]
        [Tooltip("한 번 본 인트로는 다시 재생하지 않는다 (재입장 시 생략).")]
        public bool playOnlyOnce = true;

        [Tooltip("아무 키나 눌러 건너뛸 수 있다.")]
        public bool skippable = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(mapId))
                Debug.LogWarning($"[MapIntroData] '{name}': mapId가 비어 있으면 '이미 봤는지' 판단이 불가능합니다.", this);

            // 문서 요구: 제목은 3~4초 유지
            if (titleHold < 3f || titleHold > 4f)
                Debug.LogWarning($"[MapIntroData] '{name}': 제목 유지 시간 권장 범위는 3~4초입니다 (현재 {titleHold:0.#}초).", this);
        }
#endif
    }
}
