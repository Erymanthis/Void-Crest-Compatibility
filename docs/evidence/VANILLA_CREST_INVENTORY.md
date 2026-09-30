# Generated Silksong Crest-System Inventory

This file is generated from managed IL. It records structural evidence, not observed gameplay behavior.

- Assembly: `Assembly-CSharp` 0.0.0.0
- SHA-256: `70713BB2B1F1B0C4C6B7007FA6A8446CF79818CB5E308C48C094C4D1DE7114BB`
- Generated UTC: 2026-08-16T18:57:42.6306080+00:00

## Crest state structures

### `HeroController/HunterUpgCrestStateInfo`

- `System.Int32 CurrentMeterHits`

### `HeroController/ReaperCrestStateInfo`

- `System.Single ReaperModeDurationLeft`
- `System.Boolean IsInReaperMode`

### `HeroController/WandererCrestStateInfo`

- `System.Boolean QueuedNextHitCritical`
- `System.Boolean CriticalHitsLocked`

### `HeroController/WarriorCrestStateInfo`

- `System.Boolean IsInRageMode`
- `System.Single RageEffectTimeLeft`
- `System.Single RageHealTimeLeft`
- `System.Int32 RageModeHealCount`
- `System.Int32 LastHealAttack`

## HeroController.ConfigGroup fields

- `HeroControllerConfig Config`
- `UnityEngine.GameObject ActiveRoot`
- `UnityEngine.GameObject NormalSlashObject`
- `UnityEngine.GameObject AlternateSlashObject`
- `UnityEngine.GameObject UpSlashObject`
- `UnityEngine.GameObject AltUpSlashObject`
- `UnityEngine.GameObject DownSlashObject`
- `UnityEngine.GameObject AltDownSlashObject`
- `UnityEngine.GameObject WallSlashObject`
- `UnityEngine.GameObject DashStab`
- `UnityEngine.GameObject DashStabAlt`
- `UnityEngine.GameObject ChargeSlash`
- `UnityEngine.GameObject TauntSlash`

## Crest-dependent managed call sites

