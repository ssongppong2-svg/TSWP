// 근거: 방 시스템.md — 매 플레이마다 방 위치/종류/이벤트/퍼즐/적 구성이 변경된다.
// 순수 C# 그래프 노드 — 씬 오브젝트(프리팹/타일맵)와 분리된 논리 데이터.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — IsExplored/IsDiscovered/IsCleared는 파티 공유 상태.
using System.Collections.Generic;

namespace TSWP.Map
{
    /// <summary>
    /// 맵 그래프의 방 1개. 콘텐츠(적 구성/이벤트/퍼즐/함정/아이템)는 타 시스템 결합을 피하기 위해
    /// 느슨한 string id + 결정론 파생 시드(ContentSeed)로만 보관한다 — 실제 굴림·해석은
    /// Enemies/Puzzles/Items 시스템이 ContentSeed 기반 System.Random으로 수행한다 (멀티 동기화).
    /// </summary>
    public sealed class RoomNode
    {
        // ── 식별/배치 ─────────────────────────────────────────────
        public int RoomId;
        public RoomType RoomType;
        /// <summary>그래프상 레이어(진행 깊이). 0=시작, 마지막=보스. 미니맵 배치의 세로축.</summary>
        public int Layer;
        /// <summary>레이어 내 위치(분기 슬롯). 미니맵 배치의 가로축 — 방 배치 랜덤.</summary>
        public int IndexInLayer;

        // ── 연결 (통로) ───────────────────────────────────────────
        /// <summary>통로로 연결된 모든 이웃 (방향 무관 — 플레이어 이동 판정용).</summary>
        public readonly List<RoomNode> ConnectedRooms = new List<RoomNode>();
        /// <summary>DAG 진행 방향 이웃 (시작→보스). 생성/불변식 검증용.</summary>
        public readonly List<RoomNode> NextRooms = new List<RoomNode>();
        /// <summary>DAG 역방향 이웃. 보스 역도달 검증용.</summary>
        public readonly List<RoomNode> PrevRooms = new List<RoomNode>();

        // ── 탐험/진행 상태 ────────────────────────────────────────
        /// <summary>탐험 여부 — 미니맵은 탐험한 지역만 표시(전장의 안개). SYNC: 호스트 권위.</summary>
        public bool IsExplored;
        /// <summary>발견 여부 — 비밀방은 발견 전까지 미니맵 비표시. 일반 방은 생성 시 true. SYNC: 호스트 권위.</summary>
        public bool IsDiscovered;
        /// <summary>클리어 여부 (전멸형/목표형 조건 달성). SYNC: 호스트 권위.</summary>
        public bool IsCleared;

        // ── 방 콘텐츠 (느슨한 참조 — 각 시스템이 해석) ─────────────
        /// <summary>이 방 콘텐츠 전용 파생 시드. 적 구성/이벤트/퍼즐/함정/아이템 배치를
        /// 각 시스템이 결정론적으로 굴리는 데 사용한다. SYNC: 시드만 동기화하면 결과 동일.</summary>
        public int ContentSeed;
        /// <summary>이벤트 방일 때 이벤트 id (예: "event.curse"). 미정이면 빈 문자열 — 이벤트 시스템이 ContentSeed로 굴림.</summary>
        public string EventId = "";
        /// <summary>퍼즐 방일 때 퍼즐 id. 미정이면 빈 문자열 — Puzzles 시스템이 ContentSeed로 굴림.</summary>
        public string PuzzleId = "";
        /// <summary>전투 방일 때 적 조합 id. 미정이면 빈 문자열 — Enemies 시스템이 ContentSeed로 굴림.</summary>
        public string EncounterId = "";

        /// <summary>비밀방 여부 편의 질의.</summary>
        public bool IsSecret => RoomType == RoomType.Secret;
    }
}
