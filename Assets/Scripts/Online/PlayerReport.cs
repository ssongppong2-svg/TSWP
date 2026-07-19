// 근거: 온라인 시스템.md — 욕설/핵 사용/비매너 신고 기능.
// 근거: 게임 성경.md — 트롤은 웃음을 만드는 행위여야 하며, 고의적인 비매너는 허용하지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>신고 1건.</summary>
    [Serializable]
    public class PlayerReport
    {
        public ulong reporterId;
        public ulong targetId;
        public ReportReason reason = ReportReason.BadManner;

        [TextArea] public string detail;

        /// <summary>해당 매치 식별자 — 서버 검토용.</summary>
        public string matchId;

        /// <summary>신고 시각(UTC ISO 8601).</summary>
        public string timestampUtc;
    }
}
