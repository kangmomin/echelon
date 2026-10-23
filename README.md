# ECHELON (가제)

Broken Arrow 를 기반으로 **제대(梯隊) 지휘·편제표·전술 대형·지휘통제**를 더한 현대전 RTT 프로토타입.
설계는 [`docs/PLAN.md`](docs/PLAN.md), 설계 변경 기록은 [`docs/DECISIONS.md`](docs/DECISIONS.md).

- 엔진: **Godot 4.7.2** (Forward+), 정적 타입 GDScript
- 테스트: GUT 9.7.1 (`addons/gut`)
- 현재 단계: **M1 (대형 이동) 완료**

## 실행

Godot 4.7.2 로 이 폴더의 `project.godot` 를 연다. 처음 열 때 에디터가 import 를 한 번 수행한다.
F5 로 실행하면 샌드박스 전투(청군 전차·기계화 중대, 정찰 소대 / 적군 2개 중대)가 시작된다.

| 입력 | 동작 |
| --- | --- |
| 좌클릭 | 부호 선택 (Ctrl: 추가 선택, 더블클릭: 소속 중대 선택) |
| 우클릭 | 선택 제대에 명령 (우클릭 드래그: 도착 방향 지정, Shift: 대기열) |
| M / G / A / S / D / X | 명령 종류: 이동 / 급속 이동 / 공격 이동 / 확보 / 방어 / 철수 |
| H | 정지 |
| 1 ~ 7 | 대형 (소대: 종대·쐐기·횡대·제대 좌·제대 우·V·사주 경계, 중대: 대응하는 중대 대형) |
| Q / W / E | 이동 방식: 일반 / 경계 이동 / 교대 약진 |
| F1 ~ F4 | 1~4 번째 중대 선택 |
| 화살표, 화면 가장자리 | 카메라 이동 |
| 휠 / 가운데 버튼 드래그 | 확대·축소 / 회전·기울기 |
| Space, `=` / `-` | 일시정지, 배속 (0.5x · 1x · 2x · 4x) |
| F10 | 디버그 오버레이 |

## 테스트

Windows (PowerShell). 콘솔 출력이 보이도록 `_console.exe` 를 쓴다.

```powershell
$env:GODOT = "C:\path\to\Godot_v4.7.2-stable_win64_console.exe"
.\tools\run_tests.ps1     # GUT 단위 테스트
.\tools\run_smoke.ps1     # 메인 씬을 헤드리스로 50틱 돌려 보기
```

Linux / macOS:

```bash
GODOT=/path/to/godot ./tools/run_tests.sh
GODOT=/path/to/godot ./tools/run_smoke.sh
GODOT=/path/to/godot ./tools/check_warnings.sh   # 타입·안전성 경고를 오류로 올려 검사
```

에디터에서는 하단 GUT 패널 → Run All 로도 돌릴 수 있다.

## 폴더 구조

```
data/        JSON 정의 (elements, weapons, units, formations.json, toe, maps, scenarios)
sim/         시뮬레이션. 노드·Engine·Time 에 의존하지 않는 순수 GDScript (RefCounted)
  battle.gd    한 판 전체(지형·경로·제대·시스템)
  defs/        데이터 정의 클래스 + DefReader(검증)
  echelon/     엘리먼트·소대·중대·대대, 가이드, 이동 시스템, 생성기
  orders/      명령 객체·실행기
  path/        지형 그리드, 경로탐색(AStarGrid2D), 공간 해시
  formation/   대형 슬롯 계산
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
