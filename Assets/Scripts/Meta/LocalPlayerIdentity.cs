// 근거: 이름 시스템.md — 이름은 Steam 닉네임 고정, 칭호를 장착하면 "[칭호] 닉네임"으로 표시된다.
// PlayerIdentity는 순수 C# 클래스라 씬에 존재할 수 없어 아무도 만들지 않았다(= 화면에 이름이 안 나온다).
// 이 컴포넌트가 로컬 플레이어의 PlayerIdentity를 소유하고, UI/월드 어디서든 접근할 단일 진입점이 된다.
using System;
using UnityEngine;
using TSWP.Core;
using TSWP.Art;

namespace TSWP.Meta
{
    /// <summary>
    /// 로컬 플레이어의 정체성 보유자. UI는 <see cref="DisplayName"/>만 읽으면 된다.
    /// SYNC: 원격 플레이어 이름은 추후 NGO로 수신 — 지금은 로컬 1인분만 다룬다.
    /// </summary>
    public class LocalPlayerIdentity : MonoBehaviour
    {
        public static LocalPlayerIdentity Instance { get; private set; }

        [Header("닉네임")]
        [Tooltip("TODO(Steamworks): 실제 Steam 닉네임으로 교체. 지금은 테스트용 고정값.")]
        [SerializeField] private string nickname = "ssong";

        [Header("칭호")]
        [Tooltip("칭호 색상 매핑 SO. 비워두면 흰색으로 표시한다.")]
        [SerializeField] private TitleColorConfig titleColors;

        [Tooltip("업적 보상으로 칭호를 처음 얻었을 때 자동으로 장착한다 (프로토타입 확인 편의).")]
        [SerializeField] private bool autoEquipFirstTitle = true;

        private PlayerIdentity _identity;

        /// <summary>표시명이 바뀌었을 때 발행 — 뷰가 캐시를 갱신한다.</summary>
        public event Action DisplayNameChanged;

        /// <summary>로컬 플레이어 정체성. null이 되지 않는다.</summary>
        public PlayerIdentity Identity => _identity;

        /// <summary>"[칭호] 닉네임" 형식 표시명. 칭호가 없으면 닉네임만.</summary>
        public string DisplayName
        {
            get
            {
                string titleText = AchievementManager.Instance != null
                    ? AchievementManager.Instance.GetTitleText(_identity.EquippedTitleId)
                    : null;

                return _identity.GetDisplayName(titleText);
            }
        }

        /// <summary>장착 칭호의 표시 색. 칭호가 없거나 SO 미지정이면 흰색.</summary>
        public Color TitleColor
        {
            get
            {
                if (titleColors == null || string.IsNullOrEmpty(_identity.EquippedTitleId))
                    return Color.white;

                var title = AchievementManager.Instance != null
                    ? AchievementManager.Instance.FindTitle(_identity.EquippedTitleId)
                    : null;

                return title != null ? titleColors.Get(title.colorType) : Color.white;
            }
        }

        /// <summary>
        /// 씬에 컴포넌트가 없어도 UI가 깨지지 않도록 하는 정적 조회 지점.
        /// OverheadInfo/PartyMemberInfo/결과 화면이 이 함수를 쓰면 된다.
        /// </summary>
        public static string ResolveDisplayName(string fallback = "Player")
            => Instance != null ? Instance.DisplayName : fallback;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _identity = new PlayerIdentity(nickname);
        }

        private void OnEnable()
        {
            GameEvents.TitleEarned += OnTitleEarned;
        }

        private void OnDisable()
        {
            GameEvents.TitleEarned -= OnTitleEarned;
        }

        private void Start()
        {
            // 저장된 보유/장착 칭호 복원은 매니저가 담당한다(없으면 조용히 생략).
            AchievementManager.Instance?.BindIdentity(_identity);
            DisplayNameChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnTitleEarned(string titleId)
        {
            _identity.GrantTitle(titleId);

            if (autoEquipFirstTitle && string.IsNullOrEmpty(_identity.EquippedTitleId))
                EquipTitle(titleId);
            else
                DisplayNameChanged?.Invoke();
        }

        /// <summary>칭호 장착. 매니저가 있으면 저장까지 위임한다.</summary>
        public bool EquipTitle(string titleId)
        {
            bool ok = AchievementManager.Instance != null
                ? AchievementManager.Instance.EquipTitle(titleId)
                : _identity.EquipTitle(titleId);

            if (ok) DisplayNameChanged?.Invoke();
            return ok;
        }

        /// <summary>보유 칭호를 순환 장착한다(마지막 다음은 '칭호 없음'). 프로토타입 확인용.</summary>
        public void EquipNextOwnedTitle()
        {
            var owned = _identity.OwnedTitleIds;
            if (owned.Count == 0) return;

            int current = owned.IndexOf(_identity.EquippedTitleId);
            int next = current + 1;

            // 마지막 칭호 다음에는 해제 상태를 한 번 거친다.
            if (next >= owned.Count)
            {
                _identity.EquipTitle(null);
                DisplayNameChanged?.Invoke();
                return;
            }

            EquipTitle(owned[next]);
        }
    }
}
