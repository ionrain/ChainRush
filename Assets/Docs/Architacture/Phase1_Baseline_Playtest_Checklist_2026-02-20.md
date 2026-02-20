# Phase 1 Baseline Playtest Checklist

Date: 2026-02-20  
Phase: `Phase 1 — Guardrails Baseline`  
Owner: `Experience & Bridge Owner` + `Gameplay Domains Owner`
Status: `Closed (owner sign-off)`

## Goal

Зафиксировать baseline-ожидаемое поведение перед структурными миграциями (`C02+`), чтобы отлавливать архитектурные регрессии.

## Scene / Setup

1. Main scene: `Assets/Game/Scenes/Main.unity`
2. Test scope: orchestration + combat/idle core loop + level flow smoke

## Checklist

1. [x] Units keep attraction/formation behavior around anchor/hero.
2. [x] Enemy targeting and attacks behave as expected.
3. [x] Level win/lose transitions are correct.
4. [x] No missing scripts on key unit/enemy prefabs.
5. [x] Orchestration loop ticks and dispatch pipeline remains stable.

## Baseline Status (2026-02-20)

1. Recorded as baseline checklist artifact for Phase 1.
2. Manual run status: `Executed / accepted by owner`.
3. Exit decision: `Phase 1 can be considered closed; transition to Phase 2 is approved`.
