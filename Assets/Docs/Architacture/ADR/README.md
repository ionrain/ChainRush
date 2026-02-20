# Architecture Decision Records (ADR)

Purpose: фиксировать архитектурные решения, которые меняют границы слоёв, зависимости, owner/source-of-truth или допускают временные отклонения.

## Naming

1. Формат файла: `ADR-XXXX-short-title.md`
2. Нумерация монотонная (`0001`, `0002`, ...).
3. Один ADR = одно ключевое решение.

## Status Lifecycle

1. `Proposed`
2. `Accepted`
3. `Superseded` (с ссылкой на новый ADR)
4. `Rejected`

## Required When

1. Вводится новое семейство пакетов/уровень.
2. Нарушается стандартное направление зависимостей.
3. Меняется владелец source-of-truth состояния.
4. Нужен временный bypass правила (`architecture-first`, layering, typed refs, data-driven-first).
5. Превышен file-sprawl budget и нужен исключительный путь.

## Authoring Workflow

1. Скопировать `ADR_Template.md` в новый `ADR-XXXX-...md`.
2. Заполнить все секции и указать `Target cleanup phase`, если решение временное.
3. Добавить ссылку на ADR в PR (обязательно при исключениях).
4. Обновить/добавить fitness tests под принятое решение.
