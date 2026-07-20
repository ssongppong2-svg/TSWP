// 근거: 보스 시스템.md — 패턴은 예고(텔레그래프) 후 발동해야 한다 ('공정하지만 방심하면 치명적').
// 전략 패턴의 '상태' 절반. 데이터/튜닝 값은 BossPatternBehaviour(SO 애셋, 공유)가 갖고,
// 실행 중 변하는 값(경과 시간·이미 맞은 대상)은 이 Runner 인스턴스가 갖는다.
// SO에 가변 상태를 두면 같은 패턴을 쓰는 보스 2기가 서로를 덮어쓰므로 반드시 분리한다.
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 패턴 1회 실행분의 상태 기계. 예고(Telegraph) → 발동(Active) → 종료(Finished) 흐름을
    /// 이 기반 클래스가 공통 처리하므로, 구체 패턴은 OnActiveTick만 구현하면 된다.
    /// </summary>
    public abstract class BossPatternRunner
    {
        public enum Stage
        {
            Telegraph, // 예고 — 플레이어가 반응할 시간
            Active,    // 실제 판정
            Finished,  // 종료 (정상 완료 또는 중단)
        }

        /// <summary>이 Runner를 만든 SO. 튜닝 값 조회용 (읽기 전용으로만 사용할 것).</summary>
        protected readonly BossPatternBehaviour Source;

        private float _timer;

        public Stage CurrentStage { get; private set; } = Stage.Telegraph;
        public bool IsFinished => CurrentStage == Stage.Finished;

        /// <summary>현재 단계에서의 경과 시간(초).</summary>
        protected float StageElapsed => _timer;

        protected BossPatternRunner(BossPatternBehaviour source)
        {
            Source = source;
        }

        /// <summary>실행 시작. BossController가 패턴 선택 직후 1회 호출한다.</summary>
        public void Begin(BossPatternContext ctx)
        {
            CurrentStage = Stage.Telegraph;
            _timer = 0f;

            // 예고 연출 — 연출 서비스가 없으면 조용히 생략된다.
            if (Source != null)
                ctx.PlayVfx(Source.TelegraphVfxId, ctx.BossPosition);

            OnTelegraphStart(ctx);

            // 예고 시간이 0이면 같은 프레임에 곧바로 발동 단계로 넘어간다.
            if (TelegraphSeconds(ctx) <= 0f)
                EnterActive(ctx);
        }

        /// <summary>매 프레임 갱신. BossController가 호출한다.</summary>
        public void Tick(BossPatternContext ctx, float deltaTime)
        {
            if (CurrentStage == Stage.Finished) return;

            _timer += deltaTime;

            if (CurrentStage == Stage.Telegraph)
            {
                OnTelegraphTick(ctx, deltaTime);
                if (_timer >= TelegraphSeconds(ctx))
                    EnterActive(ctx);
                return;
            }

            if (OnActiveTick(ctx, deltaTime))
                Finish(ctx);
        }

        /// <summary>강제 중단 (보스 사망/페이즈 전환/과다 지속 방어). 정리 후 즉시 종료된다.</summary>
        public void Interrupt(BossPatternContext ctx)
        {
            if (CurrentStage == Stage.Finished) return;
            OnInterrupt(ctx);
            OnCleanup(ctx);
            CurrentStage = Stage.Finished;
        }

        /// <summary>정상 완료.</summary>
        protected void Finish(BossPatternContext ctx)
        {
            if (CurrentStage == Stage.Finished) return;
            OnFinish(ctx);
            OnCleanup(ctx);
            CurrentStage = Stage.Finished;
        }

        private void EnterActive(BossPatternContext ctx)
        {
            CurrentStage = Stage.Active;
            _timer = 0f;
            OnActiveStart(ctx);
        }

        /// <summary>난이도·광폭화 속도를 반영한 예고 시간.</summary>
        private float TelegraphSeconds(BossPatternContext ctx) =>
            Source != null ? ctx.Scale(Source.TelegraphSeconds) : 0f;

        // ── 파생 클래스 훅 ────────────────────────────────────────

        /// <summary>예고 시작 시 1회 (조준 고정·경고 표시 등).</summary>
        protected virtual void OnTelegraphStart(BossPatternContext ctx) { }

        /// <summary>예고 중 매 프레임 (조준선 추적 등).</summary>
        protected virtual void OnTelegraphTick(BossPatternContext ctx, float deltaTime) { }

        /// <summary>발동 시작 시 1회.</summary>
        protected virtual void OnActiveStart(BossPatternContext ctx) { }

        /// <summary>발동 중 매 프레임. true를 반환하면 패턴이 완료된 것으로 처리한다.</summary>
        protected abstract bool OnActiveTick(BossPatternContext ctx, float deltaTime);

        /// <summary>정상 완료 시 1회.</summary>
        protected virtual void OnFinish(BossPatternContext ctx) { }

        /// <summary>강제 중단 시 1회.</summary>
        protected virtual void OnInterrupt(BossPatternContext ctx) { }

        /// <summary>완료/중단 어느 쪽이든 마지막에 반드시 실행되는 정리 훅
        /// (보스 속도 0으로 되돌리기, 화면 오버레이 끄기 등 — 여기서 하지 않으면 잔상이 남는다).</summary>
        protected virtual void OnCleanup(BossPatternContext ctx) { }
    }
}
