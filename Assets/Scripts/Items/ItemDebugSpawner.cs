// 프로토타입 검증용 — 씬에 적/보스가 없어도 아이템이 눈에 보이게 하기 위한 임시 스포너.
// 근거: 아이템 시스템.md — 시작 아이템은 자유 경쟁으로 바닥에 뿌려진다(GameFlowState.StartItemDrop).
//       이 컴포넌트는 그 흐름이 배선되기 전까지 같은 그림을 손으로 만들어 보기 위한 도구다.
// 입력은 레거시 UnityEngine.Input을 쓴다 (ARCHITECTURE.md §1 — Input System 교체 전 임시).
// TODO: 실제 흐름(RunManager → ItemDropManager.SpawnStartItems)이 배선되면 이 파일은 삭제한다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Items
{
    /// <summary>키 입력으로 아이템을 바닥에 뿌리는 개발용 스포너.</summary>
    public class ItemDebugSpawner : MonoBehaviour
    {
        [Tooltip("뿌릴 아이템 목록. 비워 두면 ItemDropManager의 LootTable 추첨을 쓴다.")]
        [SerializeField] private List<ItemDefinition> items = new();

        [Tooltip("뿌릴 기준 위치. 비우면 이 오브젝트 위치.")]
        [SerializeField] private Transform spawnOrigin;

        [Tooltip("목록 전체를 한 번에 뿌리는 키.")]
        [SerializeField] private KeyCode spawnAllKey = KeyCode.F1;

        [Tooltip("루트 테이블에서 1개만 추첨해 뿌리는 키.")]
        [SerializeField] private KeyCode spawnRandomKey = KeyCode.F2;

        [Tooltip("아이템 사이의 가로 간격(월드 유닛).")]
        [SerializeField] private float spacing = 1.2f;

        [Tooltip("바닥에서 띄울 높이.")]
        [SerializeField] private float heightOffset = 0.5f;

        [Tooltip("시작 시 자동으로 목록 전체를 뿌린다.")]
        [SerializeField] private bool spawnOnStart = true;

        private void Start()
        {
            if (spawnOnStart) SpawnAll();
        }

        private void Update()
        {
            if (Input.GetKeyDown(spawnAllKey)) SpawnAll();
            if (Input.GetKeyDown(spawnRandomKey)) SpawnRandom();
        }

        /// <summary>목록의 아이템을 좌우로 늘어놓는다.</summary>
        public void SpawnAll()
        {
            if (items == null || items.Count == 0) return;

            var manager = ItemDropManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[ItemDebugSpawner] ItemDropManager가 씬에 없습니다 — 스폰 생략", this);
                return;
            }

            Vector2 origin = Origin();
            // 목록의 가운데가 기준 위치에 오도록 좌우로 펼친다.
            float start = -(items.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                manager.SpawnDrop(items[i], origin + new Vector2(start + i * spacing, 0f));
            }
        }

        /// <summary>루트 테이블 추첨으로 1개 뿌린다 (획득 경로는 시작 아이템으로 간주).</summary>
        public void SpawnRandom()
        {
            var manager = ItemDropManager.Instance;
            if (manager == null) return;
            manager.SpawnDrop(AcquisitionMethod.StartingItem, Origin());
        }

        private Vector2 Origin()
        {
            Vector2 basePos = spawnOrigin != null ? (Vector2)spawnOrigin.position : (Vector2)transform.position;
            return basePos + new Vector2(0f, heightOffset);
        }
    }
}
