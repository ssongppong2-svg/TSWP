// 근거: 직업 시스템.md("성장은 레벨이 아닌 아이템") / 아이템 시스템.md(능력치 증감) / 전투 시스템.md(기본 치명타 0%)
// 스탯은 base 값 + modifier 스택(가산/승산)으로 계산한다. 아이템 장착/해제 = modifier 추가/제거.
using System;
using System.Collections.Generic;

namespace TSWP.Core
{
    public enum StatType
    {
        MaxHealth,
        AttackPower,
        Defense,
        MoveSpeed,
        AttackSpeed,
        CritChance,        // base 0 고정 (GameRules.BaseCritChance) — 아이템/버프로만 상승
        Range,
        CooldownReduction,
    }

    public enum StatModifierMode
    {
        Additive,          // base에 가산
        Multiplicative,    // (1 + value) 곱연산
    }

    public sealed class StatModifier
    {
        public StatType Stat;
        public StatModifierMode Mode;
        public float Value;
        /// <summary>부여 주체(아이템 인스턴스, 상태이상 등). 해제 시 이 키로 일괄 제거한다.</summary>
        public object Source;

        public StatModifier(StatType stat, StatModifierMode mode, float value, object source)
        {
            Stat = stat; Mode = mode; Value = value; Source = source;
        }
    }

    /// <summary>최종값 = (base + Σ가산) × Π(1 + 승산). 값 변경 시 Changed 이벤트 발행.</summary>
    public sealed class StatCollection
    {
        private readonly Dictionary<StatType, float> _baseValues = new();
        private readonly List<StatModifier> _modifiers = new();

        public event Action<StatType> Changed;

        public void SetBase(StatType stat, float value)
        {
            _baseValues[stat] = value;
            Changed?.Invoke(stat);
        }

        public float GetBase(StatType stat) => _baseValues.TryGetValue(stat, out var v) ? v : 0f;

        public float GetValue(StatType stat)
        {
            float additive = 0f;
            float multiplier = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m.Stat != stat) continue;
                if (m.Mode == StatModifierMode.Additive) additive += m.Value;
                else multiplier *= 1f + m.Value;
            }
            return (GetBase(stat) + additive) * multiplier;
        }

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            Changed?.Invoke(modifier.Stat);
        }

        /// <summary>같은 Source가 부여한 modifier를 전부 제거 (아이템 해제/상태이상 종료).</summary>
        public void RemoveModifiersFromSource(object source)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_modifiers[i].Source, source)) continue;
                var stat = _modifiers[i].Stat;
                _modifiers.RemoveAt(i);
                Changed?.Invoke(stat);
            }
        }
    }
}
