// 근거: 팔레트 시스템.md — 체력바 색상 구간: 100%=초록, 70%=연두, 40%=노랑, 20%=빨강, 5%=깜빡임.
//       색만 보고 즉시 위험을 판단할 수 있어야 한다 (시인성 최우선).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>체력 비율 → 체력바 색상.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Health Bar Colors", fileName = "HealthBarColorConfig")]
    public class HealthBarColorConfig : ScriptableObject
    {
        [Serializable]
        public class Threshold
        {
            [Tooltip("이 비율 이상일 때 해당 색을 사용한다.")]
            [Range(0f, 1f)] public float minRatio;
            public Color color = Color.green;
        }

        [SerializeField]
        [Tooltip("높은 비율부터 내림차순으로 정렬해 둔다.")]
        private List<Threshold> thresholds = new List<Threshold>
        {
            new Threshold { minRatio = 1.00f },  // 초록
            new Threshold { minRatio = 0.70f },  // 연두
            new Threshold { minRatio = 0.40f },  // 노랑
            new Threshold { minRatio = 0.20f },  // 빨강
        };

        [Header("위험 깜빡임")]
        [Tooltip("이 비율 이하에서 체력바가 깜빡인다.")]
        [Range(0f, 1f)] public float blinkRatio = 0.05f;

        [Tooltip("깜빡임 주기(초). 접근성 설정의 '플래시 효과 감소'가 켜져 있으면 깜빡이지 않아야 한다.")]
        [Min(0.05f)] public float blinkPeriod = 0.4f; // TODO(밸런스): 문서 미정

        /// <summary>체력 비율에 해당하는 색을 반환한다.</summary>
        public Color Evaluate(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);

            Color result = Color.red;
            float bestMin = -1f;

            for (int i = 0; i < thresholds.Count; i++)
            {
                var t = thresholds[i];
                if (ratio >= t.minRatio && t.minRatio > bestMin)
                {
                    bestMin = t.minRatio;
                    result = t.color;
                }
            }

            // 어떤 구간에도 걸리지 않으면(가장 낮은 구간 미만) 마지막 색을 유지한다.
            if (bestMin < 0f && thresholds.Count > 0)
                result = thresholds[thresholds.Count - 1].color;

            return result;
        }

        /// <summary>지금 깜빡여야 하는지 (위험 구간 + 주기).</summary>
        public bool ShouldBlink(float ratio, float time, bool reduceFlashEffects)
        {
            if (reduceFlashEffects) return false;      // 접근성: 번쩍임 효과 감소
            if (ratio > blinkRatio) return false;
            return Mathf.Repeat(time, blinkPeriod) < blinkPeriod * 0.5f;
        }
    }
}
