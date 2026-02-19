# Morboo Architecture Layers Reference
_Версия: 1.0 (reference)_  
_Цель: единый “север” для ИИ/разработчиков при добавлении новых систем и миграциях. Документ описывает **слои**, их **ответственности**, **границы зависимостей**, **запреты**, и **шаблон раскладки файлов** для любой Game System (Orchestration/Economy/Goals/Generation и т.п.)._

---

## 0) Ключевой принцип

Мы строим систему как набор **переносимых слоёв**, где:
- верхние слои более универсальны и **не знают** о конкретной игре;
- нижние слои адаптируют универсальное к **типу проекта** и к **конкретному проекту**;
- зависимости **строго однонаправленные** (компилятор должен ловить нарушения через asmdef).

---

## 1) Слои и их назначение

Ниже — слои, о которых мы договорились:

### 1.1 Morboo.Framework
**Назначение:** универсальные инварианты архитектуры, не зависящие от Unity и конкретных систем.  
**Уровень абстракции:** максимально общий.

**Типичные содержимое:**
- value-types и базовая математика (`Float2`, `AABB2D`, и т.п.)
- идентичность и идентификаторы (`EntityId`, `ContentId`)
- snapshot/query контракты (`IWorldState`, `IWorldQuery`, `WorldSnapshot`)
- decision/arbiter контракты (`Proposal`, `Decision`, `IProposalSource`, `IArbiter`)
- command/event абстракции (`ICommand`, `IDomainEvent`, `ICommandBus`, `IEventBus`)
- scheduling контракты (если есть) — только **универсальная часть**

**MUST:**
- `noEngineReferences: true`
- нет `using UnityEngine`
- нет ссылок на Morboo.* системы/домены

**MUST NOT:**
- Unity types (`Transform`, `MonoBehaviour`, `ScriptableObject`, `Vector2`, `Bounds`)
- доменные payload’ы конкретных систем (например `CombatCommand`, `IdleCommand`)

---

### 1.2 Morboo.Systems.Runtime
**Назначение:** общая runtime-инфраструктура исполнения систем в Unity, которая **не специфична** для конкретной системы.  
**Уровень абстракции:** “над системами”, но Unity-разрешён.

**Примеры того, что должно жить здесь:**
- tick/loop инфраструктура: realtime scheduler, tick source implementation
- общие runtime-шины/механизмы: in-process bus реализации (если они реально общие)
- общие Unity-bridge утилиты (конвертеры типов Framework ↔ Unity)
- общие diagnostics/hooks для архитектурных тестов runtime уровня (если применимо)

**MUST:**
- не знать о конкретных доменах (Combat/Idle/Goals/etc)
- не содержать игровые типы проекта

**MAY:**
- ссылаться на `Morboo.Framework`
- использовать `UnityEngine`

**MUST NOT:**
- ссылаться на `Morboo.Core` / `Morboo.RuntimeHost` конкретных систем напрямую **если** это превращает Systems.Runtime в “свалку orchestration-кода”.
  - Systems.Runtime — инфраструктура, а не место, куда “всё удобное” складывается.

---

### 1.3 Morboo.Core
**Назначение:** core-части всех систем (переносимые между проектами одного семейства), **без** конкретики доменов “типа игры”.  
**Уровень абстракции:** system-core (контракты/данные системы), но всё ещё универсально для разных project types.

**Типичные содержимое:**
- system-level контракты и payload types, которые **не являются** реализацией конкретных доменов проекта  
  (пример: `Orchestration` как “движок принятия решений”, но не `CombatOrchestratorLite`)
- типы команд/интентов системы, если они engine-agnostic (на `EntityId`, `Float2` и т.п.)
- system-core SO (только если это чистые данные, без сценовых ссылок)

**MUST:**
- быть переносимым между проектами (в рамках студийного семейства)
- зависеть от `Morboo.Framework`

**MUST NOT:**
- содержать “project-type домены” (например `CombatOrchestratorLite`, `IdleOrchestratorLite`, конкретные политики боёвки/айла)
- содержать Unity-сценовые механики и интеграцию (`GetComponent`, `MonoBehaviour`-оркестрация, доступ к конкретной игре)

---

### 1.4 Morboo.RuntimeHost
**Назначение:** host-части всех систем — конкретная реализация системного runtime-ядра (арбитраж, world cache/snapshot building, маршрутизация решений), но всё ещё **не завязана** на конкретный проект.  
**Уровень абстракции:** системный рантайм (host), Unity-разрешён.

