// 근거: 요구사항 — "처음 진입 시에만 재생, 재입장 시에는 생략".
// 한 런 안에서의 재입장과, 계정 단위로 이미 본 인트로를 구분한다.
//   런 내 재입장  → 무조건 생략 (같은 판에서 숲에 두 번 들어가면 두 번째는 안 나온다)
//   다음 런        → 기본은 다시 재생 (로그라이크는 매 판이 새 시작이다)
// NOTE(기획 확인 필요): "영구히 한 번만" 정책을 원하면 persistAcrossRuns를 켠다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>이미 본 맵 인트로 기록.</summary>
    public static class MapIntroHistory
    {
        private const string PrefsKeyPrefix = "tswp.intro.seen.";

        /// <summary>이번 런에서 본 맵들. 런이 끝나면 초기화된다.</summary>
        private static readonly HashSet<string> _seenThisRun = new HashSet<string>();

        /// <summary>
        /// true면 한 번 본 인트로를 계정 단위로 영구 기억한다(다음 런에도 생략).
        /// 로그라이크 특성상 기본은 false — 매 판 시작의 분위기를 살린다.
        /// </summary>
        public static bool PersistAcrossRuns { get; set; }

        public static bool HasSeen(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return false;

            if (_seenThisRun.Contains(mapId)) return true;

            if (PersistAcrossRuns && PlayerPrefs.GetInt(PrefsKeyPrefix + mapId, 0) == 1)
                return true;

            return false;
        }

        public static void MarkSeen(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return;

            _seenThisRun.Add(mapId);

            if (!PersistAcrossRuns) return;

            PlayerPrefs.SetInt(PrefsKeyPrefix + mapId, 1);
            PlayerPrefs.Save();
        }

        /// <summary>새 런 시작 시 호출 — 이번 판 기록을 지운다.</summary>
        public static void ResetRun() => _seenThisRun.Clear();

        /// <summary>영구 기록까지 전부 지운다 (디버그/테스트용).</summary>
        public static void ResetAll(IEnumerable<string> knownMapIds = null)
        {
            _seenThisRun.Clear();

            if (knownMapIds == null) return;

            foreach (string id in knownMapIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                PlayerPrefs.DeleteKey(PrefsKeyPrefix + id);
            }
            PlayerPrefs.Save();
        }
    }
}
