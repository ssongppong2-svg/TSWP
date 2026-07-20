// 근거: 맵 시스템.md / 방 시스템.md — 매 플레이마다 방 종류/콘텐츠가 달라지고, 군계마다 고유 콘텐츠를 가진다.
//   절차 생성이 만든 RoomNode(논리)에 실제 방 데이터(RoomDefinition)를 붙이는 유일한 지점.
//   맵/보스/퍼즐이 늘어날 때 여기에 에셋만 추가하면 되도록 코드에 방 종류를 하드코딩하지 않는다.
// SYNC: 선택은 RoomNode.ContentSeed 기반 결정론 — 전 클라이언트가 같은 방을 고른다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>
    /// 방 정의(RoomDefinition) 카탈로그. 방 종류 + 군계 + 스테이지로 후보를 걸러
    /// ContentSeed 기반 가중 굴림으로 1개를 고른다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Room Catalog", fileName = "RoomCatalog")]
    public class RoomCatalog : ScriptableObject
    {
        [Header("등록된 방 정의 — 콘텐츠 추가는 이 목록에 에셋을 넣는 것으로 끝난다")]
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

        [Header("폴백")]
        [Tooltip("해당 종류의 방 정의가 하나도 없을 때 사용할 정의. 비우면 물리 방 없이 논리만 진행한다.")]
        [SerializeField] private RoomDefinition fallbackRoom;

        // 굴림마다 리스트를 새로 만들지 않도록 재사용 버퍼 (SO는 단일 인스턴스라 안전).
        private readonly List<RoomDefinition> _candidates = new List<RoomDefinition>();

        public IReadOnlyList<RoomDefinition> Rooms => rooms;

        /// <summary>
        /// 방 종류/군계/스테이지 조건을 만족하는 정의 중 하나를 결정론적으로 고른다.
        /// contentSeed가 같으면 항상 같은 결과 — 멀티 동기화의 전제.
        /// </summary>
        public RoomDefinition Pick(RoomType roomType, BiomeType biome, int stageIndex, int contentSeed)
        {
            _candidates.Clear();
            int totalWeight = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room == null) continue;
                if (room.RoomType != roomType) continue;
                if (!room.Matches(biome, stageIndex)) continue;
                if (room.SelectionWeight <= 0) continue;

                _candidates.Add(room);
                totalWeight += room.SelectionWeight;
            }

            if (_candidates.Count == 0 || totalWeight <= 0)
                return fallbackRoom; // null 가능 — 호출측(RoomFlowManager)이 조용히 논리 진행만 한다

            // System.Random(seed)로 굴려 시드 결정론을 유지한다 (UnityEngine.Random은 전역 상태라 금지).
            var rng = new System.Random(contentSeed);
            int roll = rng.Next(totalWeight);
            for (int i = 0; i < _candidates.Count; i++)
            {
                roll -= _candidates[i].SelectionWeight;
                if (roll < 0) return _candidates[i];
            }
            return _candidates[_candidates.Count - 1];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 절차 생성이 만들 수 있는 방 종류에 정의가 하나도 없으면 그 방은 빈 방이 된다 — 조기 경고.
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i] == null)
                    Debug.LogWarning($"[RoomCatalog] '{name}': rooms[{i}]가 비어 있습니다.", this);
        }
#endif
    }
}
