// 근거: 이름 시스템.md — 플레이어 이름은 Steam 닉네임을 그대로 사용하며 게임 내에서 변경할 수 없다.
//       칭호를 장착하면 이름 앞에 붙어 표시된다.
using System;
using System.Collections.Generic;

namespace TSWP.Meta
{
    /// <summary>
    /// 플레이어 정체성(닉네임 + 칭호). 닉네임은 Steam에서 받아오며 게임 내 변경 불가다.
    /// </summary>
    [Serializable]
    public class PlayerIdentity
    {
        /// <summary>Steam 닉네임. 게임 내 변경 불가 — 생성 시점에만 설정된다.</summary>
        public string SteamNickname { get; private set; }

        /// <summary>현재 장착 중인 칭호 id. 비어 있으면 칭호 없이 닉네임만 표시.</summary>
        public string EquippedTitleId { get; private set; }

        /// <summary>보유 칭호 목록.</summary>
        public List<string> OwnedTitleIds { get; } = new List<string>();

        /// <summary>업적 보상으로 얻는 프로필 테두리 id.</summary>
        public string ProfileBorderId { get; private set; }

        public PlayerIdentity(string steamNickname)
        {
            // TODO(Steamworks): 실제 Steam 닉네임 조회로 교체. 현재는 주입값 사용.
            SteamNickname = string.IsNullOrEmpty(steamNickname) ? "Player" : steamNickname;
        }

        /// <summary>칭호를 획득한다. 이미 보유 중이면 무시.</summary>
        public bool GrantTitle(string titleId)
        {
            if (string.IsNullOrEmpty(titleId)) return false;
            if (OwnedTitleIds.Contains(titleId)) return false;

            OwnedTitleIds.Add(titleId);
            return true;
        }

        /// <summary>보유 중인 칭호만 장착할 수 있다. null/빈 문자열이면 칭호 해제.</summary>
        public bool EquipTitle(string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
            {
                EquippedTitleId = null;
                return true;
            }

            if (!OwnedTitleIds.Contains(titleId)) return false;

            EquippedTitleId = titleId;
            return true;
        }

        public void SetProfileBorder(string borderId) => ProfileBorderId = borderId;

        /// <summary>
        /// 표시 이름. 칭호가 있으면 "[칭호] 닉네임" 형식.
        /// 칭호 문구 조회는 TitleData가 필요하므로 호출 측이 전달한다.
        /// </summary>
        public string GetDisplayName(string equippedTitleText = null)
        {
            if (string.IsNullOrEmpty(EquippedTitleId) || string.IsNullOrEmpty(equippedTitleText))
                return SteamNickname;

            return $"[{equippedTitleText}] {SteamNickname}";
        }
    }
}
