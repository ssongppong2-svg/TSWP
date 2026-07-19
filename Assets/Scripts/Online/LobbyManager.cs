// 근거: 온라인 시스템.md — 1~8인 로비, 방 코드/초대 참가, 방장이 난이도 선택, 전원 준비 시 시작.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 동일 직업 중복 선택 가능(8명 전원 같은 직업 허용).
// TODO(NGO+Steamworks): 현재는 로컬 상태만 관리하는 스텁이다. 실제 전송/세션은 네트워크 도입 시 연결한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Online
{
    /// <summary>
    /// 로비 생성/참가/준비 상태 관리. 방장 전용 동작은 반드시 IsHost 검증을 거친다.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [Header("로비")]
        [SerializeField] private LobbyVisibility visibility = LobbyVisibility.Private;

        private readonly List<LobbyPlayerState> _players = new List<LobbyPlayerState>();

        /// <summary>방 코드 — 비공개 로비 참가에 사용.</summary>
        public string RoomCode { get; private set; }

        public ulong HostPlayerId { get; private set; }
        public LobbyVisibility Visibility => visibility;
        public IReadOnlyList<LobbyPlayerState> Players => _players;
        public int PlayerCount => _players.Count;

        /// <summary>로비 상태가 바뀔 때 발행 — 로비 UI가 구독한다.</summary>
        public event Action LobbyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ── 로비 생성/참가 ────────────────────────────────────────

        /// <summary>새 로비를 생성하고 자신을 방장으로 등록한다.</summary>
        public void CreateLobby(ulong hostPlayerId, ulong steamId, string displayName)
        {
            // TODO(NGO): NetworkManager.StartHost() 호출 지점.
            _players.Clear();
            HostPlayerId = hostPlayerId;
            RoomCode = GenerateRoomCode();

            _players.Add(new LobbyPlayerState
            {
                playerId = hostPlayerId,
                steamId = steamId,
                displayName = displayName,
                isHost = true,
            });

            GameFlowManager.Instance?.EnterLobby();
            LobbyChanged?.Invoke();
        }

        /// <summary>기존 로비에 참가한다. 정원(8명) 초과면 실패.</summary>
        public bool JoinLobby(ulong playerId, ulong steamId, string displayName)
        {
            // TODO(NGO+Steamworks): 방 코드/초대로 세션에 연결한 뒤 호출되어야 한다.
            if (_players.Count >= GameRules.MaxPlayers)
            {
                Debug.LogWarning($"[LobbyManager] 정원 초과 — 최대 {GameRules.MaxPlayers}명");
                return false;
            }

            if (FindPlayer(playerId) != null) return false;

            _players.Add(new LobbyPlayerState
            {
                playerId = playerId,
                steamId = steamId,
                displayName = displayName,
            });

            LobbyChanged?.Invoke();
            return true;
        }

        public void LeaveLobby(ulong playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null) return;

            // 자발적 종료는 캐릭터를 즉시 제거한다 (재접속 대기 대상이 아니다).
            player.connectionState = ConnectionState.Left;
            _players.Remove(player);

            // 방장이 나가면 다음 인원에게 위임한다.
            if (player.isHost && _players.Count > 0)
            {
                _players[0].isHost = true;
                HostPlayerId = _players[0].playerId;
            }

            LobbyChanged?.Invoke();
        }

        // ── 로비 내 조작 ──────────────────────────────────────────

        /// <summary>직업 선택 — 중복 제한 없음 (8명 전원 같은 직업 가능).</summary>
        public void SelectJob(ulong playerId, string jobId)
        {
            var player = FindPlayer(playerId);
            if (player == null) return;

            player.selectedJobId = jobId;
            LobbyChanged?.Invoke();
        }

        public void SetReady(ulong playerId, bool ready)
        {
            var player = FindPlayer(playerId);
            if (player == null) return;

            player.isReady = ready;
            LobbyChanged?.Invoke();
        }

        /// <summary>난이도 선택 — 방장 전용.</summary>
        public bool SetDifficulty(ulong requesterId, Difficulty difficulty)
        {
            if (!IsHost(requesterId))
            {
                Debug.LogWarning("[LobbyManager] 난이도는 방장만 변경할 수 있습니다.");
                return false;
            }

            GameFlowManager.Instance?.SetDifficulty(difficulty);
            return true;
        }

        /// <summary>로비 공개 여부 — 방장 전용.</summary>
        public bool SetVisibility(ulong requesterId, LobbyVisibility value)
        {
            if (!IsHost(requesterId)) return false;

            visibility = value;
            LobbyChanged?.Invoke();
            return true;
        }

        /// <summary>전원 준비 완료 시 게임 시작 — 방장 전용.</summary>
        public bool TryStartGame(ulong requesterId)
        {
            if (!IsHost(requesterId)) return false;
            if (!AreAllReady())
            {
                Debug.Log("[LobbyManager] 아직 준비하지 않은 플레이어가 있습니다.");
                return false;
            }

            GameFlowManager.Instance?.StartGame();
            return true;
        }

        public bool AreAllReady()
        {
            if (_players.Count == 0) return false;

            for (int i = 0; i < _players.Count; i++)
                if (!_players[i].isReady) return false;

            return true;
        }

        public bool IsHost(ulong playerId) => playerId == HostPlayerId;

        public LobbyPlayerState FindPlayer(ulong playerId)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].playerId == playerId) return _players[i];
            return null;
        }

        /// <summary>6자리 방 코드 생성. // TODO(Steamworks): 실제 로비 코드 발급으로 교체.</summary>
        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 혼동 문자(I,O,0,1) 제외
            var code = new char[6];
            for (int i = 0; i < code.Length; i++)
                code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            return new string(code);
        }
    }
}
