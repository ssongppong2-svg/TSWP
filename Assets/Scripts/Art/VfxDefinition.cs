// 근거: 도트 시스템.md — 애니메이션은 12FPS 기준. 이펙트는 EffectType 11종으로 분류한다.
// 시트를 슬라이스하지 않고 Sprite.Create로 런타임에 프레임을 잘라 쓴다
// (에셋 180장을 에디터에서 일일이 자르는 작업을 없애고, 행/프레임을 데이터로 지정할 수 있다).
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 이펙트 1종 정의. 스프라이트 시트에서 특정 색상 행을 골라 순서대로 재생한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Art/VFX Definition", fileName = "Vfx_")]
    public class VfxDefinition : ScriptableObject
    {
        [Header("식별")]
        public string vfxId;

        [Header("시트")]
        [Tooltip("64x64 셀, 9행(색상) x N열(프레임) 구조의 스프라이트 시트.")]
        public Texture2D sheet;

        [Tooltip("사용할 색상 행. 같은 시트로 화염/빙결/독 버전을 만들 수 있다.")]
        public VfxRow row = VfxRow.Neutral;

        [Tooltip("재생할 첫 프레임(0-based). 0이면 시트 처음부터.")]
        [Min(0)] public int startFrame;

        [Tooltip("재생할 프레임 수. 0이면 시트 끝까지 자동 계산.")]
        [Min(0)] public int frameCount;

        [Header("재생")]
        [Tooltip("초당 프레임. 도트 시스템.md 기준 12FPS.")]
        [Min(1f)] public float fps = 12f;

        [Tooltip("반복 재생 여부. false면 끝나고 자동 소멸한다.")]
        public bool loop;

        [Header("표시")]
        [Tooltip("픽셀당 유닛. 64px 셀 기준 32면 이펙트가 2유닛 크기가 된다.")]
        [Min(1f)] public float pixelsPerUnit = 32f;

        [Tooltip("추가 크기 배율.")]
        [Min(0.01f)] public float scale = 1f;

        [Tooltip("정렬 순서 — 캐릭터(10)보다 크면 앞에 그려진다.")]
        public int sortingOrder = 20;

        [Tooltip("좌우 반전 허용 — 공격 방향에 따라 뒤집는다.")]
        public bool canFlip = true;

        // 런타임 캐시 — 같은 정의를 여러 번 재생해도 Sprite를 다시 만들지 않는다.
        private Sprite[] _cachedFrames;

        /// <summary>시트의 전체 열(프레임) 수.</summary>
        public int SheetColumns => sheet != null ? sheet.width / VfxSheet.CellSize : 0;

        /// <summary>실제 재생할 프레임 수.</summary>
        public int ResolvedFrameCount
        {
            get
            {
                int available = Mathf.Max(0, SheetColumns - startFrame);
                return frameCount > 0 ? Mathf.Min(frameCount, available) : available;
            }
        }

        /// <summary>총 재생 시간(초). loop면 1회 순환 시간.</summary>
        public float Duration => fps > 0f ? ResolvedFrameCount / fps : 0f;

        /// <summary>
        /// 프레임 스프라이트 배열을 얻는다. 최초 호출 시 Sprite.Create로 잘라 캐시한다.
        /// (Sprite.Create는 Read/Write 없이도 동작한다 — 렌더링용 부분 사각형만 정의)
        /// </summary>
        public Sprite[] GetFrames()
        {
            if (_cachedFrames != null && _cachedFrames.Length > 0) return _cachedFrames;
            if (sheet == null) return System.Array.Empty<Sprite>();

            int count = ResolvedFrameCount;
            if (count <= 0) return System.Array.Empty<Sprite>();

            int rowIndex = Mathf.Clamp((int)row, 0, VfxSheet.RowCount - 1);
            var frames = new List<Sprite>(count);

            for (int i = 0; i < count; i++)
            {
                int col = startFrame + i;

                // PNG 좌표는 좌상단 기준, Unity 텍스처 좌표는 좌하단 기준이라 행을 뒤집는다.
                float x = col * VfxSheet.CellSize;
                float y = sheet.height - (rowIndex + 1) * VfxSheet.CellSize;

                if (x + VfxSheet.CellSize > sheet.width || y < 0f) break;

                var rect = new Rect(x, y, VfxSheet.CellSize, VfxSheet.CellSize);
                var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                sprite.name = $"{name}_{i}";
                frames.Add(sprite);
            }

            _cachedFrames = frames.ToArray();
            return _cachedFrames;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(vfxId)) vfxId = name;

            if (sheet == null) return;

            if (sheet.height % VfxSheet.CellSize != 0 || sheet.width % VfxSheet.CellSize != 0)
            {
                Debug.LogWarning(
                    $"[VfxDefinition] '{name}': 시트 크기 {sheet.width}x{sheet.height}가 " +
                    $"{VfxSheet.CellSize}의 배수가 아닙니다.", this);
            }

            int rows = sheet.height / VfxSheet.CellSize;
            if (rows != VfxSheet.RowCount)
                Debug.LogWarning($"[VfxDefinition] '{name}': 행 수가 {rows}입니다 (예상 {VfxSheet.RowCount}).", this);
        }
#endif
    }
}
