// 근거: 팔레트 시스템.md — 색은 정보다. 빨강=위험/폭발, 파랑=회복/얼음, 초록=독, 보라=저주/마법.
// 이펙트 팩(Effect and FX Pixel All Free)의 시트는 64x64 셀 기준 9행(색상) × N열(프레임) 구조다.
// 행 색상은 실제 픽셀 분석으로 확인했다 — 아래 주석의 RGB는 측정값이다.
namespace TSWP.Art
{
    /// <summary>
    /// 이펙트 시트의 색상 행. 값은 시트의 행 인덱스(0-based)와 일치한다.
    /// 팔레트 시스템.md의 색상 의미에 맞춰 이름을 붙였다.
    /// </summary>
    public enum VfxRow
    {
        /// <summary>#E47565 — 화염·폭발·피해·적 (팔레트: 빨강/주황)</summary>
        Fire = 0,

        /// <summary>#8A2ED9 — 저주·혼란·마법 (팔레트: 보라)</summary>
        Arcane = 1,

        /// <summary>#3B9FDE — 빙결·물·회복·보호 (팔레트: 파랑)</summary>
        Ice = 2,

        /// <summary>#459D4C — 독·자연 (팔레트: 초록)</summary>
        Poison = 3,

        /// <summary>#996145 — 흙·모래·나무 (환경)</summary>
        Earth = 4,

        /// <summary>#808080 — 연기·먼지·일반 타격 (무채색)</summary>
        Neutral = 5,

        /// <summary>#7B526C — 탁한 자주 (어둠·부패)</summary>
        Dusk = 6,

        /// <summary>#842345 — 진홍 (출혈·피)</summary>
        Blood = 7,

        /// <summary>#52287C — 진보라 (공포·어둠)</summary>
        Void = 8,
    }

    /// <summary>이펙트 시트 규격 상수.</summary>
    public static class VfxSheet
    {
        /// <summary>셀 한 변의 픽셀 크기.</summary>
        public const int CellSize = 64;

        /// <summary>색상 행 개수.</summary>
        public const int RowCount = 9;
    }
}
