// 근거: UI 시스템.md — HUD는 플레이 중 항상 표시. 표시 요소 9종:
//   체력 / 직업 아이콘 / 장착 아이템 / 스킬 쿨타임 / 부활 횟수 / 현재 방 / 미니맵 / 핑 / 음성 표시.
// 규칙: UI는 게임 로직을 직접 참조하지 않는다 — 갱신은 오직 Core.GameEvents 구독으로만 한다
//   (ARCHITECTURE.md §3-5). 장착 아이템도 ItemDefinition 참조 대신 itemCode 문자열만 보관하고,
//   아이콘/툴팁은 뷰가 itemCode로 조회한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>HUD 전체 뷰모델. 로컬 플레이어 1인 기준.</summary>
    public sealed class HudModel
    {
        /// <summary>로컬 플레이어 id. 다른 playerId의 이벤트는 파티 패널로만 흘린다.</summary>
        public int LocalPlayerId { get; private set; }

        // ── 체력 ──────────────────────────────────────────────────
        public float Hp;
        public float MaxHp;
        public float HpRatio => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);
        public bool IsDead;

        // ── 직업 ──────────────────────────────────────────────────
        /// <summary>직업 식별자 문자열 (직업 enum 금지). 아이콘/색은 뷰가 jobId로 조회.</summary>
        public string JobId;
        public Sprite JobIcon;

        // ── 장착 아이템 5칸 ───────────────────────────────────────
        /// <summary>장착 슬롯 itemCode 배열. 길이는 GameRules.EquipSlotCount(5) 고정. 빈 칸은 null.</summary>
        public readonly string[] EquippedItemCodes = new string[GameRules.EquipSlotCount];

        // ── 스킬 쿨타임 ───────────────────────────────────────────
        public readonly List<SkillCooldownInfo> SkillCooldowns = new List<SkillCooldownInfo>();

        // ── 공유 부활 횟수 ────────────────────────────────────────
        /// <summary>남은 공유 부활 횟수 (팀 공유 자원). 적어질수록 뷰가 색을 바꾼다.
        /// // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public int SharedReviveCount;

        /// <summary>부활 횟수 경고 임계값. // TODO(밸런스): 문서 미정 — 색상 변경 단계 임시 값.</summary>
        public const int ReviveWarningThreshold = 3;
        public bool IsReviveLow => SharedReviveCount <= ReviveWarningThreshold;

        // ── 현재 방 ───────────────────────────────────────────────
        public int CurrentRoomId = -1;
        /// <summary>현재 방 표시 문구. roomId만 이벤트로 오므로 뷰가 방 이름을 조회해 채운다.</summary>
        public string CurrentRoomLabel;

        // ── 미니맵 / 음성 ─────────────────────────────────────────
        public readonly MinimapViewModel Minimap = new MinimapViewModel();

        /// <summary>말하는 중인 플레이어 집합. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public readonly HashSet<int> SpeakingPlayerIds = new HashSet<int>();

        /// <summary>파티 패널 항목 (좌측). playerId 기준.</summary>
        public readonly List<PartyMemberInfo> PartyMembers = new List<PartyMemberInfo>();

        /// <summary>보스 상단 UI.</summary>
        public readonly BossUIModel Boss = new BossUIModel();

        /// <summary>상호작용 프롬프트.</summary>
        public readonly InteractionPromptModel InteractionPrompt = new InteractionPromptModel();

        /// <summary>뷰 갱신 통지 (Update 폴링 대신 이벤트 구독 — ARCHITECTURE.md §3-8).</summary>
        public event Action Changed;

        private bool _subscribed;

        public void Initialize(int localPlayerId)
        {
            LocalPlayerId = localPlayerId;
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            GameEvents.PlayerHealthChanged += OnPlayerHealthChanged;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRevived += OnPlayerRevived;
            GameEvents.ReviveCountChanged += OnReviveCountChanged;
            GameEvents.ItemAcquired += OnItemAcquired;
            GameEvents.ItemDropped += OnItemDropped;
            GameEvents.RoomEntered += OnRoomEntered;
            GameEvents.VoiceSpeakingChanged += OnVoiceSpeakingChanged;

            Minimap.Subscribe();
            Boss.Subscribe();
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            GameEvents.PlayerHealthChanged -= OnPlayerHealthChanged;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRevived -= OnPlayerRevived;
            GameEvents.ReviveCountChanged -= OnReviveCountChanged;
            GameEvents.ItemAcquired -= OnItemAcquired;
            GameEvents.ItemDropped -= OnItemDropped;
            GameEvents.RoomEntered -= OnRoomEntered;
            GameEvents.VoiceSpeakingChanged -= OnVoiceSpeakingChanged;

            Minimap.Unsubscribe();
            Boss.Unsubscribe();
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void OnPlayerHealthChanged(int playerId, float hp, float maxHp)
        {
            var member = FindOrAddMember(playerId);
            member.Hp = hp;
            member.MaxHp = maxHp;

            if (playerId == LocalPlayerId)
            {
                Hp = hp;
                MaxHp = maxHp;
            }
            Changed?.Invoke();
        }

        private void OnPlayerDied(int playerId)
        {
            FindOrAddMember(playerId).IsDead = true;
            if (playerId == LocalPlayerId) IsDead = true;
            Changed?.Invoke();
        }

        private void OnPlayerRevived(int playerId)
        {
            FindOrAddMember(playerId).IsDead = false;
            if (playerId == LocalPlayerId) IsDead = false;
            Changed?.Invoke();
        }

        private void OnReviveCountChanged(int remaining)
        {
            SharedReviveCount = remaining;
            Changed?.Invoke();
        }

        private void OnItemAcquired(int playerId, string itemCode)
        {
            if (playerId != LocalPlayerId) return;
            for (int i = 0; i < EquippedItemCodes.Length; i++)
            {
                if (!string.IsNullOrEmpty(EquippedItemCodes[i])) continue;
                EquippedItemCodes[i] = itemCode;
                break;
            }
            // NOTE(기획 확인 필요): 5칸이 모두 찬 상태의 교체는 Items.PlayerEquipment가 결정하며
            //   ItemDropped(교체로 버려진 아이템) 이벤트가 함께 발행되는 것을 전제로 한다.
            Changed?.Invoke();
        }

        private void OnItemDropped(int playerId, string itemCode)
        {
            if (playerId != LocalPlayerId) return;
            for (int i = 0; i < EquippedItemCodes.Length; i++)
            {
                if (EquippedItemCodes[i] != itemCode) continue;
                EquippedItemCodes[i] = null;
                break;
            }
            Changed?.Invoke();
        }

        private void OnRoomEntered(int roomId)
        {
            CurrentRoomId = roomId;
            Changed?.Invoke();
        }

        private void OnVoiceSpeakingChanged(int playerId, bool speaking)
        {
            if (speaking) SpeakingPlayerIds.Add(playerId);
            else SpeakingPlayerIds.Remove(playerId);

            var member = FindOrAddMember(playerId);
            member.IsSpeaking = speaking;
            Changed?.Invoke();
        }

        // ── 조회/보조 ─────────────────────────────────────────────
        public PartyMemberInfo FindOrAddMember(int playerId)
        {
            for (int i = 0; i < PartyMembers.Count; i++)
            {
                if (PartyMembers[i].PlayerId == playerId) return PartyMembers[i];
            }
            var info = new PartyMemberInfo { PlayerId = playerId };
            PartyMembers.Add(info);
            return info;
        }

        /// <summary>스킬 쿨타임 갱신. Jobs.SkillCaster가 CooldownTimer 값을 밀어 넣는 지점.
        /// TODO: 쿨타임 진행도 전용 GameEvents가 없어 직접 호출로 둔다 (GameEvents 수정 금지).</summary>
        public void UpdateSkillCooldown(string skillId, float remaining, float total, bool usable)
        {
            SkillCooldownInfo info = null;
            for (int i = 0; i < SkillCooldowns.Count; i++)
            {
                if (SkillCooldowns[i].SkillId != skillId) continue;
                info = SkillCooldowns[i];
                break;
            }
            if (info == null)
            {
                info = new SkillCooldownInfo { SkillId = skillId };
                SkillCooldowns.Add(info);
            }
            info.SetCooldown(remaining, total);
            // 침묵 등으로 사용이 막힌 경우 쿨타임과 별개로 회색 처리 (StatusEffectController.CanUseSkill 질의 결과).
            info.IsUsable = usable && info.RemainingCooldown <= 0f;
            Changed?.Invoke();
        }

        /// <summary>런 시작/스테이지 전환 시 초기화.</summary>
        public void ResetForNewRun()
        {
            Hp = MaxHp = 0f;
            IsDead = false;
            for (int i = 0; i < EquippedItemCodes.Length; i++) EquippedItemCodes[i] = null;
            SkillCooldowns.Clear();
            PartyMembers.Clear();
            SpeakingPlayerIds.Clear();
            CurrentRoomId = -1;
            CurrentRoomLabel = null;
            Minimap.Clear();
            Changed?.Invoke();
        }
    }
}
