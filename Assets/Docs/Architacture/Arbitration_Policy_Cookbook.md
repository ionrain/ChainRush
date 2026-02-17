# Arbitration Policy Cookbook

*(Набор рецептов арбитража (Arbitration Layer) для типовых ситуаций)*

Версия: 1.0\
Статус: Recommended (SHOULD), с обязательными ограничениями (MUST) из
Architecture Compliance Standard\
Связан с документами:\
- Game Systems Architecture Framework\
- Orchestration Architecture Charter\
- Architecture Compliance Standard\
- Architectural Fitness Functions Specification

------------------------------------------------------------------------

## 1. Назначение (Purpose)

Данный документ описывает типовые политики арбитража (Arbitration
Policies) и рекомендуемые паттерны их применения.

Политики предназначены для выбора `Decision` на основе списка `Proposal`
при соблюдении инвариантов:

-   Arbitration Layer не выполняет команды и не имеет side effects
-   Execution Layer не принимает решений
-   Все изменения состояния выполняются через Command pipeline

------------------------------------------------------------------------

# 2. Общая терминология (Shared Terms)

## 2.1 Proposal Shape (Рекомендуемая форма Proposal)

Каждое предложение SHOULD содержать:

-   `ProposalId`
-   `Source` (Player / AI / Director / Economy / Scenario)
-   `IntentType` (Move, Attack, Cast, Spawn, Spend, Offer, etc.)
-   `Target` (entity / position / contentId)
-   `Priority` (integer или enum)
-   `Score` (float)
-   `Cost` (budget cost, опционально)
-   `Tags` (набор маркеров)
-   `CommitGroup` (ключ группы "commit window", опционально)
-   `TTL` (time-to-live, опционально)
-   `Constraints` (например: requiresThreat, requiresInputLock, etc.)

Arbiter MUST работать только с этими данными и `WorldSnapshot` (если
разрешено контрактом), без доступа к внешнему состоянию систем.

------------------------------------------------------------------------

## 2.2 Decision Shape (Рекомендуемая форма Decision)

`Decision` SHOULD содержать:

-   выбранные proposals (одно или список)
-   список отклонённых proposals (для telemetry, опционально)
-   причину выбора (reason code, опционально)
-   "locks/commitments" (если применяется commit windows)

------------------------------------------------------------------------

# 3. Policy Recipe: Player Override (Player \> AI)

## 3.1 Цель

Обеспечить приоритет действий игрока над AI и автоповедением, кроме
случаев forced states (оглушение, кат-сцена, запрет ввода).

------------------------------------------------------------------------

## 3.2 Когда применять

-   RTS: команда игрока должна перебивать автопоиск цели
-   ARPG: нажатие скилла перебивает "idle" и "auto target switch"
-   Survivors-like: выбор апгрейда перебивает всё, переводя игру в
    UI-flow

------------------------------------------------------------------------

## 3.3 Правило

1.  Если в списке proposals есть `Source=Player` и они валидны по
    constraints, то Decision MUST включить player proposals.
2.  AI proposals MAY быть отброшены, если конфликтуют по intent group.
3.  Исключение: если snapshot содержит `InputLocked=true` или
    `ForcedState=true`, player proposals MUST быть отклонены с reason
    code.

------------------------------------------------------------------------

## 3.4 Implementation Notes

-   Использовать `IntentGroup` (например Movement, Combat, Interaction,
    MetaChoice)
-   Player proposals получают базовый приоритет `P_PLAYER`
-   AI proposals ограничиваются `P_AI_MAX < P_PLAYER`

------------------------------------------------------------------------

# 4. Policy Recipe: Threat Override (Threat \> Idle/Utility)

## 4.1 Цель

Когда существует непосредственная угроза (Threat), боевые/защитные
предложения должны перебивать idle/utility.

------------------------------------------------------------------------

## 4.2 Когда применять

