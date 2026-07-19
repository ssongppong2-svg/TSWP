// 근거: UI 시스템.md — 설정 변경 항목에 '데미지 숫자 표시'가 있다(UISettings.showDamageNumbers).
// 근거: 팔레트 시스템.md — 색은 정보다. 빨강=피해, 노랑=강조(치명타), 초록=회복.
// 근거: 게임 성경.md — 프로토타입 단계에서 밸런스를 '눈으로' 확인하기 위한 핵심 도구.
// 외부 패키지(TextMeshPro) 금지 규칙에 따라 표시는 IMGUI로 하고, 이 파일은 순수 데이터/수명만 소유한다.
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>떠오르는 숫자의 종류. 색·크기 규칙이 여기서 갈린다.</summary>
    public enum DamageNumberKind
    {
        /// <summary>일반 피해 — 빨강.</summary>
        Damage,
        /// <summary>치명타 — 노랑, 더 크게.</summary>
        Critical,
        /// <summary>회복 — 초록.</summary>
        Heal,
        /// <summary>아군 오사 — 구분되는 색(트롤 확인용).</summary>
        FriendlyFire,
    }

    /// <summary>
    /// 화면에 떠오르는 숫자 하나. MonoBehaviour가 아니다 —
    /// 숫자마다 GameObject를 만들면 8인 전투에서 생성/파괴가 폭주하므로
    /// DamageNumberSpawner가 배열 풀로 재사용한다.
    /// </summary>
    public class DamageNumber
    {
        /// <summary>월드 기준 현재 위치 (매 프레임 위로 떠오른다).</summary>
        public Vector3 WorldPosition;

        /// <summary>월드 기준 이동 속도.</summary>
        public Vector2 Velocity;

        public DamageNumberKind Kind;

        /// <summary>표시 문자열 (정수 캐시를 사용하므로 보통 할당이 없다).</summary>
        public string Text;

        public float Elapsed;
        public float Lifetime;

        /// <summary>치명타 '팝' 연출용 크기 배율.</summary>
        public float Scale = 1f;

        /// <summary>0~1 알파 (수명 후반에 서서히 사라진다).</summary>
        public float Alpha = 1f;

        /// <summary>수명이 끝났는가.</summary>
        public bool IsExpired => Elapsed >= Lifetime;

        /// <summary>풀에서 꺼내 재사용할 때 상태를 초기화한다.</summary>
        public void Setup(Vector3 worldPosition, Vector2 velocity, DamageNumberKind kind,
                          string text, float lifetime)
        {
            WorldPosition = worldPosition;
            Velocity = velocity;
            Kind = kind;
            Text = text;
            Lifetime = Mathf.Max(0.05f, lifetime);
            Elapsed = 0f;
            Scale = 1f;
            Alpha = 1f;
        }

        /// <summary>
        /// 시간 진행. 히트스톱(timeScale 0 근처) 중에도 숫자는 읽혀야 하므로 unscaled 델타를 받는다.
        /// </summary>
        /// <param name="unscaledDeltaTime">unscaled 프레임 시간</param>
        /// <param name="gravity">위로 뜬 뒤 감속시키는 값 (월드 단위/초^2)</param>
        /// <param name="fadeStartRatio">이 비율을 넘긴 시점부터 알파가 줄어든다</param>
        /// <param name="popScale">치명타가 시작할 때의 확대 배율</param>
        public void Tick(float unscaledDeltaTime, float gravity, float fadeStartRatio, float popScale)
        {
            Elapsed += unscaledDeltaTime;

            Velocity.y -= gravity * unscaledDeltaTime;
            WorldPosition += (Vector3)(Velocity * unscaledDeltaTime);

            float t = Lifetime > 0f ? Mathf.Clamp01(Elapsed / Lifetime) : 1f;

            // 알파: 후반부에만 사라진다 (초반에 흐려지면 읽기 어렵다).
            Alpha = t < fadeStartRatio
                ? 1f
                : 1f - Mathf.InverseLerp(fadeStartRatio, 1f, t);

            // 크기: 치명타만 '툭' 튀었다가 원래 크기로 수렴한다.
            if (Kind == DamageNumberKind.Critical)
            {
                float pop = Mathf.Clamp01(Elapsed / 0.12f); // TODO(밸런스): 문서 미정 — 팝 연출 시간
                Scale = Mathf.Lerp(popScale, 1f, pop);
            }
            else
            {
                Scale = 1f;
            }
        }
    }
}
