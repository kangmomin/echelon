# ECHELON (가제)

[![CI / Deploy](https://github.com/kangmomin/echelon/actions/workflows/ci-deploy.yml/badge.svg)](https://github.com/kangmomin/echelon/actions/workflows/ci-deploy.yml)

- **웹에서 바로 하기:** https://kangmomin.github.io/echelon/ (WebGL 2 브라우저. 큰 전투는 데스크톱 판이 더 빠르다)
- **실행 파일(Windows·Linux):** [Releases](https://github.com/kangmomin/echelon/releases)

Broken Arrow 를 기반으로 **제대(梯隊) 지휘·편제표·전술 대형·지휘통제**를 더한 현대전 RTT 프로토타입.
설계는 [`docs/PLAN.md`](docs/PLAN.md), 설계 변경 기록은 [`docs/DECISIONS.md`](docs/DECISIONS.md).

- 엔진: **Godot 4.7.2** (Forward+), 정적 타입 GDScript
- 테스트: GUT 9.7.1 (`addons/gut`)
- 현재 단계: **M7 (다듬기·검증) 완료 — 프로토타입 1차 완성**. 결과 정리는 [`docs/REPORT.md`](docs/REPORT.md), 플레이테스트 기록은 [`docs/PLAYTEST.md`](docs/PLAYTEST.md)

## 실행

Godot 4.7.2 로 이 폴더의 `project.godot` 를 연다. 처음 열 때 에디터가 import 를 한 번 수행한다.
F5 로 실행하면 메뉴가 뜬다.

- **튜토리얼**: 처음이라면 여기부터(약 10분, 14단계). 작은 전장에서 카메라 → 선택 → 부대 지정 → 대형 → 시간 조절 →
  이동 명령과 지휘 지연 → 사격 통제 → 공격 이동 → 화력 요청 → 적 소대 격파까지 과제를 하나씩 해 본다.
- **가이드**: 메뉴의 "가이드" 또는 전투 중 `/` 키(왼쪽 위 버튼). 조작, 제대와 지휘 지연, 대형, 탐지·교전, 화력·보급,
  편성, 승리 조건, 요령을 정리한 도움말. 전투 중에 열면 일시정지된다.
- **스커미시**: 편성 화면 → 배치 단계 → 전투. 편성 화면에서 대대 편제표의 중대 슬롯을 채우고, 소대 줄의
  "이동…"으로 교차 배속, 장비 템플릿(TUSK, 재블린 2기, BMP-2M, T-72B3), 증원 단계(T+10분 10% 할인, T+20분 20% 할인),
  직할·배속 자산, 포병·CAS 구매를 고른다. 포인트(2,000점)·제약·비편제·예상 지속 교전 시간이 바로 보인다.
  "저장"은 `user://forces/이름.json` 에 남는다. 배치 단계에서는 부대를 고르고 우클릭으로 배치 구역 안에 옮긴 뒤 "전투 시작".
- **샌드박스**: 편성 없이 청군 전차·기계화 중대, 정찰 소대 / 적군 2개 중대로 바로 시작.

| 입력 | 동작 |
| --- | --- |
| 좌클릭 | 부호 선택 (Ctrl: 추가 선택, 더블클릭: 소속 중대 선택) |
| 우클릭 | 선택 제대에 명령 (우클릭 드래그: 도착 방향 지정, Shift: 대기열) |
| WASD, 화살표, 화면 가장자리 | 카메라 이동 |
| 휠 / 가운데 버튼 드래그 | 확대·축소 / 회전·기울기 |
| Q / F / M | 공격 이동 / 급속 이동 / 이동 |
| E | 명령 취소(정지) |
| H | 사격 중지 ↔ 자유 (중지 중에는 응사만) |
| U | 탑승 ↔ 하차 |
| B | 철수 |
| T (한 번 더: 포병) | 화력 요청: 박격포 → 포병 → 좌클릭으로 표적, 우클릭·Esc 취소 |
| G (한 번 더: 포병) | 연막 요청: 박격포 연막 → 포병 연막 |
| P | 근접항공지원 |
| Ctrl+1~9 / 1~9 | 부대 지정 / 불러오기 (두 번 누르면 카메라 이동, Shift: 추가 선택) |
| Tab / Shift+Tab | 하위 제대 선택(중대 → 소대) / 상위 제대 |
| F1 ~ F4 | 1~4 번째 중대 선택 |
| Y / J | 확보 / 방어 (이 게임에만 있는 명령) |
| Alt+1 ~ 7 | 대형 (소대: 종대·쐐기·횡대·제대 좌·제대 우·V·사주 경계, 중대: 대응하는 중대 대형) |
| L | 이동 방식 전환: 일반 → 경계 이동 → 교대 약진 |
| K | 보급 요청(선택 제대 → 가장 가까운 보급반이 온다) |
| Space, `=` / `-` | 일시정지, 배속 (0.5x · 1x · 2x · 4x) |
| 미니맵 클릭·드래그 | 카메라 이동 (오른쪽 아래) |
| F9 | 전장의 안개 켜기/끄기(디버그) |
| / | 가이드 열기·닫기 |
| F10 | 디버그 오버레이 |

단축키는 Broken Arrow 기본 배치(공식 매뉴얼)를 따랐다. BA 에 있지만 이 게임에 없는 명령(R 후진, Z 질주, X 연막탄, C 기만체, V 고도)은
누르면 "아직 없음" 안내만 나온다. 웹판에서는 브라우저가 Ctrl+숫자(탭 전환), Alt+숫자(리눅스 탭 전환)를 먼저 가져갈 수 있다 —
그때는 대형 버튼을 누르거나 데스크톱판을 쓴다.

명령은 지휘망을 타고 지연되어 전파된다(대대→중대 15초, 중대→소대 10초, 실행 준비 5초 × 통신 품질 × 숙련 × 상태).
중대 대형 안 소대에 직접 이동 명령을 내리면 그 소대는 분리되고 지휘 용량을 2 쓴다. 지휘 용량은 분당 6(상단 바).
커서 옆에 "실행까지 약 N초" 미리보기, 지도에는 전파 중인 명령이 점선과 남은 시간으로 표시된다.

전투(M3): 엘리먼트는 2초마다 주변을 탐지하고(거리·크기·은폐·지형·이동·대형 측면 경계), 본 적은 소대→중대→대대로
지연 보고된다. 화면에는 아군이 지금 보고 있는 적과 대대에 보고된 적(오래되면 흐림)만 보인다.
사격은 명중(거리·숙련·이동·제압·피복) → 맞은 방향 장갑 대 관통력(격파/손상/튕김) 순서로 판정하고,
보병은 인원 손실, 탑승 중 IFV 가 격파되면 분대가 절반을 잃고 내린다. 앞 아군 차량이 사선을 가리면 쏘지 않는다.
제압·사기(40 이하 공격 거부, 20 이하 패주)와 소대 SOP(접촉 시 정지 사격·철수 기준·하차)가 자동으로 돈다.

화력·보급(M5): 화력 요청은 지휘 용량 1을 쓰고, 표적을 보는 관측자(관측반이 최선)와 통신 상태에 따라 지연·정확도가
달라진다(관측자 없으면 지도 좌표 사격: 산포 2배, +30초). 박격포는 지도 위 박격포 소대가 실제로 쏘고 탄을 쓴다.
포병·CAS 는 편성 때 산 횟수만큼, CAS 는 관측반이 표적을 봐야 하고 적 방공에 격추될 수 있다. 연막은 시선을 막는다.
보급 명령을 받은 소대는 멈춰 기다리고, 보급반이 와서 분당 20 단위를 채운다. 손상 차량은 보급반 옆 5분이면 수리된다.

스커미시(M6): 거점 4곳(반경 300 m, 한쪽만 있으면 점령, 분당 1점)과 임무 목표, 40분 시간 제한, 전투력 40% 이하 즉시 패배.
증원은 T+10분·T+20분에 진입 지점으로 들어와 소속 중대에 합류한다. 적은 TF AI 가 같은 지휘망(지연·지휘 용량)으로 지휘한다.
전투가 끝나면 사후 검토(AAR: 손실, 명령 지연 통계, 거점, 타임라인, 승패 요인)가 뜬다.

헤드리스 AI 대 AI 한 판(AAR JSON):

```bash
$GODOT --headless --path . -s res://tools/headless_battle.gd -- --seed=7 --out=/tmp/aar.json
# 편성 파일을 바꿔 붙이기: --blue=편성.json --red=편성.json (편성 화면에서 저장한 파일 형식)
```

성능 측정(엘리먼트 ~300개, 시스템별 틱 시간):

```bash
$GODOT --headless --path . -s res://tools/profile_sim.gd -- --ticks=1500 --extra=5
```

## 테스트

Windows (PowerShell). 콘솔 출력이 보이도록 `_console.exe` 를 쓴다.

```powershell
$env:GODOT = "C:\path\to\Godot_v4.7.2-stable_win64_console.exe"
.\tools\run_tests.ps1     # GUT 단위 테스트
.\tools\run_smoke.ps1     # 샌드박스·기본 편성 스커미시를 헤드리스로 50틱씩 돌려 보기
```

Linux / macOS:

```bash
GODOT=/path/to/godot ./tools/run_tests.sh
GODOT=/path/to/godot ./tools/run_smoke.sh
GODOT=/path/to/godot ./tools/check_warnings.sh   # 타입·안전성 경고를 오류로 올려 검사
```

에디터에서는 하단 GUT 패널 → Run All 로도 돌릴 수 있다.

## 배포

`.github/workflows/ci-deploy.yml` 이 `main` 에 올라올 때마다 정적 타입 검사 → GUT 테스트 → 스모크를 돌리고,
통과하면 웹·Windows·Linux 로 내보내 **웹 빌드를 GitHub Pages 에 배포**한다. `v*` 태그를 올리면 Windows·Linux
실행 파일을 Releases 에 첨부한다(예: `git tag v0.1.1 && git push origin v0.1.1`). 태그 없이 Actions 화면의
**Run workflow** 에서 `release_tag` 를 적어 실행해도 같은 릴리스를 만든다.

- 내보내기 프리셋: `export_presets.cfg` (웹은 단일 스레드 빌드라 Pages 에서 별도 헤더 없이 돈다)
- 웹은 Compatibility 렌더러(WebGL 2)로 바뀌고, 한글은 내장 글꼴 Noto Sans KR(SIL OFL, `assets/fonts`)로 그린다.
- 로컬 내보내기: Godot 4.7.2 내보내기 템플릿 설치 후 `godot --headless --path . --export-release Web build/web/index.html`

## 폴더 구조

```
data/        JSON 정의 (elements, weapons, units, formations.json, toe, maps, scenarios, forces=기본 편성)
sim/         시뮬레이션. 노드·Engine·Time 에 의존하지 않는 순수 GDScript (RefCounted)
  battle.gd    한 판 전체(지형·경로·제대·시스템)
  defs/        데이터 정의 클래스 + DefReader(검증)
  echelon/     엘리먼트·소대·중대·대대, 가이드, 이동 시스템, 생성기
  orders/      명령 객체·실행기·전파(지휘 용량)·지휘망
  path/        지형 그리드, 경로탐색(AStarGrid2D), 공간 해시
  formation/   대형 슬롯 계산
  combat/      탐지·보고, 사격·피해, 제압·사기
  force/       편성(ForceSpec)·편성 규칙(포인트·제약·비편제)·자동 배치
  fires/       간접화력(박격포·포병·CAS·연막, 관측 체인)
  supply/      보급·재적재·수리
  victory/     거점·임무 목표·승점·승패, 전투 기록(AAR)
  ai/          소대 SOP 반응, TF(대대) AI
  world.gd     10 Hz 고정 틱 루프, 배속, 일시정지, 시드 RNG
game/        씬, 표현 계층, UI (Node)
services/    데이터 로더, 디버그 오버레이 등
tests/       GUT 테스트 (unit: 단위, integration: 마일스톤 검증 시나리오)
tools/       실행 스크립트
docs/        PLAN.md, DECISIONS.md
```

## 규약 (PLAN 18.2 요약)

- 모든 변수·함수에 타입을 적는다. 프로젝트 설정에서 미타입 선언과 unsafe 접근을 경고로 켜 두었다.
- `sim/` 은 `Node`, `get_tree()`, `Engine`, `Time` 에 접근하지 않는다. 시간은 틱 정수로만 다룬다.
- 난수는 `world.rng` 에서만 뽑는다. 전역 `randf()` 금지.
- 시뮬 → 표현 통신은 `world.events` 큐로만.
- 수치는 `data/` 또는 `sim/constants.gd` 에만 둔다.