-   любые realtime боевые игры
-   защитные сценарии (tower defense, escort)
-   спортивные симуляторы (контратака/перехват мяча как "угроза")

------------------------------------------------------------------------

## 4.3 Требования к Sensing (Threat Snapshot)

Sensing SHOULD публиковать `ThreatSnapshot`, содержащий:

-   `HasThreat` (bool)
-   `ThreatLevel` (0..N)
-   `NearestThreatDistance`
-   `ThreatTargets` (опционально)
-   `ThreatTTL` (истечение угрозы после пропажи контакта)

------------------------------------------------------------------------

## 4.4 Правило

1.  Если `ThreatSnapshot.HasThreat=true`:
    -   proposals с тегом `Combat` или `Defense` получают +Δ к Score
    -   proposals с тегом `Idle` получают -Δ к Score или отклоняются
2.  Если `ThreatTTL` ещё не истёк, threat override сохраняется
    (анти-дёрганье).
3.  При `ThreatLevel` выше порога допускается принудительное решение
    (например Retreat).

------------------------------------------------------------------------

## 4.5 Anti-Jitter (Hysteresis)

Threat override SHOULD использовать: - `ThreatTTL` - минимальное время
удержания решения (commit window) - "cooldown" на возвращение в idle

------------------------------------------------------------------------

# 5. Policy Recipe: Commit Windows (Lock / Commit-based Arbitration)

## 5.1 Цель

Устранить дёрганье решений и конфликтующие переключения в коротком окне
времени.

Commit window фиксирует решение (или его группу) на определённое время.

------------------------------------------------------------------------

## 5.2 Когда применять

-   боевые атаки и касты (commit to attack)
-   выбор цели (target lock)
-   перемещение к цели (commit to path)
-   длительные действия в тайм-менеджере (готовка, уборка)
-   спортивные действия (пас/бросок)

------------------------------------------------------------------------

## 5.3 Модель Commit Groups

Proposal SHOULD иметь `CommitGroup`, например:

-   `Combat.Attack`
-   `Combat.Cast`
-   `Targeting.Lock`
-   `Movement.Path`
-   `Station.Cook`
-   `Economy.Transaction`

------------------------------------------------------------------------

## 5.4 Правило

1.  Если для `CommitGroup` активен lock:
    -   Arbiter MUST выбирать proposals только из этой группы, либо
        выбирать "cancel/interrupt" proposals, если они разрешены.
2.  Lock имеет:
    -   `CommitUntilTime`
    -   `InterruptTags` (например Stun, ForcedMove, PlayerCancel)
3.  При выборе нового proposal из группы Arbiter обновляет lock.

------------------------------------------------------------------------

## 5.5 Interrupt Policy

Interrupt proposals MUST иметь явный тег:

-   `Interrupt`
-   `Forced`
-   `Cancel`

И обрабатываться отдельным правилом: - Forced interrupt \> Player cancel
\> Threat interrupt \> normal

------------------------------------------------------------------------

# 6. Policy Recipe: Budget-Based Arbitration (Budgets)

## 6.1 Цель

Ограничить количество/стоимость действий по бюджету, чтобы:

-   удерживать производительность (spawn, FX, physics)
-   управлять темпом сложности (Survivors director)
-   контролировать экономику (spend/grant caps)
-   соблюдать лимиты LiveOps

------------------------------------------------------------------------

## 6.2 Когда применять

Особенно эффективно для:

-   Survivors-like Director (spawn budgets)
-   Loot генераторы (rarity budgets)
-   Economy (daily caps, sink/source limits)
-   VFX/Projectiles (perf budgets)

------------------------------------------------------------------------

## 6.3 Модель бюджета

Proposal SHOULD иметь `Cost` (одно или несколько измерений):

-   `SpawnCost`
-   `CPUCOST`
-   `FXCOST`
-   `EconomyCost`
-   `RiskCost`

