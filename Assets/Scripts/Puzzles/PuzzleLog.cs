// 근거: 퍼즐 시스템.md — 트롤 원칙 ④ "같은 실수를 반복하지 않도록 피드백을 제공한다".
//       프로토타입에서는 '무슨 일이 왜 일어났는지'가 콘솔과 화면에 남는 것이 최소 요건이다.
// 근거: ARCHITECTURE.md §3-5 — UI로의 통지는 GameEvents가 담당한다. 이 로그는 개발용 보조 채널이며 게임 로직에 영향을 주지 않는다.
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 이벤트 개발 로그. Debug.Log와 화면 HUD(PuzzleDebugHud)가 공유하는 링 버퍼다.
    /// 할당을 줄이기 위해 고정 크기 배열을 재사용한다.
    /// </summary>
    public static class PuzzleLog
    {
        public const int Capacity = 14;

        private static readonly string[] Lines = new string[Capacity];
        private static int _count;
        private static int _head; // 다음에 기록할 위치

        /// <summary>기록될 때마다 증가 — HUD가 문자열 재생성 여부를 판단한다.</summary>
        public static int Version { get; private set; }

        public static int Count => _count;

        /// <summary>콘솔로도 출력할지. 대량 로그가 부담되면 끈다.</summary>
        public static bool EchoToConsole = true;

        public static void Record(Object context, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            Lines[_head] = $"[{Time.time:0.0}s] {message}";
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
            Version++;

            if (EchoToConsole) Debug.Log("[Puzzle] " + message, context);
        }

        /// <summary>0 = 가장 최근 기록.</summary>
        public static string GetLine(int index)
        {
            if (index < 0 || index >= _count) return string.Empty;
            int i = (_head - 1 - index + Capacity * 2) % Capacity;
            return Lines[i] ?? string.Empty;
        }

        public static void Clear()
        {
            for (int i = 0; i < Capacity; i++) Lines[i] = null;
            _count = 0;
            _head = 0;
            Version++;
        }
    }
}