| Method | Crest getters | `IsEquipped` calls | State references |
|---|---|---:|---|
| `HeroController/HunterUpgCrestStateInfo HeroController::get_HunterUpgState()` |  | 0 | HeroController/HunterUpgCrestStateInfo HeroController::hunterUpgState |
| `HeroController/ReaperCrestStateInfo HeroController::get_ReaperState()` |  | 0 | HeroController/ReaperCrestStateInfo HeroController::reaperState |
| `HeroController/WandererCrestStateInfo HeroController::get_WandererState()` |  | 0 | HeroController/WandererCrestStateInfo HeroController::<WandererState>k__BackingField |
| `HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::warriorState |
| `IHitResponder/HitResponse BouncePod::Hit(HitInstance)` | WarriorCrest | 1 |  |
| `System.Boolean BindOrbHudFrame::DoChangeFrame(System.Boolean,System.Boolean)` | CloaklessCrest, CursedCrest, HunterCrest, HunterCrest2, HunterCrest3, ReaperCrest, SpellCrest, ToolmasterCrest, WandererCrest, WarriorCrest, WitchCrest | 11 |  |
| `System.Boolean BindOrbHudFrame/<FrameAppear>d__69::MoveNext()` | CursedCrest | 1 |  |
| `System.Boolean BindOrbHudFrame/<HunterCrestUpgradedRoutine>d__79::MoveNext()` |  | 0 | HeroController/HunterUpgCrestStateInfo HeroController::get_HunterUpgState()<br>System.Int32 HeroController/HunterUpgCrestStateInfo::CurrentMeterHits |
| `System.Boolean BindOrbHudFrame/<ReaperCrestRoutine>d__74::MoveNext()` |  | 0 | HeroController/ReaperCrestStateInfo HeroController::get_ReaperState()<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode |
| `System.Boolean BindOrbHudFrame/<WarriorCrestRoutine>d__73::MoveNext()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Single HeroController/WarriorCrestStateInfo::RageEffectTimeLeft |
| `System.Boolean BouncePod::WillRespond(HitInstance)` | WarriorCrest | 1 |  |
| `System.Boolean DamageEnemies::DoDamage(UnityEngine.GameObject,System.Boolean)` | HunterCrest2, HunterCrest3 | 2 | HeroController/HunterUpgCrestStateInfo HeroController::get_HunterUpgState()<br>HeroController/WandererCrestStateInfo HeroController::get_WandererState()<br>HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/HunterUpgCrestStateInfo::get_IsComboMeterAboveExtra()<br>System.Boolean HeroController/WandererCrestStateInfo::CriticalHitsLocked<br>System.Boolean HeroController/WandererCrestStateInfo::QueuedNextHitCritical<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode<br>System.Int32 HeroController/HunterUpgCrestStateInfo::CurrentMeterHits |
| `System.Boolean HeroAnimationController::IsInRage()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Boolean HeroController::get_IsWandererLucky()` | WandererCrest | 1 |  |
| `System.Boolean HeroController::IsHealthCritical()` |  | 1 |  |
| `System.Boolean HeroController/<Die>d__1101::MoveNext()` |  | 1 |  |
| `System.Boolean HeroController/<StartRecoil>d__1105::MoveNext()` |  | 1 |  |
| `System.Boolean HeroController/HunterUpgCrestStateInfo::get_IsComboMeterAboveExtra()` |  | 0 | System.Int32 HeroController/HunterUpgCrestStateInfo::CurrentMeterHits |
| `System.Boolean HeroPerformanceRegion::IsInRange(UnityEngine.Vector2,UnityEngine.Vector2,UnityEngine.Vector2)` |  | 1 |  |
| `System.Boolean HutongGames.PlayMaker.Actions.CheckIfCrestEquipped::get_IsTrue()` |  | 1 |  |
| `System.Boolean HutongGames.PlayMaker.Actions.CheckIfToolEquipped::get_IsTrue()` |  | 1 |  |
| `System.Boolean InventoryItemToolManager::get_IsHeroCursed()` | CursedCrest | 1 |  |
| `System.Boolean NailSlashTravel/<Travel>d__51::MoveNext()` |  | 1 |  |
| `System.Boolean PlayerData::get_IsAnyCursed()` | CursedCrest | 0 |  |
| `System.Boolean ToolCrest::get_IsVisible()` |  | 1 |  |
| `System.Int32 HealthSpecialHealIndicator::get_CurrentHealCap()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Int32 HeroController/WarriorCrestStateInfo::RageModeHealCount |
| `System.Int32 HeroController::GetRageModeHealCap()` |  | 1 |  |
| `System.Int32 HeroController::GetReaperPayout()` |  | 0 | HeroController/ReaperCrestStateInfo HeroController::reaperState<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode |
| `System.Int32 PlayerData::get_CurrentSilkMax()` |  | 1 |  |
| `System.Int32 PlayerData::get_CurrentSilkRegenMax()` |  | 1 |  |
| `System.Single HeroController::get_CurrentNailChargeBeginTime()` |  | 1 |  |
| `System.Single HeroController::get_CurrentNailChargeTime()` |  | 1 |  |
| `System.Single HeroController::GetTotalFrostSpeed()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Single HeroControllerConfigWarrior::get_AttackCooldownTime()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Single HeroControllerConfigWarrior::get_AttackDuration()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Single HeroControllerConfigWarrior::get_AttackRecoveryTime()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Single HeroControllerConfigWarrior::get_QuickAttackCooldownTime()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Single SilkSpool::get_BindCost()` | WitchCrest | 1 |  |
| `System.Void ActiveCorpse::DoQueuedBurnEffects()` | SpellCrest | 1 |  |
| `System.Void ClockworkHatchling::RecordPoisonStatus()` |  | 1 |  |
| `System.Void ClockworkHatchlingDummy::CheckPoison()` |  | 1 |  |
| `System.Void DamageEnemies::ProcessDamageBuffer()` |  | 0 | HeroController/WandererCrestStateInfo HeroController::get_WandererState()<br>System.Boolean HeroController/WandererCrestStateInfo::QueuedNextHitCritical<br>System.Void HeroController::set_WandererState(HeroController/WandererCrestStateInfo) |
| `System.Void GameManager::StartAct3()` | HunterCrest, HunterCrest2, HunterCrest3 | 0 |  |
| `System.Void GameMap::PositionCompassAndCorpse()` |  | 1 |  |
| `System.Void HealthManager::Die(System.Nullable`1<System.Single>,AttackTypes,NailElements,UnityEngine.GameObject,System.Boolean,System.Single,System.Boolean,System.Boolean)` |  | 2 |  |
| `System.Void HealthManager::DoStealHit()` |  | 1 |  |
| `System.Void HealthManager::TakeDamage(HitInstance)` | HunterCrest3 | 1 | HeroController/HunterUpgCrestStateInfo HeroController::get_HunterUpgState()<br>HeroController/ReaperCrestStateInfo HeroController::get_ReaperState()<br>System.Boolean HeroController/HunterUpgCrestStateInfo::get_IsComboMeterAboveExtra()<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode |
| `System.Void HeroAnimationController::UpdateToolEquipFlags()` | CursedCrest | 1 |  |
| `System.Void HeroController::ActivateQuickening()` |  | 1 |  |
| `System.Void HeroController::AddSilk(System.Int32,System.Boolean,SilkSpool/SilkAddSource,System.Boolean)` | WitchCrest | 1 |  |
| `System.Void HeroController::Attack(GlobalEnums.AttackDirection)` | ToolmasterCrest | 2 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Void HeroController::BindCompleted()` | ReaperCrest, WarriorCrest | 2 | HeroController/ReaperCrestStateInfo HeroController::reaperState<br>HeroController/WarriorCrestStateInfo<br>HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode<br>System.Single HeroController/ReaperCrestStateInfo::ReaperModeDurationLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageEffectTimeLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageHealTimeLeft |
| `System.Void HeroController::DoDoubleJump()` |  | 1 |  |
| `System.Void HeroController::DoSpecialDamage(System.Int32,System.Boolean,System.String,System.Boolean,System.Boolean,System.Boolean,System.Boolean)` |  | 1 |  |
| `System.Void HeroController::FallCheck()` | SpellCrest | 1 |  |
| `System.Void HeroController::HeroDashPressed()` |  | 1 |  |
| `System.Void HeroController::NailHitEnemy(HealthManager,HitInstance)` | HunterCrest2, HunterCrest3 | 2 | HeroController/HunterUpgCrestStateInfo HeroController::hunterUpgState<br>HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Int32 HeroController/HunterUpgCrestStateInfo::CurrentMeterHits<br>System.Int32 HeroController/WarriorCrestStateInfo::LastHealAttack<br>System.Int32 HeroController/WarriorCrestStateInfo::RageModeHealCount<br>System.Single HeroController/WarriorCrestStateInfo::RageEffectTimeLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageHealTimeLeft |
| `System.Void HeroController::Recoil(System.Boolean,System.Boolean)` | CursedCrest, WitchCrest, WitchCrestRecoilSteps | 3 |  |
| `System.Void HeroController::RecoilDown()` | CursedCrest, WitchCrest | 6 |  |
| `System.Void HeroController::ResetHunterUpgCrestState()` |  | 0 | HeroController/HunterUpgCrestStateInfo<br>HeroController/HunterUpgCrestStateInfo HeroController::hunterUpgState |
| `System.Void HeroController::ResetReaperCrestState()` |  | 0 | HeroController/ReaperCrestStateInfo<br>HeroController/ReaperCrestStateInfo HeroController::reaperState<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode |
| `System.Void HeroController::ResetSilkRegen()` |  | 1 |  |
| `System.Void HeroController::ResetWandererCrestState()` |  | 0 | HeroController/WandererCrestStateInfo<br>System.Void HeroController::set_WandererState(HeroController/WandererCrestStateInfo) |
| `System.Void HeroController::ResetWarriorCrestState()` |  | 0 | HeroController/WarriorCrestStateInfo<br>HeroController/WarriorCrestStateInfo HeroController::warriorState |
| `System.Void HeroController::ResetWarriorRageEffect()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Void HeroController::set_WandererState(HeroController/WandererCrestStateInfo)` |  | 0 | HeroController/WandererCrestStateInfo HeroController::<WandererState>k__BackingField |
| `System.Void HeroController::StartDashEffect()` |  | 1 |  |
| `System.Void HeroController::StartSilkRegen()` |  | 1 |  |
| `System.Void HeroController::TakeDamage(UnityEngine.GameObject,GlobalEnums.CollisionSide,System.Int32,GlobalEnums.HazardType,GlobalEnums.DamagePropertyFlags)` |  | 3 | HeroController/ReaperCrestStateInfo HeroController::reaperState<br>HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode<br>System.Single HeroController/WarriorCrestStateInfo::RageEffectTimeLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageHealTimeLeft |
| `System.Void HeroController::Update()` |  | 0 | HeroController/ReaperCrestStateInfo HeroController::reaperState<br>HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/ReaperCrestStateInfo::IsInReaperMode<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode<br>System.Single HeroController/ReaperCrestStateInfo::ReaperModeDurationLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageEffectTimeLeft<br>System.Single HeroController/WarriorCrestStateInfo::RageHealTimeLeft |
| `System.Void HeroController::UpdateConfig()` | WarriorCrest | 0 | HeroController/WarriorCrestStateInfo HeroController::warriorState<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Void HeroDamageSpawn::OnTakenDamage(HeroController/DamageInfo)` |  | 1 |  |
| `System.Void HeroPerformanceRegion::SetIsPerforming(System.Boolean)` |  | 1 |  |
| `System.Void HeroShamanRuneEffect::Refresh()` | SpellCrest, SpellCrestRuneDamageMult | 2 |  |
| `System.Void HeroSlabCapture::ApplyCaptured()` | CloaklessCrest | 0 |  |
| `System.Void HeroWispLantern::OnEquipsChanged()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.EnemySingControl::OnEnter()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.GetAttunementLevel::GetLevel()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.GetAttunementTime::GetTime()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.GetToolEquipInfo::DoAction()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.HeroGetWarriorCrestState::OnEnter()` |  | 0 | HeroController/WarriorCrestStateInfo HeroController::get_WarriorState()<br>System.Boolean HeroController/WarriorCrestStateInfo::IsInRageMode |
| `System.Void HutongGames.PlayMaker.Actions.HeroSetWandererCrestState::OnEnter()` |  | 0 | HeroController/WandererCrestStateInfo HeroController::get_WandererState()<br>System.Boolean HeroController/WandererCrestStateInfo::CriticalHitsLocked<br>System.Boolean HeroController/WandererCrestStateInfo::QueuedNextHitCritical<br>System.Void HeroController::set_WandererState(HeroController/WandererCrestStateInfo) |
| `System.Void HutongGames.PlayMaker.Actions.ScreenFlashLifeblood::OnEnter()` |  | 1 |  |
| `System.Void HutongGames.PlayMaker.Actions.SetHeroMaggoted::OnEnter()` |  | 1 |  |
| `System.Void InventoryFloatingToolSlots::Evaluate()` | CursedCrest | 1 |  |
| `System.Void InventoryItemToolManager::SetDisplay(InventoryItemSelectable)` |  | 1 |  |
| `System.Void InventoryWideMap::UpdatePositions()` |  | 1 |  |
| `System.Void MaggotRegion::OnHeroEnteredWater(HeroController)` |  | 1 |  |
| `System.Void NailAttackBase::OnSlashStarting()` |  | 1 |  |
| `System.Void PlayerData::TakeHealth(System.Int32,System.Boolean,System.Boolean)` |  | 1 |  |
| `System.Void PoisonPouchEventCaller::OnEnable()` |  | 1 |  |
| `System.Void PoisonTintBase::UpdatePoison()` |  | 1 |  |
| `System.Void RunEffects::UpdateEffects()` |  | 1 |  |
| `System.Void SimpleProjectile::OnEnable()` |  | 1 |  |
| `System.Void SurfaceWaterRegion::OnTriggerEnter2D(UnityEngine.Collider2D)` | SpellCrest | 1 |  |
| `System.Void TestGameObjectActivator::Evaluate()` |  | 1 |  |
| `System.Void ToolBoomerang::CheckPoison()` |  | 1 |  |
| `System.Void ToolEquipChecker::OnToolEquipsChanged()` |  | 1 |  |
| `System.Void ToolItemLimiter::ReportToolUsed(ToolItem)` |  | 1 |  |
| `System.Void ToolItemManager::RefreshEquippedState()` |  | 1 |  |
| `System.Void ToolItemManager::SetEquippedCrest(System.String)` |  | 1 |  |
| `System.Void ToolPin::CheckPoison()` |  | 1 |  |
| `System.Void ToolRing::CheckPoison()` |  | 1 |  |
| `System.Void WeaverSpeedPanel::RecordSpeed()` |  | 1 |  |