Arbiter получает `BudgetSnapshot`:

-   `BudgetMax`
-   `BudgetUsed`
-   `BudgetRegenRate`
-   `BudgetWindow` (per second/per wave/per match)

------------------------------------------------------------------------

## 6.4 Правило

1.  Arbiter сортирует proposals по Score/priority.
2.  Arbiter добавляет proposal в Decision, если:
    -   `BudgetUsed + Cost <= BudgetMax`
3.  Arbiter продолжает, пока:
    -   бюджет не исчерпан, или proposals не закончились.
4.  Если proposal превышает бюджет, он отклоняется либо деградирует на
    cheaper variant.

------------------------------------------------------------------------

## 6.5 Degradation Strategy (Fallback)

Budget-based policy SHOULD иметь fallback:

-   заменить "elite spawn" на "normal spawn"
-   снизить count
-   выбрать ближайшую зону
-   отложить proposal (TTL) до следующего окна

------------------------------------------------------------------------

# 7. Композиция политик (Policy Composition)

Рекомендуемый порядок применения (pipeline):

1.  Hard Constraints (валидность, input locked, forced states)
2.  Player Override
3.  Commit Windows
4.  Threat Override
5.  Score-based ranking
6.  Budget-based selection
7.  Final tie-breakers (deterministic)

Каждый шаг MUST быть детерминированным при одинаковом input.

------------------------------------------------------------------------

# 8. Tie-breakers (Детерминированные разрешители конфликтов)

Чтобы избежать недетерминированности, SHOULD использовать:

-   сортировку по `Priority` (desc)
-   затем по `Score` (desc)
-   затем по `ProposalId` (asc) или stable hash
-   затем по `Source` (фиксированный порядок)

Запрещено использовать случайность без seeded RNG.

------------------------------------------------------------------------

# 9. Рекомендованные "пакеты" политик по жанрам

## 9.1 ARPG / Diablo-like

-   Player Override (MUST)
-   Commit Windows (Combat.Cast, Combat.Attack) (SHOULD)
-   Threat Override (SHOULD)
-   Tie-breakers deterministic (MUST)

------------------------------------------------------------------------

## 9.2 RTS / Warcraft-like

-   Player Override (MUST)
-   Commit Windows для Move/AttackMove (SHOULD)
-   Threat Override (опционально, если есть auto-defense)
-   Budget-based для массовых приказов (MAY)

------------------------------------------------------------------------

## 9.3 Turn-Based / Heroes-like

-   Scheduler turn-based (MUST)
-   Commit Windows = фиксация фазы (MUST)
-   Player Override внутри активного хода (MUST)
-   Threat Override заменяется фазовыми правилами (MAY)

------------------------------------------------------------------------

## 9.4 Survivors-like / Vampire Survivors

-   Threat Override почти всегда true (MAY)
-   Budget-based Director (MUST)
-   Commit Windows для эволюций/выбора апгрейда (MUST)
-   Player Override для MetaChoice (MUST)

------------------------------------------------------------------------

## 9.5 Economy / LiveOps

-   Budget-based по дневным/сессионным лимитам (MUST)
-   Commit Windows для транзакций (SHOULD)
-   Player Override для подтверждений (MUST)

------------------------------------------------------------------------

# 10. Требования соответствия (Compliance Requirements)

Политики арбитража MUST соответствовать:

-   Architecture Compliance Standard (Arbitration does not execute)
-   Architectural Fitness Functions (no forbidden dependencies)
-   Deterministic Mode rules (если включено)

------------------------------------------------------------------------

# 11. Приложение: Минимальный набор полей Proposal (MVP)

Минимально достаточный набор, рекомендованный для большинства игр:

-   ProposalId
-   Source
-   IntentType
-   Priority
-   Score
-   TTL
-   CommitGroup
-   Cost (если используется budgets)

------------------------------------------------------------------------

Документ предназначен для повторного использования между проектами.
