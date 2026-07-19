// 근거: UI 시스템.md — 머리 위 플레이어 정보(닉네임/체력바/직업 아이콘, 저체력 시 체력바 색상 변경),
//   이모트(캐릭터 머리 위 표시), 음성 채팅 UI(말하는 플레이어는 닉네임 옆 마이크 아이콘, 음소거는 별도 아이콘).
//   / 이름 시스템.md — 표시 이름은 "[칭호] 닉네임" 형식.
// 이 데이터는 World Space 캔버스 계층이 소비한다 (UIManager 주석 참조).
using System;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>플레이어 머리 위 월드스페이스 UI 데이터 1인분.</summary>
    [Serializable]
    public sealed class OverheadInfo
    {
        public int PlayerId;

        /// <summary>"[칭호] 닉네임" 형식 표시명. Meta.PlayerIdentity.GetDisplayName()으로 조립한다.</summary>
        public string DisplayName;

        /// <summary>칭호 색상 종류. 실제 Color는 Art.TitleColorConfig가 매핑 (하드코딩 금지 — 이름 시스템.md '추후 변경 가능').</summary>
        public Meta.TitleColorType TitleColorType = Meta.TitleColorType.Default;

        public float Hp;
        public float MaxHp;

        /// <summary>체력바 비율 0~1.</summary>
        public float HpRatio => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);

        /// <summary>직업 아이콘 (Jobs.JobDefinition.icon).</summary>
        public Sprite JobIcon;

        /// <summary>직업 식별자 문자열 (직업 enum 금지 — jobId 문자열 규칙).</summary>
        public string JobId;

        /// <summary>저체력 여부 — 체력바 색상 변경 트리거.
        /// 실제 색 단계는 Art.HealthBarColorConfig(1.0/0.7/0.4/0.2/0.05)가 소유한다.</summary>
        public bool IsLowHp => HpRatio <= LowHpThreshold;

        /// <summary>저체력 임계값. // TODO(밸런스): 문서 미정 — Art.HealthBarColorConfig의 0.4 단계를 임시 채택.</summary>
        public const float LowHpThreshold = 0.4f;

        public bool IsDead;

        /// <summary>머리 위에 표시 중인 이모트 (없으면 null). EmoteData는 Core 한 곳에만 정의됨.</summary>
        public EmoteData ActiveEmote;

        /// <summary>이모트 표시 만료 시각(Time.unscaledTime). 뷰가 이 시각 이후 ActiveEmote를 지운다.</summary>
        public float EmoteExpireTime;

        /// <summary>말하는 중 — 닉네임 옆 마이크 아이콘. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public bool IsSpeaking;

        /// <summary>음소거됨 — 별도 아이콘. 로컬 클라이언트 설정 값(동기화 대상 아님).</summary>
        public bool IsMuted;
    }
}
