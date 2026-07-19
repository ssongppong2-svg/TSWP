// 근거: 온라인 시스템.md — 욕설/핵 사용/비매너 신고 기능.
// 근거: 게임 성경.md — 트롤은 웃음을 만드는 행위여야 하며, 고의적인 비매너는 허용하지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>
    /// 신고 접수. 실제 서버 전송은 네트워크/백엔드 도입 시 연결한다.
    /// </summary>
    public class ReportService : MonoBehaviour
    {
        public static ReportService Instance { get; private set; }

        private readonly List<PlayerReport> _pending = new List<PlayerReport>();

        public IReadOnlyList<PlayerReport> Pending => _pending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>신고 접수. // TODO(백엔드): 서버 전송으로 교체.</summary>
        public void Submit(ulong reporterId, ulong targetId, ReportReason reason, string detail, string matchId)
        {
            if (reporterId == targetId)
            {
                Debug.LogWarning("[ReportService] 자기 자신은 신고할 수 없습니다.");
                return;
            }

            _pending.Add(new PlayerReport
            {
                reporterId = reporterId,
                targetId = targetId,
                reason = reason,
                detail = detail,
                matchId = matchId,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            });
        }
    }
}
