// 근거: 맵 시스템.md — "각 생물 군계는 고유한 적, 퍼즐, 함정, 배경을 가진다"의 데이터화.
// 타 시스템 타입 결합을 피하기 위해 적/퍼즐/이벤트는 느슨한 string id 목록으로 보관하고,
// 함정은 Combat.HazardType을 재사용한다 (ARCHITECTURE.md §5 — HazardType 재정의 금지).
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>
    /// 생물 군계 1종의 콘텐츠 풀 정의. SO 에셋 추가만으로 군계 콘텐츠가 늘어나는 구조.
    /// 각 시스템(Enemies/Puzzles/이벤트)이 RoomNode.ContentSeed 기반 rng로 이 풀에서 굴린다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Biome Definition", fileName = "Biome_")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("군계")]
        public BiomeType biomeType;
        public string displayName = "";

        [Header("고유 적 (느슨한 id — Enemies 시스템의 EnemyData/EncounterComposition id)")]
        public List<string> uniqueEnemyIds = new List<string>();

        [Header("고유 퍼즐 (느슨한 id — Puzzles 시스템의 PuzzleDefinition id)")]
        public List<string> uniquePuzzleIds = new List<string>();

        [Header("고유 이벤트 (느슨한 id — 이벤트 시스템 id)")]
        public List<string> uniqueEventIds = new List<string>();

        [Header("고유 함정/위험 요소 — Combat.HazardType 재사용 (진영 무관 피해)")]
        public List<Combat.HazardType> uniqueHazards = new List<Combat.HazardType>();

        [Header("배경/타일 (2D URP Tilemap — 도트 시스템.md: 타일 16/32px)")]
        [Tooltip("방 배경 스프라이트 풀. TODO: Addressables 전환 검토 (스펙 unityNotes ③).")]
        public List<Sprite> backgroundAssets = new List<Sprite>();
        [Tooltip("방 타일맵 프리팹 풀 — RoomManager가 방 활성화 시 인스턴스화.")]
        public List<GameObject> roomPrefabs = new List<GameObject>();

        [Header("튜닝 — '30초마다 새로운 상황' 원칙 (스펙 unityNotes ⑩)")]
        [SerializeField] private float eventDensity = 1f;  // TODO(밸런스): 문서 미정 — 방 내 이벤트/함정 밀도 배율
        [SerializeField] private float roomSizeScale = 1f; // TODO(밸런스): 문서 미정 — 방 크기 배율

        public float EventDensity => eventDensity;
        public float RoomSizeScale => roomSizeScale;
    }
}
