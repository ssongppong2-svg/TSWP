// 근거: 조작과 시스템.md — T키 이모트 휠, 기본 7종(😀 😂 😭 👍 👎 💀 🖕), 추후 해금 이모트 추가 예정.
// 업적 보상(Meta)과 UI 휠이 공용으로 쓰므로 Core에 둔다 (ARCHITECTURE.md §5).
using UnityEngine;

namespace TSWP.Core
{
    [CreateAssetMenu(menuName = "TSWP/Core/Emote", fileName = "Emote_")]
    public sealed class EmoteData : ScriptableObject
    {
        public string EmoteId;
        public string DisplayName;
        public Sprite Sprite;
        /// <summary>true면 업적 등으로 해금해야 사용 가능 (기본 7종은 false).</summary>
        public bool IsUnlockable;
    }
}