**Типичные содержимое:**
- system loop / orchestration loop (если это “host системы”, а не общий scheduler)
- сбор world cache/snapshots, построение `IWorldQuery`
- decision loop реализация в терминах Framework (Snapshot → Proposals → Arbiter → Decision → ExecutionRouting)
- policy map pulling (НО без конкретных политик проекта)
- registry/lookup, если это “host инфраструктура системы” (и доступ к ним строго контролируется)

**MUST:**
- зависеть от `Morboo.Framework` и `Morboo.Core`
- не содержать project-specific Unity adapters/executors
- держать world доступ доменам только через `IWorldQuery`/snapshot

**MUST NOT:**
- содержать конкретные домены “типа проекта” (combat/idle как реализация StrategyCombat)
- содержать игровые типы (Unit/Enemy/TopDownEngine и т.д.)
- напрямую “исполнять” проектные executors (после отделения routing/bus)

---

### 1.5 Morboo.Integration.StrategyCombat
**Назначение:** интеграция уровня **типа проекта** (в данном случае StrategyCombat). Тут живут домены/политики/модели поведения, которые характерны для данного жанра/типа игры, но всё ещё не про конкретный проект.  
**Уровень абстракции:** project-type layer.

**Примеры содержимого:**
- доменные реализации, характерные для StrategyCombat:
  - `CombatOrchestrator*`, `IdleOrchestrator*`
  - боевые/айдл политики и их карты
  - формации (Grid/Ring) — если они действительно про StrategyCombat, а не универсальные мат.паттерны
  - movement constraints для боёвки и т.п.
- любые “сценарии поведения” уровня типа игры

**MUST:**
- зависеть от `Morboo.Framework`, `Morboo.Core`, `Morboo.RuntimeHost`
- оставаться независимым от конкретной игры (нет конкретных UnitData/конкретных prefabs/конкретных систем проекта)

**MUST NOT:**
- зависеть от `Assets/Scripts` конкретного проекта
- содержать “bridge” к конкретным компонентам проекта (это следующий слой)

---

### 1.6 MorbooBridge (Assets/Scripts/MorbooBridge)
**Назначение:** bridge к **конкретному проекту** (конкретная игра). Это “Integration.Project”.  
**Уровень абстракции:** project layer.

**Типичные содержимое:**
- адаптеры к юнитам/врагам/данным проекта
- конкретные `MonoBehaviour`-связки
- конкретные SO/мапы проекта (например маппинг UnitClass → RoleAsset)
- любые “glue” компоненты, которые знают про ассеты/данные/структуру проекта

**MUST:**
- находиться в `Assets/Scripts/MorbooBridge` (как договорились)
- иметь свой asmdef: `Morboo.Bridge.asmdef`
- быть единственным местом, где встречаются ссылки на:
  - конкретные игровые типы и данные проекта
  - конкретные сцены/префабы
  - конкретные “Game.Runtime” типы

**MUST NOT:**
- содержать переносимую логику системы (если это можно вынести в StrategyCombat или RuntimeHost — выносить)

---

## 2) Направление зависимостей (dependency chain)

Разрешённая зависимость (сверху вниз):

`Morboo.Framework`
→ `Morboo.Systems.Runtime` (может зависеть от Framework)  
→ `Morboo.Core` (зависит от Framework)  
→ `Morboo.RuntimeHost` (зависит от Framework + Core + возможно Systems.Runtime)  
→ `Morboo.Integration.StrategyCombat` (зависит от Framework + Core + RuntimeHost)  
→ `MorbooBridge` (зависит от всего выше + Game.Runtime)

**Запрещено:**
- обратные зависимости (например RuntimeHost → MorbooBridge)
- Framework → любые Morboo.* системы
- Core/RuntimeHost → project-specific типы/ассеты

---

## 3) Правила “что куда класть” (быстрый фильтр)

Если компонент/класс…

### 3.1 Это универсальный инвариант, который может жить в любой игре/системе
→ `Morboo.Framework`

### 3.2 Это Unity runtime-инфраструктура, полезная сразу многим системам
→ `Morboo.Systems.Runtime`

### 3.3 Это часть ядра конкретной системы, но без жанровой/проектной конкретики
→ `Morboo.Core`

### 3.4 Это host-исполнение системы: snapshot, arbiter loop, routing (но без project-type доменов)
→ `Morboo.RuntimeHost`

### 3.5 Это реализация поведения/домена уровня “типа проекта” (StrategyCombat)
→ `Morboo.Integration.StrategyCombat`

### 3.6 Это glue к текущему проекту, его данным и типам
→ `Assets/Scripts/MorbooBridge` (+ `Morboo.Bridge.asmdef`)

