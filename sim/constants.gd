class_name SimConst
extends RefCounted
## 시뮬레이션 전역 상수. 밸런스 수치는 data/ 에 두고, 여기에는 엔진 구조에 관한 값만 둔다.

## 시뮬레이션 틱 주파수(Hz). PLAN 15.1.
const TICK_RATE: int = 10
## 한 틱의 시뮬 시간(초).
const TICK_DT: float = 1.0 / TICK_RATE
## 누적기 비교용 허용 오차. 0.1 을 반복해서 더할 때 생기는 부동소수 오차를 흡수한다.
const TICK_EPSILON: float = 1e-6

## 배속 단계. PLAN 4장: 0.5x ~ 4x.
const SPEED_STEPS: Array[float] = [0.5, 1.0, 2.0, 4.0]
const DEFAULT_SPEED_INDEX: int = 1

## advance() 한 번에 최대로 돌리는 틱 수. 프레임이 크게 끊기면 밀린 시간은 버린다.
const MAX_TICKS_PER_ADVANCE: int = 8

## 경로탐색·지형 그리드 셀 크기(m). PLAN 17.3.
const CELL_SIZE_M: float = 10.0
