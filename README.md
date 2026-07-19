# TSWP — The Strongest Warrior Party

> "스트리머와 친구들이 서로 웃고, 협동하고, 배신(?)하며 보스를 공략하는 2D 로그라이크 협동 액션 게임"

2D 픽셀아트 · 로그라이크 · 최대 8인 협동 · Steam 출시 목표

## 저장소 구성

- 루트의 `*.md` — 게임 설계 문서 (최우선 문서: **게임 성경.md**)
- `Assets/Scripts/` — Unity C# 뼈대 코드
  - 코드 구조·규칙은 [Assets/Scripts/ARCHITECTURE.md](Assets/Scripts/ARCHITECTURE.md) 참조

## Unity 프로젝트 열기 (최초 1회)

이 저장소 루트가 곧 Unity 프로젝트 루트가 되도록 설계했다.
Unity가 생성하는 폴더(`Library/`, `Temp/` 등)는 `.gitignore`에 이미 등록되어 있다.

1. **Unity Hub**에서 **Unity 6 LTS** 설치 (Windows Build Support 포함)
2. Unity Hub → New Project → **2D (URP)** 템플릿 → 임시 폴더에 프로젝트 생성 (예: `TSWP-temp`)
3. 생성된 프로젝트에서 다음 폴더를 이 저장소 루트로 **복사**:
   - `ProjectSettings/`
   - `Packages/`
   - `Assets/Settings/` (URP 설정)
4. Unity Hub → Open → 이 저장소 루트(`TSWP/`) 선택
5. 열리면 `Assets/Scripts/`의 코드가 컴파일된다 (외부 패키지 의존 없음)
6. 임시 프로젝트 폴더는 삭제

### 프로젝트 설정 권장값 (도트 시스템.md 근거)

- 해상도 기준: 1920x1080 (16:9)
- 스프라이트 임포트: Filter Mode = **Point**, Compression = **None**, PPU = **16**
- 2D Pixel Perfect Camera 사용 (정수 줌)
- 애니메이션 기준 12FPS

## 뼈대 이후 도입 예정 (코드에 TODO 시임 존재)

| 영역 | 예정 기술 | 현재 상태 |
|---|---|---|
| 네트워킹 (8인 멀티) | Netcode for GameObjects + Steamworks | `TSWP.Online` 스텁 + `// SYNC` 주석 |
| 음성 채팅 (거리 기반 Open Mic) | Vivox 또는 Steam Voice | `VoiceChatConfig` SO + 감쇠/차폐 파라미터 |
| 입력 (키 리바인딩) | Input System | `IPlayerInput` 추상화 + 레거시 구현 |

## 개발 원칙

> "재미없으면 만들지 않는다." — 게임 성경.md

새 기능을 만들기 전에 게임 성경.md를 다시 읽는다.
매일 개발 종료 전 개발일지.md를 작성한다.
