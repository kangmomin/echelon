# ECHELON (가제)

[![CI / Deploy](https://github.com/kangmomin/echelon/actions/workflows/ci-deploy.yml/badge.svg)](https://github.com/kangmomin/echelon/actions/workflows/ci-deploy.yml)

- **기존 공개 빌드:** [웹](https://kangmomin.github.io/echelon/) · [Releases](https://github.com/kangmomin/echelon/releases) (Unity 전환 후 배포부터 갱신)

Broken Arrow 를 기반으로 **제대(梯隊) 지휘·편제표·전술 대형·지휘통제**를 더한 현대전 RTT 프로토타입.
설계는 [`docs/PLAN.md`](docs/PLAN.md), 설계 변경 기록은 [`docs/DECISIONS.md`](docs/DECISIONS.md).

- 엔진: **Unity 6000.3.27f1 LTS**, URP, C#
- 테스트: .NET/NUnit, Unity Test Framework, 8개 시드 골든 비교(`tests/golden`)
- 현재 단계: **M7 완료 + Unity 전환**. 검증 결과는 [`docs/REPORT.md`](docs/REPORT.md), 플레이테스트 기록은 [`docs/PLAYTEST.md`](docs/PLAYTEST.md)

## 실행

Unity Hub에서 **프로젝트 추가(Add project from disk)**로 이 저장소의 `unity/` 디렉터리를 연다. Unity 버전은 **6000.3.27f1**이다.
처음 패키지 import가 끝나면 `Assets/Scenes/Boot.unity`를 열고 상단 **Play 버튼(Ctrl+P)**을 누른다. 메인 메뉴가 나온다.

빌드한 실행 파일은 `build/unity/windows/Echelon.exe`(Windows), `build/unity/linux/Echelon.x86_64`(Linux)다.
브라우저용은 `build/unity/web/index.html`이며, 이 디렉터리를 로컬 HTTP 서버로 열어야 한다(`file://`로는 실행되지 않는다).

- **튜토리얼**: 처음이라면 여기부터(약 10분, 14단계). 작은 전장에서 침로 → 선택 → 부대 지정 → 대형 → 시간 조절 →
  이동 명령과 지휘 지연 → 사격 통제 → 공격 이동 → 화력 요청 → 적 소대 격파까지 과제를 하나씩 해 본다.
- **가이드**: 메뉴의 "가이드" 또는 전투 중 `/` 키(왼쪽 위 버튼). 조작, 제대와 지휘 지연, 대형, 탐지·교전, 화력·보급,
  편성, 승리 조건, 요령을 정리한 도움말. 전투 중에 열리면 일시정지된다.
- **장비와 병과**: 장비 46종, 무기 46종. 각 역할과 장단점, 능력치, 제원은 편성 화면의 정보 카드와 툴팁에서 확인할 수 있다.
- **스커미시**: 편성 화면 → 배치 단계 → 전투. 편성 화면에서 대대 편제표의 중대 슬롯을 채우고, 소대 줄의
  "이동…"으로 교차 배속, 장비 템플릿(TUSK, 재블린 2기, BMP-2M, T-72B3), 증원 단계(T+10분 10% 할인, T+20분 20% 할인),
  직할·배속 자산, 포병·CAS 구매를 고른다. 포인트(2,000점)·제약·비편제·예상 지속 교전 시간이 바로 보인다.
  편성 저장은 데스크톱에서 `Application.persistentDataPath/forces/이름.json`, WebGL에서는 PlayerPrefs에 남는다.
  배치 단계에서는 부대를 고르고 우클릭으로 배치 구역 안에 옮긴 뒤 "전투 시작".
- **샌드박스**: 편성 없이 청군 전차·기계화 중대, 정찰 소대 / 적군 2개 중대로 바로 시작.

| 입력 | 동작 |
| --- | --- |
| 좌클릭 | 부호 선택 (Ctrl: 추가 선택, 더블클릭: 소속 중대 선택) |
| 우클릭 | 선택 제대에 명령 (우클릭 드래그: 도착 방향 지정, Shift: 대기열) |
| WASD, 화살표, 화면 가장자리 | 침로 이동 |
| 휠 / 중간 버튼 드래그 | 확대·축소 / 회전·기울기 |
| Q / F / M | 공격 이동 / 급속 이동 / 이동 |
| E | 명령 취소(정지) |
| H | 사격 중지 ↔ 자유 (중지 중에는 응사만) |
| U | 탑승 ↔ 하차 |
| B | 철수 |
| T (한 번 더: 포병) | 화력 요청: 박격포 → 포병 → 좌클릭으로 표적, 우클릭·Esc 취소 |
| G (한 번 더: 포병) | 연막 요청: 박격포 연막 → 포병 연막 |
| P | 근접항공지원 |
| Ctrl+1~9 / 1~9 | 부대 지정 / 불러오기 (두 번 누를 때 침로 이동, Shift: 추가 선택) |
| Tab / Shift+Tab | 하위 제대 선택(중대 → 소대) / 상위 제대 |
| F1 ~ F4 | 1~4 번째 중대 선택 |
| Y / J | 확보 / 방어 (이 게임에만 있는 명령) |
| V | 공격헬기 고도 전환: 저공 25 m ↔ 고공 90 m |
| N / I / O | 공병: 지뢰 매설 / 지뢰 제거 / 진지 구축 (우클릭으로 지점) |
| Alt+1 ~ 7 | 대형 (소대: 종대·쐐기·횡대·제대 좌·제대 우·V·사주 경계, 중대: 대응하는 중대 대형) |
| L | 이동 방식 전환: 일반 → 경계 이동 → 교대 약진 |
| K | 보급 요청(선택 제대 → 가장 가까운 보급반이 온다) |
| Space, `=` / `-` | 일시정지, 배속 (0.5x · 1x · 2x · 4x) |
| 미니맵 클릭·드래그 | 침로 이동 (오른쪽 아래) |
| F9 | 전장의 안개 켜기/끄기(디버그) |
| / | 가이드 열기·닫기 |
| F10 | 디버그 오버레이 |

단축키는 Broken Arrow 기본 배치(공식 매뉴얼)를 따랐다. BA 에 있지만 이 게임에 없는 명령(R 후진, Z 질주, X 연막탄, C 기만체)은
누를 때 "아직 없음" 안내만 나온다. 웹판에서는 브라우저가 Ctrl+숫자(탭 전환), Alt+숫자(리눅스 탭 전환)를 먼저 가져갈 수 있다 —
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

스커미시(M6): 거점 4곳(반경 300 m, 한쪽만 있으면 점령, 분당 1점)과 임무 목표, 40분 시간 제한이 있다.
현재 전장 전투력이 지금까지 투입된 부대 가치의 25% 이하가 되면 즉시 패배한다(대기 증원은 제외).
증원은 T+10분·T+20분에 진입 지점으로 들어와 소속 중대에 합류한다. 적은 TF AI 가 같은 지휘망(지연·지휘 용량)으로 지휘한다.
전투가 끝나면 사후 검토(AAR: 손실, 명령 지연 통계, 거점, 타임라인, 승패 요인)가 뜬다.

## 명령행 실행·검증

Windows (PowerShell). `UNITY`는 설치 위치가 다를 때만 설정하면 된다. .NET 8 SDK도 필요하다.

```powershell
$env:UNITY = "C:\path\to\Unity.exe"
.\tools\run_dotnet_tests.ps1
.\tools\run_unity_tests.ps1 EditMode
.\tools\run_unity_tests.ps1 PlayMode
.\tools\run_headless.ps1 --seed=7 --out=build/validation/aar_7.json
.\tools\build_unity.ps1 all
```

Linux / macOS:

```bash
UNITY=/path/to/Unity ./tools/run_unity_tests.sh EditMode
UNITY=/path/to/Unity ./tools/run_unity_tests.sh PlayMode
./tools/run_dotnet_tests.sh
./tools/run_headless.sh --seed=7 --out=build/validation/aar_7.json
./tools/build_unity.sh all
```

실제 전투 50틱과 8개 시드 골든 비교(시뮬레이션을 바꿨다면 반드시 돌린다):

```bash
./tools/run_unity.sh -batchmode -nographics -executeMethod Echelon.Unity.Editor.EchelonSmoke.Run -- --scene=sandbox --smoke-ticks=50
./tools/check_golden.sh          # Windows: .\tools\check_golden.ps1
```

디스플레이가 없는 Linux 컨테이너에서는 플레이어 스모크에 SDL dummy 백엔드를 지정한다(데스크톱 GUI 실행에는 사용하지 않는다).

```bash
SDL_VIDEODRIVER=dummy ./build/unity/linux/Echelon.x86_64 -batchmode -nographics -- --scene=sandbox --smoke-ticks=50
```

골든 데이터는 Godot 4.7.2 구현에서 뽑은 기준값이다(Godot 코드는 D-045로 제거). AAR canonical JSON은 정확히 비교한다.
Godot 고유의 엔진 상태 해시는 제외하고, 기본 수학 라이브러리의 작은 차이는 1e-12로 제한한다.
상세 스냅샷은 시드 1·8에서 비교하고, 나머지 시드는 개수·도착 기록·최종 틱·AAR을 검증한다.
의도한 규칙 변경으로 결과가 바뀌면 골든과 달라지는 것이 정상이며, 그때는 변경 이유를 DECISIONS에 남기고 기준값을 갱신한다.

브라우저 검증은 Node.js와 Chrome을 사용한다. 먼저 WebGL 빌드와 로컬 서버를 띄운 뒤 다른 터미널에서 검증 스크립트를 실행한다.
`CHROME`으로 Chrome 경로를, `CHROME_ANGLE`로 렌더링 백엔드(예: `d3d11`, `swiftshader`)를 바꿀 수 있다.

```bash
./tools/build_unity.sh web
python3 tools/serve_unity_web.py --port 18080
# 다른 터미널
node --experimental-websocket tools/web_smoke.mjs
```

## 배포

로컬 빌드는 `./tools/build_unity.sh all`(Windows에서는 `.\tools\build_unity.ps1 all`)로 만든다. Unity Hub의 Windows·Linux·WebGL 빌드 모듈이 필요하다.

`.github/workflows/ci-deploy.yml`은 .NET 테스트·골든 비교·데이터 동기화 검사와 Unity EditMode·PlayMode를 거쳐 WebGL·Windows·Linux를 빌드한다.
통과하면 `main`에서 **웹 빌드를 GitHub Pages에 배포**하고, `v*` 태그 또는 `release_tag`로 데스크톱 실행 파일을 Releases에 첨부한다.
원격 Unity 실행에는 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` Secrets가 필요하다(GameCI Personal 라이선스).

- `build/unity/windows/Echelon.exe`: Windows 플레이어
- `build/unity/linux/Echelon.x86_64`: Linux 플레이어
- `build/unity/web/index.html`: WebGL 2. 로컬에서는 `python3 tools/serve_unity_web.py --port 8000` 후 `http://localhost:8000`으로 연다. Unity 압축 해제 대체 기능도 켜져 있어 Brotli 헤더를 설정할 수 없는 일반 정적 호스팅을 지원한다
- 한글은 내장 Noto Sans KR(SIL OFL, `unity/Assets/Fonts`)로 그린다

## 디렉터리 구조

```
data/                 공유 JSON 정의(장비·무기·제대·대형·편제표·지도·시나리오·기본 편성)
src/Echelon.Core/     Unity와 무관한 순수 C# 시뮬레이션(netstandard2.1)
src/Echelon.Headless/ 카탈로그·지형 검증, 실제 전투·정규 스냅샷 생성기
unity/                Unity 프로젝트
  Assets/Scenes/      Boot 씬
  Assets/Scripts/App/ 메뉴·전투 흐름, 명령 입력, 코어-표현 어댑터
  Assets/Scripts/Presentation/ 지형·모델·침로·부호·전투 효과
  Assets/Scripts/UI/  메뉴·편성·전투 HUD·가이드·튜토리얼·AAR
  Assets/Scripts/Platform/ JSON 리소스·플랫폼별 편성 저장
  Assets/Tests/       EditMode·PlayMode 테스트
tests/csharp/        .NET 테스트
tests/golden/        동일 시드·지형·전투 기준 데이터(Godot 4.7.2 기준값)
tools/               실행·검증·빌드 스크립트
docs/                PLAN.md, DECISIONS.md, REPORT.md
```

## 규약

- 공유 시뮬레이션 시간은 10 Hz 정수 틱으로만 다루고 `Time.timeScale`에 의존하지 않는다. 난수는 `world.rng`에서만 뽑는다.
- `src/Echelon.Core`는 `UnityEngine`에 의존하지 않는다. 시뮬 → 표현 통신은 `world.events`와 읽기 전용 상태로 한다.
- `data/`가 유일한 정의 원본이다. Unity의 `Resources/GameData`는 `tools/sync_unity_data.sh`로 만드는 복사본이므로 직접 고치지 않는다.
  CI는 `--check`로 복사본이 원본과 같은지 검사한다.
