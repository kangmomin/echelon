using System.Collections.Generic;

namespace Echelon.Unity.UI.Tutorial
{
    public static class TutorialContent
    {
        public const int StepCount = 14;

        private static readonly IReadOnlyList<TutorialStepDefinition> AllSteps = new[]
        {
            Step(
                TutorialStepId.Welcome,
                "환영합니다",
                Text(
                    "ECHELON 에서는 차량 하나하나가 아니라 <b>중대·소대</b>를 지휘합니다. 명령은 무전을 타고 내려가므로 <b>10초 안팎 늦게</b> 실행됩니다.",
                    "이 튜토리얼(약 10분)에서 카메라, 선택, 명령, 대형, 화력 요청을 한 번씩 해 봅니다. 언제든 " + Key("/") + " 키로 가이드를 열 수 있습니다."),
                false),
            Step(
                TutorialStepId.Camera,
                "카메라",
                Key("W A S D") + "(또는 화살표)로 카메라를 움직이고, " + Key("마우스 휠") + "로 확대·축소해 보세요. "
                    + Key("가운데 버튼") + " 드래그로 돌리고 기울일 수 있습니다."),
            Step(
                TutorialStepId.CompanySelection,
                "중대 선택",
                Key("F1") + "를 누르거나 지도에서 <b>A 중대</b> 부호를 좌클릭하세요. 소대 부호를 <b>더블클릭</b>해도 소속 중대가 선택됩니다.\n"
                    + "왼쪽 편제 패널에서 이름을 눌러도 됩니다."),
            Step(
                TutorialStepId.Subselection,
                "하위 제대",
                Key("Tab") + "을 누르면 선택한 중대의 <b>소대들</b>이 선택됩니다. 그다음 " + Key("Shift+Tab") + "으로 다시 중대를 고르세요.\n"
                    + "평소에는 중대에 명령하고, 꼭 필요할 때만 소대에 직접 명령합니다(그 소대는 중대에서 <b>분리</b>됩니다)."),
            Step(
                TutorialStepId.ControlGroups,
                "부대 지정",
                "A 중대를 고른 채 " + Key("Ctrl+1") + "로 1번에 지정하고, " + Key("Esc") + "로 선택을 푼 뒤, " + Key("1")
                    + "을 눌러 다시 불러오세요. 빠르게 두 번 누르면 카메라가 갑니다."),
            Step(
                TutorialStepId.Formation,
                "대형",
                Key("Alt+3") + "으로 A 중대를 <b>소대 횡대</b>로 바꾸세요. 화면 아래 명령 바의 대형 버튼을 눌러도 됩니다.\n"
                    + "횡대는 정면 화력이 가장 크고 포격에 강하지만 느립니다. 종대는 빠르지만 앞으로 쏠 수 있는 차량이 적습니다.\n"
                    + "<color=#a6a9b3>대형 명령도 무전으로 내려가므로 잠시 뒤에 바뀝니다.</color>"),
            Step(
                TutorialStepId.TimeControls,
                "시간 조절",
                Key("Space") + "로 일시정지하고, " + Key("=") + "로 배속을 올려 보세요(" + Key("-") + " 로 내림). 마지막에 다시 "
                    + Key("Space") + "로 재개하세요.\n일시정지 중에도 명령을 내릴 수 있습니다. 복잡할 때는 멈추고 생각하세요."),
            Step(
                TutorialStepId.MovementOrder,
                "이동 명령",
                "A 중대를 선택한 상태에서 강 건너 <color=#ffd94d><b>노란 표시</b></color>를 " + Key("우클릭") + "하세요.\n"
                    + "우클릭하기 전 커서 옆의 <b>\"실행까지 약 N초\"</b>가 지휘 지연입니다. 우클릭한 채로 끌면 도착 후 바라볼 방향을 정할 수 있습니다."),
            Step(
                TutorialStepId.CommandDelay,
                "지휘 지연",
                "명령은 <b>대대 → 중대 → 소대</b>로 무전을 타고 내려갑니다. 지도의 점선과 숫자가 남은 시간입니다.\n"
                    + "명령이 소대에 닿아 움직이기 시작할 때까지 기다려 보세요(" + Key("=") + "로 배속을 올려도 됩니다)."),
            Step(
                TutorialStepId.RulesOfEngagement,
                "사격 통제",
                "이동하는 동안 " + Key("H") + "를 눌러 <b>사격 중지</b>로 바꾸고(맞기 전에는 먼저 쏘지 않음 — 매복용), 다시 "
                    + Key("H") + "로 <b>사격 자유</b>로 돌려 놓으세요.\n"
                    + "왼쪽 아래 부대 카드의 \"사격\" 항목이 바뀝니다(역시 무전으로 전달)."),
            Step(
                TutorialStepId.AttackMove,
                "공격 이동",
                "정찰 보고: <color=#ff7a70><b>적 BMP-3 소대</b></color>가 BRAVO 능선에 나타났습니다.\n"
                    + Key("Q") + "를 누른 뒤 빨간 표시를 " + Key("우클릭") + "하세요. 공격 이동은 가다가 만나는 적과 교전합니다.\n"
                    + "같은 방식으로 " + Key("F") + " 급속 이동, " + Key("B") + " 철수, " + Key("Y") + " 확보, " + Key("J") + " 방어를 씁니다."),
            Step(
                TutorialStepId.FireSupport,
                "화력 지원",
                Key("T") + "를 눌러 <b>박격포</b> 요청 모드로 바꾸고 빨간 표시 근처를 " + Key("좌클릭") + "하세요.\n"
                    + Key("T") + "를 한 번 더 누르면 포병, " + Key("G") + "는 연막, " + Key("P")
                    + "는 근접항공지원입니다. 표적을 보는 아군(관측자)이 있으면 빠르고 정확합니다."),
            Step(
                TutorialStepId.EnemyDestruction,
                "적 소대 격파",
                "적 소대를 격파하세요. 오른쪽 알림과 왼쪽 편제 패널의 전투력(%)을 보세요.\n"
                    + "손실과 제압이 쌓이면 사기가 떨어져 <b>공격을 거부하거나 패주</b>합니다. 시간이 오래 걸리면 "
                    + Key("=") + "로 배속을 올리세요."),
            Step(
                TutorialStepId.Completion,
                "튜토리얼 완료",
                "수고했습니다. 이제 기본 조작을 모두 해 봤습니다.\n"
                    + "• 대대 편성, 보급, 승리 조건은 " + Key("/") + " 가이드에 정리돼 있습니다.\n"
                    + "• 메뉴의 <b>스커미시</b>에서 직접 편성한 대대로 AI 와 한 판(최대 40분)을 해 보세요.",
                false)
        };

        public static IReadOnlyList<TutorialStepDefinition> Steps => AllSteps;

        public static TutorialStepDefinition Get(TutorialStepId id)
        {
            return AllSteps[(int)id];
        }

        private static TutorialStepDefinition Step(
            TutorialStepId id,
            string title,
            string body,
            bool requiresCompletion = true)
        {
            return new TutorialStepDefinition(id, title, body, requiresCompletion);
        }

        private static string Key(string key)
        {
            return "<color=#f2cc59><b>" + key + "</b></color>";
        }

        private static string Text(params string[] lines)
        {
            return string.Join("\n", lines);
        }
    }
}
