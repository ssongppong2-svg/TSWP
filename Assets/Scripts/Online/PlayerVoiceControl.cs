// 근거: 온라인 시스템.md — 플레이어별 음소거와 개별 음량 조절을 지원한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Online
{
    /// <summary>
    /// 플레이어별 음소거/음량 관리와 거리 기반 음량 계산.
    /// </summary>
    public class PlayerVoiceControl : MonoBehaviour
    {
        public static PlayerVoiceControl Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private VoiceChatConfig config;

        [Tooltip("전체 음성 채팅 볼륨 (설정-오디오).")]
        [Range(0f, 1f)] public float masterVoiceVolume = 1f;

        private readonly HashSet<ulong> _muted = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> _individualVolume = new();

        /// <summary>말하는 상태가 바뀔 때 발행 — 머리 위 마이크 아이콘 표시용.</summary>
        public event Action<ulong, bool> SpeakingChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ── 음소거/음량 ───────────────────────────────────────────

        public void SetMuted(ulong playerId, bool muted)
        {
            if (muted) _muted.Add(playerId);
            else _muted.Remove(playerId);
        }

        public bool IsMuted(ulong playerId) => _muted.Contains(playerId);

        public void SetIndividualVolume(ulong playerId, float volume)
            => _individualVolume[playerId] = Mathf.Clamp01(volume);

        public float GetIndividualVolume(ulong playerId)
            => _individualVolume.TryGetValue(playerId, out float v) ? v : 1f;

        // ── 최종 음량 ─────────────────────────────────────────────

        /// <summary>
        /// 특정 화자의 최종 음량. 음소거 → 0, 아니면 거리 감쇠 × 개별 음량 × 마스터.
        /// </summary>
        public float GetEffectiveVolume(ulong speakerId, Vector2 listenerPos, Vector2 speakerPos)
        {
            if (IsMuted(speakerId)) return 0f;

            float spatial = config != null ? config.CalculateVolume(listenerPos, speakerPos) : 1f;
            return spatial * GetIndividualVolume(speakerId) * masterVoiceVolume;
        }

        /// <summary>말하기 상태 변경 통지. // TODO(Vivox): 실제 음성 활동 감지로 교체.</summary>
        public void SetSpeaking(ulong playerId, bool speaking)
        {
            SpeakingChanged?.Invoke(playerId, speaking);
            GameEvents.RaiseVoiceSpeakingChanged((int)playerId, speaking);
        }
    }
}
