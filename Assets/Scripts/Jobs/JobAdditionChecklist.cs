#if UNITY_EDITOR
// 근거: 직업 시스템.md — 직업 추가 규칙: 신규 직업은 아래 6항목을 반드시 만족해야 한다.
// ① 기존 직업과 플레이 방식이 다르다 ② 협동 요소가 존재한다 ③ 위험 요소가 존재한다
// ④ 최소 한 보스에서 활약한다 ⑤ 최소 하나의 재미있는 상황을 만든다 ⑥ 아이템과 시너지가 존재한다
// 에디터 전용 검증 유틸 — 데이터 무결성(팀 기여 ≥1, 활약 보스 ≥1, 위험 요소 ≥1)도 함께 확인한다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>신규 직업 6항목 체크리스트 평가 + 프로젝트 전체 JobDefinition 에셋 일괄 검증.</summary>
    public static class JobAdditionChecklist
    {
        /// <summary>6항목 평가 결과. 실패 항목은 Failures에 한국어 사유로 쌓인다.</summary>
        public sealed class ChecklistResult
        {
            public bool IsPlaystyleUnique;           // ① — 데이터상 근사: 플레이 스타일 서술 존재 (고유성 최종 판단은 수동 확인)
            public bool HasCoopElement;              // ② — teamPlayAbilities ≥ 1
            public bool HasRiskElement;              // ③ — risks ≥ 1
            public bool ShinesAgainstAtLeastOneBoss; // ④ — favoredBossIds ≥ 1
            public bool CreatesFunnySituation;       // ⑤ — trollElements ≥ 1
            public bool HasItemSynergy;              // ⑥ — itemSynergyNotes ≥ 1

            public readonly List<string> Failures = new List<string>();
            public bool AllPassed => Failures.Count == 0;
        }

        /// <summary>6항목 체크리스트 평가. 자동 판정 가능한 데이터 필드 기준으로 검사한다.</summary>
        public static ChecklistResult Evaluate(JobDefinition job)
        {
            var result = new ChecklistResult();
            if (job == null)
            {
                result.Failures.Add("JobDefinition이 null이다.");
                return result;
            }

            // ① 기존 직업과 플레이 방식이 다르다 — 서술 존재 여부만 자동 검사 (고유성 비교는 수동 확인 필요).
            result.IsPlaystyleUnique = !string.IsNullOrWhiteSpace(job.PlayStyleDescription);
            if (!result.IsPlaystyleUnique)
            {
                result.Failures.Add("① 고유 플레이 스타일 서술(playStyleDescription)이 비어 있다 — 기존 직업과의 차별점을 기술할 것.");
            }

            // ② 협동 요소 — 팀 기여 능력 최소 1개 (문서: 팀 플레이).
            result.HasCoopElement = HasAny(job.TeamPlayAbilities);
            if (!result.HasCoopElement)
            {
                result.Failures.Add("② 팀 기여 능력(teamPlayAbilities)이 없다 — 회복/이동 보조/적 제어/구조물 설치/버프/디버프 중 최소 1개.");
            }

            // ③ 위험 요소 — 모든 직업은 명확한 위험 요소를 가진다.
            result.HasRiskElement = HasAny(job.Risks);
            if (!result.HasRiskElement)
            {
                result.Failures.Add("③ 위험 요소(risks)가 없다 — 최소 1개 필수 (예: 아군도 피해를 입을 수 있다).");
            }

            // ④ 보스 상성 — 최소 한 보스에서 가장 활약해야 한다.
            result.ShinesAgainstAtLeastOneBoss = HasAny(job.FavoredBossIds);
            if (!result.ShinesAgainstAtLeastOneBoss)
            {
                result.Failures.Add("④ 활약 보스(favoredBossIds)가 없다 — 최소 1개 필수 (모든 보스는 특정 직업이 활약할 수 있다).");
            }

            // ⑤ 재미있는 상황 — 트롤/웃긴 상황 요소.
            result.CreatesFunnySituation = HasAny(job.TrollElements);
            if (!result.CreatesFunnySituation)
            {
                result.Failures.Add("⑤ 트롤 요소(trollElements)가 없다 — 웃긴 상황을 만들 수 있는 요소 최소 1개.");
            }

            // ⑥ 아이템 시너지 — 성장은 레벨이 아닌 아이템이므로 시너지 서술 필수.
            result.HasItemSynergy = HasAny(job.ItemSynergyNotes);
            if (!result.HasItemSynergy)
            {
                result.Failures.Add("⑥ 아이템 시너지(itemSynergyNotes)가 없다 — 최소 1개 서술 필수.");
            }

            return result;
        }

        /// <summary>체크리스트 외 기본 구성 검증 — 직업 구성 요소(기본 공격/스킬/패시브)와 계약 필드 무결성.</summary>
        public static List<string> ValidateComposition(JobDefinition job)
        {
            var failures = new List<string>();
            if (job == null)
            {
                failures.Add("JobDefinition이 null이다.");
                return failures;
            }

            if (string.IsNullOrWhiteSpace(job.JobId))
            {
                failures.Add("jobId가 비어 있다 (알려진 예: warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho).");
            }

            if (string.IsNullOrWhiteSpace(job.DisplayName))
            {
                failures.Add("displayName이 비어 있다.");
            }

            if (job.Difficulty < 1 || job.Difficulty > 5)
            {
                failures.Add($"난이도({job.Difficulty})가 1~5 범위를 벗어났다 — 5단계 별점.");
            }

            if (job.BasicAttack == null)
            {
                failures.Add("기본 공격 프로파일(basicAttack)이 없다 — 모든 직업은 기본 공격을 가진다.");
            }

            if (job.ActiveSkill == null)
            {
                failures.Add("액티브 스킬(activeSkill)이 없다 — 모든 직업은 하나의 고유 스킬(Q)을 가진다.");
            }
            else if (job.ActiveSkill.Cooldown <= 0f)
            {
                failures.Add($"스킬 {job.ActiveSkill.name}의 쿨타임이 0 이하다 — 반드시 쿨타임이 존재해야 한다.");
            }

            if (job.Passive == null)
            {
                failures.Add("패시브(passive)가 없다 — 모든 직업은 하나의 고유 패시브를 가진다.");
            }

            return failures;
        }

        /// <summary>프로젝트의 모든 JobDefinition 에셋을 찾아 6항목 체크리스트 + 기본 구성 + jobId 중복을 일괄 검증한다.</summary>
        [MenuItem("TSWP/Jobs/직업 체크리스트 전체 검증")]
        private static void ValidateAllJobAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:JobDefinition");
            if (guids.Length == 0)
            {
                Debug.Log("[JobAdditionChecklist] JobDefinition 에셋이 없다 — 검증 생략.");
                return;
            }

            int passCount = 0;
            var seenJobIds = new Dictionary<string, string>(); // jobId → 에셋 경로 (중복 검출)

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
                if (job == null)
                {
                    continue;
                }

                var allFailures = new List<string>();
                allFailures.AddRange(ValidateComposition(job));
                allFailures.AddRange(Evaluate(job).Failures);

                // jobId 중복 검출 — 직업 식별은 문자열이므로 데이터 수준에서 유일성을 지킨다.
                if (!string.IsNullOrWhiteSpace(job.JobId))
                {
                    if (seenJobIds.TryGetValue(job.JobId, out string firstPath))
                    {
                        allFailures.Add($"jobId '{job.JobId}' 중복 — 먼저 정의된 에셋: {firstPath}");
                    }
                    else
                    {
                        seenJobIds.Add(job.JobId, path);
                    }
                }

                if (allFailures.Count == 0)
                {
                    passCount++;
                }
                else
                {
                    Debug.LogError(
                        $"[JobAdditionChecklist] '{job.name}' ({path}) 검증 실패 {allFailures.Count}건:\n - " +
                        string.Join("\n - ", allFailures),
                        job);
                }
            }

            Debug.Log($"[JobAdditionChecklist] 검증 완료: {passCount}/{guids.Length} 통과. " +
                      "①(플레이 방식 고유성)은 서술 존재만 자동 검사 — 기존 직업과의 실제 차별성은 수동 확인 필요.");
        }

        private static bool HasAny<T>(T[] array) => array != null && array.Length > 0;
    }
}
#endif
