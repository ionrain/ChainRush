# Phase 1 Baseline Playtest Checklist

Date: 2026-02-20  
Phase: `Phase 1 — Guardrails Baseline`  
Owner: `Experience & Bridge Owner` + `Gameplay Domains Owner`

## Goal

Зафиксировать baseline-ожидаемое поведение перед структурными миграциями (`C02+`), чтобы отлавливать архитектурные регрессии.

## Scene / Setup

1. Main scene: `Assets/Game/Scenes/Main.unity`
2. Test scope: orchestration + combat/idle core loop + level flow smoke

## Checklist

1. Units keep attraction/formation behavior around anchor/hero.
2. Enemy targeting and attacks behave as expected.
3. Level win/lose transitions are correct.
4. No missing scripts on key unit/enemy prefabs.
5. Orchestration loop ticks and dispatch pipeline remains stable.

## Baseline Status (2026-02-20)

1. Recorded as baseline checklist artifact for Phase 1.
2. Manual run status in this Codex session: `Not executed` (Unity scene playtest required).