---

## 4) Template структуры для добавляемой системы

Ниже шаблон для любой новой Game System `XSystem` (Economy/Goals/Generation/…).

### 4.1 Morboo.Framework (если нужно)
- Добавлять только если появляются **новые инварианты**, полезные не только XSystem.
- Иначе не трогать.

Пример:
Packages/com.morboo.framework/Runtime/
Math/
Identity/
State/
Decision/
Execution/
Scheduling/

### 4.2 Morboo.Systems.Runtime
Packages/com.morboo.systems/Runtime/
Scheduling/               # tick sources, realtime loops, timer services
Bus/                      # общие bus реализации, если реально cross-system
Diagnostics/              # runtime invariants / probes
UnityBridge/              # конверсии типов Framework <-> Unity

### 4.3 Morboo.Core (System Core)
Packages/com.morboo.core/Runtime/XSystem/
Contracts/                # интерфейсы системы, публичные типы
Data/                     # engine-agnostic data types
Commands/                 # ICommand payloads (engine-agnostic)
Events/                   # IDomainEvent payloads (engine-agnostic)

### 4.4 Morboo.RuntimeHost (System Host)
Packages/com.morboo.runtimehost/Runtime/XSystem/
Host/                     # loop, snapshot building, router emitting
World/                    # world cache/snapshot builder, query impl
Arbitration/              # proposal sources, arbiters, decision policies
Routing/                  # decision routing (желательно через bus seam)
Maps/                     # host-level maps pulling / resolution
Diagnostics/              # asserts, invariants

### 4.5 Morboo.Integration.StrategyCombat (ProjectType)
Packages/com.morboo.integration.strategycombat/Runtime/XSystem/
Domains/                  # project-type домены
Policies/                 # project-type политики и их SO
Content/                  # project-type content assets (если переносимые)
Utilities/                # helpers specific to StrategyCombat

### 4.6 MorbooBridge (Project)
Assets/Scripts/MorbooBridge/XSystem/
Adapters/                 # подписки на bus, конверсия EntityId -> Unity objects
Data/                     # project-specific maps (UnitClass->Role, etc.)
MonoBehaviours/           # сцено-зависимая wiring логика
PrefabGlue/               # компоненты на префабах, которые “подключают” систему

---

## 5) Архитектурные инварианты (checklist)

Этот список стоит прикладывать к каждому плану/PR.

### 5.1 Framework
- [ ] `noEngineReferences: true`
- [ ] 0 `using UnityEngine`
- [ ] 0 упоминаний Transform/MonoBehaviour/ScriptableObject
- [ ] нет ссылок на Morboo.* system payloads

### 5.2 Systems.Runtime
- [ ] не содержит доменных реализаций конкретной системы
- [ ] не зависит от MorbooBridge/проектных ассетов
- [ ] публичные API — общие, не “про orchestration”

### 5.3 Core / RuntimeHost
- [ ] Core не содержит project-type доменов (Combat/Idle и т.п.)
- [ ] RuntimeHost не знает про Unit/Enemy/Game.Runtime
- [ ] Domain/Policy читают мир только через `IWorldQuery`/snapshot
- [ ] никакого `GetComponent`/scene wiring в Core
- [ ] никакого прямого доступа доменов к Registry (только через world/snapshot)

### 5.4 Integration.StrategyCombat
- [ ] не зависит от конкретного проекта
- [ ] нет ссылок на `Assets/Scripts/...` типов проекта

### 5.5 MorbooBridge (project)
- [ ] содержит все project-specific glue
- [ ] единственное место, где есть EntityId→Transform/Component resolution
- [ ] asmdef: `Morboo.Bridge.asmdef` и зависимости направлены вниз по цепочке

---

## 6) Примечание про домены Combat/Idle и формации

**Правило:** если домен/политика/формация — это “тип игры” (StrategyCombat), то это **не Core**.  
Они должны жить в `Morboo.Integration.StrategyCombat`.

Core должен содержать только:
- переносимые контракты/данные системы,
- универсальные механизмы принятия решений и маршрутизации,
- но не “конкретный набор доменов данного жанра”.

---

## 7) Как этим пользоваться при постановке задач ИИ

При любой задаче “добавить фичу / систему / часть системы”:
1) Сначала определить слой(и) по правилу из §3  
2) В плане явно указать:
   - какие файлы добавляются/перемещаются,
   - какие asmdef зависимости нужны,
   - какие архитектурные тесты/grep проверки должны ловить регрессии.
3) Любое отклонение от слоёв фиксировать как **архитектурный риск** и не делать “временно”.

---