// 근거: UI 시스템.md — 파티 정보는 화면 좌측에 표시.
//   표시 내용 5종: 플레이어 이름 / 체력 / 직업 / 사망 여부 / 음성 채팅 중 여부.
using System;
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>좌측 파티 패널 항목 1개.</summary>
    [Serializable]
    public sealed class PartyMemberInfo
    {
        public int PlayerId;

        /// <summary>"[칭호] 닉네임" 형식 (Meta.PlayerIdentity.GetDisplayName()).</summary>
        public string PlayerName;

        public float Hp;
        public float MaxHp;
        public float HpRatio => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);

        /// <summary>직업 식별자 문자열. 색상은 Art.JobColorConfig가 jobId 키로 매핑.</summary>
        public string JobId;
        public Sprite JobIcon;

        /// <summary>사망 여부 — 사망 시 항목을 회색 처리. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public bool IsDead;

        /// <summary>음성 채팅 중 여부. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public bool IsSpeaking;

        /// <summary>호스트(방장) 표시용. 로비 정보(Online.LobbyPlayerState)에서 채운다.</summary>
        public bool IsHost;
    }
}
