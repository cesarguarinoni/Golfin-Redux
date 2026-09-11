# Architecture Audit

> Auto-generated 2026-09-11 11:04. Do not edit manually.

## File Tree (Scripts)

```
Assets/Scripts/Audio/AudioManager.cs
Assets/Scripts/Audio/Events/ISfxGates.cs
Assets/Scripts/Audio/Events/SfxBus.cs
Assets/Scripts/Audio/Events/SfxId.cs
Assets/Scripts/Audio/SfxBusReset.cs
Assets/Scripts/Audio/SfxLibrary.cs
Assets/Scripts/Audio/SfxPlayer.cs
Assets/Scripts/Auth/AppDeepLink.cs
Assets/Scripts/Auth/AuthFlowState.cs
Assets/Scripts/Auth/AuthModels.cs
Assets/Scripts/Auth/AuthRedirectUrl.cs
Assets/Scripts/Auth/AuthService.cs
Assets/Scripts/Auth/AuthSession.cs
Assets/Scripts/Auth/ISupabaseAuthClient.cs
Assets/Scripts/Auth/MockSupabaseAuthClient.cs
Assets/Scripts/Auth/OAuthCallbackParser.cs
Assets/Scripts/Auth/OAuthUrlBuilder.cs
Assets/Scripts/Auth/PlayerIdentity.cs
Assets/Scripts/Auth/SupabaseAuthClient.cs
Assets/Scripts/Auth/SupabaseConfig.cs
Assets/Scripts/Auth/Tests/AppDeepLinkTests.cs
Assets/Scripts/Auth/Tests/AuthTests.cs
Assets/Scripts/Auth/Tests/OAuthTests.cs
Assets/Scripts/Auth/Tests/RecoveryFlowTests.cs
Assets/Scripts/BagDatabaseCSV.cs
Assets/Scripts/BagManager.cs
Assets/Scripts/BallManager.cs
Assets/Scripts/BannersRuntime/BannerPolicy.cs
Assets/Scripts/BannersRuntime/BannerService.cs
Assets/Scripts/BannersRuntime/BannerSlotBinder.cs
Assets/Scripts/BannersRuntime/RemoteBannerDtos.cs
Assets/Scripts/BannersRuntime/RemoteBannerSource.cs
Assets/Scripts/CatalogArt/CatalogArt.cs
Assets/Scripts/CatalogArt/CatalogArtPolicy.cs
Assets/Scripts/CharacterManager.cs
Assets/Scripts/ClubManager.cs
Assets/Scripts/ContentRuntime/ContentBuildNumber.cs
Assets/Scripts/ContentRuntime/ContentCatalogMapper.cs
Assets/Scripts/ContentRuntime/ContentCatalogStore.cs
Assets/Scripts/ContentRuntime/ContentCatalogs.cs
Assets/Scripts/ContentRuntime/ContentClamp.cs
Assets/Scripts/ContentRuntime/ContentRow.cs
Assets/Scripts/ContentRuntime/ContentService.cs
Assets/Scripts/ContentRuntime/ContentShopWindow.cs
Assets/Scripts/ContentRuntime/ContentSpriteGuard.cs
Assets/Scripts/ContentRuntime/ContentTextsMapper.cs
Assets/Scripts/ContentRuntime/ContentVersionFile.cs
Assets/Scripts/ContentRuntime/RemoteContentDtos.cs
Assets/Scripts/ContentRuntime/RemoteContentSource.cs
Assets/Scripts/ContentRuntime/ScheduleRefreshThrottle.cs
Assets/Scripts/ContentRuntime/Tests/ContentBuildNumberTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentCatalogMapperTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentCatalogStoreTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentClampTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentEndpointTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentFetchPathTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentPerCatalogKillTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentShopWindowTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentTextsMapperTests.cs
Assets/Scripts/ContentRuntime/Tests/ContentVersionFileTests.cs
Assets/Scripts/ContentRuntime/Tests/LocalizationOverlayTests.cs
Assets/Scripts/ContentRuntime/Tests/RemoteContentSourceTests.cs
Assets/Scripts/Core/FramePacingBootstrap.cs
Assets/Scripts/Core/Stamina/StaminaConfig.cs
Assets/Scripts/Core/Stamina/StaminaConfigLoader.cs
Assets/Scripts/Core/Stamina/StaminaModel.cs
Assets/Scripts/Core/Stamina/Tests/LiveDisplayEnergyTests.cs
Assets/Scripts/Core/Stamina/Tests/StaminaModelTests.cs
Assets/Scripts/Course/BridgeAnchor.cs
Assets/Scripts/Course/BunkerSurfaceInfo.cs
Assets/Scripts/Course/GreenSurfaceInfo.cs
Assets/Scripts/Course/Runtime/CourseSlugResolver.cs
Assets/Scripts/Course/Runtime/GreenTopology.cs
Assets/Scripts/Course/Runtime/GreenTopologyCache.cs
Assets/Scripts/Course/Runtime/HoleTeesCsvParser.cs
Assets/Scripts/Course/Runtime/TeeData.cs
Assets/Scripts/Course/SurfaceMarker.cs
Assets/Scripts/Course/Tests/ActiveCourseContextTests.cs
Assets/Scripts/Course/Tests/CourseSlugResolverTests.cs
Assets/Scripts/Course/Tests/GreenTopologyTests.cs
Assets/Scripts/Course/Tests/TeeDataTests.cs
Assets/Scripts/Debug/RewardPointsDebugPanel.cs
Assets/Scripts/Debug/ScreenshotCapture/ScreenshotHelper.cs
Assets/Scripts/Debug/WalkCamera.cs
Assets/Scripts/Demo/DemoConfig.cs
Assets/Scripts/Demo/DemoGate.cs
Assets/Scripts/Dev/BotSessionOverride.cs
Assets/Scripts/Dev/DevFpsOverlay.cs
Assets/Scripts/Dev/PerfBaselineBot.cs
Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs
Assets/Scripts/Economy/Editor/PointsBackendMenu.cs
Assets/Scripts/Economy/GachaPullOutcome.cs
Assets/Scripts/Economy/GachaPullService.cs
Assets/Scripts/Economy/MissionsClient.cs
Assets/Scripts/Economy/PendingOpsQueue.cs
Assets/Scripts/Economy/PendingOpsStore.cs
Assets/Scripts/Economy/PendingPointsOp.cs
Assets/Scripts/Economy/PointsActions.cs
Assets/Scripts/Economy/PointsBackendFlag.cs
Assets/Scripts/Economy/PointsDtos.cs
Assets/Scripts/Economy/PointsService.cs
Assets/Scripts/Economy/ProgressOutcome.cs
Assets/Scripts/Economy/ProgressService.cs
Assets/Scripts/Economy/ServerBalanceSync.cs
Assets/Scripts/Economy/ShopPurchaseOutcome.cs
Assets/Scripts/Economy/ShopPurchaseService.cs
Assets/Scripts/Economy/SpendOutcome.cs
Assets/Scripts/Economy/Tests/ModeEntryFeeSpendTests.cs
Assets/Scripts/Economy/Tests/PendingOpsQueueTests.cs
Assets/Scripts/Economy/Tests/PendingPointsOpLoanTests.cs
Assets/Scripts/Economy/Tests/PointsServiceTests.cs
Assets/Scripts/Economy/Tests/PointsSpendTests.cs
Assets/Scripts/Economy/Tests/ProgressServiceTests.cs
Assets/Scripts/Economy/Tests/ServerBalanceSyncTests.cs
Assets/Scripts/Economy/Tests/ShopPurchaseServiceTests.cs
Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs
Assets/Scripts/EconomyRuntime/PointsSpendGate.cs
Assets/Scripts/EconomyRuntime/ServerBalanceSyncBehaviour.cs
Assets/Scripts/EconomyRuntime/Tests/ApplyServerBalanceTests.cs
Assets/Scripts/Editor/A4DiffHelper.cs
Assets/Scripts/Editor/BagDatabaseCSVSetup.cs
Assets/Scripts/Editor/BagManagerSetup.cs
Assets/Scripts/Editor/BagSelectionModalAutoWire.cs
Assets/Scripts/Editor/BuildHoleCompleteModal.cs
Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs
Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsDefaultSpritePatcher.cs
Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsPatcherAutoRunner.cs
Assets/Scripts/Editor/CanvasScalerMigration/CanvasScalerMigrationTool.cs
Assets/Scripts/Editor/CanvasScalerMigration/CanvasScalerTestSceneBuilder.cs
Assets/Scripts/Editor/CanvasScalerMigration/CleanupStaleBallClones.cs
Assets/Scripts/Editor/CanvasScalerMigration/HoleCompleteWidgetBuilder.cs
Assets/Scripts/Editor/CanvasScalerMigration/IndicatorWidgetBuilder.cs
Assets/Scripts/Editor/CaptureHelper.cs
Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs
Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs
Assets/Scripts/Editor/CourseImporter/BridgeInstanceCatalog.cs
Assets/Scripts/Editor/CourseImporter/BridgeObstacleBaker.cs
Assets/Scripts/Editor/CourseImporter/BridgeTransplantTool.cs
Assets/Scripts/Editor/CourseImporter/BuildExperimentalHole01.cs
Assets/Scripts/Editor/CourseImporter/CourseImporterWindow.cs
Assets/Scripts/Editor/CourseImporter/CupReseatTool.cs
Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.cs
Assets/Scripts/Editor/CourseImporter/HoleDebugWindow.cs
Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs
Assets/Scripts/Editor/CourseImporter/HoleImporter.cs
Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs
Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs
Assets/Scripts/Editor/CourseImporter/MigrateHoleDataToCourseNamespaced.cs
Assets/Scripts/Editor/CourseImporter/MissionStartAreaBaker.cs
Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs
Assets/Scripts/Editor/CourseImporter/ReimportCurrentHole.cs
Assets/Scripts/Editor/CourseImporter/StandaloneTreeCatalog.cs
Assets/Scripts/Editor/CourseImporter/TeeSkirtSettingsWindow.cs
Assets/Scripts/Editor/CourseImporter/TreeBakeValidator.cs
Assets/Scripts/Editor/CourseImporter/TreeBrushTool.cs
Assets/Scripts/Editor/CourseImporter/TreeObstacleBaker.cs
Assets/Scripts/Editor/CourseImporter/TreePlacer.cs
Assets/Scripts/Editor/CourseImporter/TreePlacerWindow.cs
Assets/Scripts/Editor/CourseImporter/WaterShoreSettingsWindow.cs
Assets/Scripts/Editor/DevTools/DevAutoSignIn.cs
Assets/Scripts/Editor/DevTools/DevClubGrants.cs
Assets/Scripts/Editor/DevTools/SceneSaveChurnGuard.cs
Assets/Scripts/Editor/DynamicFontAtlasGuard.cs
Assets/Scripts/Editor/GolferGate/Tests/GolferTestBuildGateTests.cs
Assets/Scripts/Editor/GreenAuthoring/CaptureEditorWindow.cs
Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringMath.cs
Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringVisualGate.cs
Assets/Scripts/Editor/GreenAuthoring/GreenJsonWriter.cs
Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs
Assets/Scripts/Editor/ItemManagerSetup.cs
Assets/Scripts/Editor/MapViewCapture/MapViewCaptureBotMenu.cs
Assets/Scripts/Editor/MapViewCapture/MapViewCaptureDriver.cs
Assets/Scripts/Editor/MatchmakingCaptureRunner.cs
Assets/Scripts/Editor/Missions/DailyClearHarness.cs
Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs
Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs
Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs
Assets/Scripts/Editor/Physics/PhysicsTuningWindow.cs
Assets/Scripts/Editor/PutterTimingSlabSetup.cs
Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs
Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs
Assets/Scripts/Editor/SaveDataHostExecutionOrder.cs
Assets/Scripts/Editor/SaveDataHostSetup.cs
Assets/Scripts/Editor/SceneHygiene/StagedHoleSceneGuard.cs
Assets/Scripts/Editor/SceneSnapshot/ManualSceneSnapshotWindow.cs
Assets/Scripts/Editor/SceneSnapshot/SceneSnapshotCapture.cs
Assets/Scripts/Editor/SceneSnapshot/SceneSnapshotRestore.cs
Assets/Scripts/Editor/SceneSnapshot/SnapshotData.cs
Assets/Scripts/Editor/SceneSnapshot/Tests/SceneSnapshotTests.cs
Assets/Scripts/Editor/ScreenshotTool.cs
Assets/Scripts/Editor/SelectorAutoCapture.cs
Assets/Scripts/Editor/SelectorScreenshotHelper.cs
Assets/Scripts/Editor/SurfaceMarkerMap.cs
Assets/Scripts/Editor/TournamentResultModalBuilder.cs
Assets/Scripts/Editor/Tournaments/TournamentLoopCaptureHarness.cs
Assets/Scripts/Editor/TreeWindDriverEditorGuard.cs
Assets/Scripts/Editor/VersusResultScreenBuilder.cs
Assets/Scripts/Gameplay/Config/ControlsConfig.cs
Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs
Assets/Scripts/Gameplay/Defaults/DefaultStatProvider.cs
Assets/Scripts/Gameplay/Defaults/StatProviderBus.cs
Assets/Scripts/Gameplay/Environment/SkyPreset.cs
Assets/Scripts/Gameplay/Environment/SkyPresetLibrary.cs
Assets/Scripts/Gameplay/Environment/SkyRandomizer.cs
Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs
Assets/Scripts/Gameplay/Golfer/GolferTestBootstrap.cs
Assets/Scripts/Gameplay/Input/DebugShotAccuracy.cs
Assets/Scripts/Gameplay/Input/DebugShotInputSource.cs
Assets/Scripts/Gameplay/Input/FlickMath.cs
Assets/Scripts/Gameplay/Input/FlickPullMath.cs
Assets/Scripts/Gameplay/Input/IShotInputSource.cs
Assets/Scripts/Gameplay/Input/InputSimulationBootstrap.cs
Assets/Scripts/Gameplay/Input/InputSystemSource.cs
Assets/Scripts/Gameplay/Input/ShotConeTestDriver.cs
Assets/Scripts/Gameplay/Input/ShotController.cs
Assets/Scripts/Gameplay/Input/ShotDebugFlags.cs
Assets/Scripts/Gameplay/Input/ShotInputState.cs
Assets/Scripts/Gameplay/Input/ShotIntent.cs
Assets/Scripts/Gameplay/Input/ShotState.cs
Assets/Scripts/Gameplay/Input/SyntheticInputSource.cs
Assets/Scripts/Gameplay/Loop/ActiveCourseContext.cs
Assets/Scripts/Gameplay/Loop/BallState.cs
Assets/Scripts/Gameplay/Loop/BallStateChange.cs
Assets/Scripts/Gameplay/Loop/BallStateMachine.cs
Assets/Scripts/Gameplay/Loop/ICupDetector.cs
Assets/Scripts/Gameplay/Loop/NullCupDetector.cs
Assets/Scripts/Gameplay/Loop/OBReason.cs
Assets/Scripts/Gameplay/Loop/RealCupDetector.cs
Assets/Scripts/Gameplay/Loop/Session/GameSession.cs
Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs
Assets/Scripts/Gameplay/Loop/Session/IHoleProgressionStore.cs
Assets/Scripts/Gameplay/Loop/Session/ISessionStore.cs
Assets/Scripts/Gameplay/Loop/ShotResult.cs
Assets/Scripts/Gameplay/Missions/DailyMissionState.cs
Assets/Scripts/Gameplay/Missions/MissionCatalog.cs
Assets/Scripts/Gameplay/Missions/MissionCsv.cs
Assets/Scripts/Gameplay/Missions/MissionDefinition.cs
Assets/Scripts/Gameplay/Missions/MissionGoal.cs
Assets/Scripts/Gameplay/Missions/MissionGoalEvaluator.cs
Assets/Scripts/Gameplay/Missions/MissionProgressionService.cs
Assets/Scripts/Gameplay/Missions/MissionResult.cs
Assets/Scripts/Gameplay/Missions/MissionSession.cs
Assets/Scripts/Gameplay/Missions/MissionSessionBag.cs
Assets/Scripts/Gameplay/Tests/ActionButtonRenderingTests.cs
Assets/Scripts/Gameplay/Tests/AimLineBendTests.cs
Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs
Assets/Scripts/Gameplay/Tests/BallStateMachineTests.cs
Assets/Scripts/Gameplay/Tests/BotSchemeParityTests.cs
Assets/Scripts/Gameplay/Tests/ClubSelectionGreenGateTests.cs
Assets/Scripts/Gameplay/Tests/ControlSchemeServiceTests.cs
Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs
Assets/Scripts/Gameplay/Tests/FlickMathTests.cs
Assets/Scripts/Gameplay/Tests/FlickPullMathTests.cs
Assets/Scripts/Gameplay/Tests/FreeSwingMathTests.cs
Assets/Scripts/Gameplay/Tests/FreeSwingSchemeDriverTests.cs
Assets/Scripts/Gameplay/Tests/GameSessionTests.cs
Assets/Scripts/Gameplay/Tests/HoleCompleteModal/HoleCompleteModalControllerTests.cs
Assets/Scripts/Gameplay/Tests/HoleCompleteModal/HoleCompletionBridgeTests.cs
Assets/Scripts/Gameplay/Tests/HoleCompleteModal/RewardGrantTests.cs
Assets/Scripts/Gameplay/Tests/LaneEndCapTests.cs
Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs
Assets/Scripts/Gameplay/Tests/MissionGoalEvaluatorTests.cs
Assets/Scripts/Gameplay/Tests/MissionSessionTests.cs
Assets/Scripts/Gameplay/Tests/NeedleMathTests.cs
Assets/Scripts/Gameplay/Tests/NeedleSchemeDriverTests.cs
Assets/Scripts/Gameplay/Tests/PendulumMathTests.cs
Assets/Scripts/Gameplay/Tests/PendulumSchemeDriverTests.cs
Assets/Scripts/Gameplay/Tests/PlayMode/LiveStatProviderHostPlayModeTests.cs
Assets/Scripts/Gameplay/Tests/PowerGaugeMarkerTests.cs
Assets/Scripts/Gameplay/Tests/PutterConeLifecycleTests.cs
Assets/Scripts/Gameplay/Tests/QualityTierResolverTests.cs
Assets/Scripts/Gameplay/Tests/QualityTierServiceTests.cs
Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs
Assets/Scripts/Gameplay/Tests/SchemeConfirmTests.cs
Assets/Scripts/Gameplay/Tests/SelectorCarouselMathTests.cs
Assets/Scripts/Gameplay/Tests/ShotAimParityTests.cs
Assets/Scripts/Gameplay/Tests/ShotConeViewConfigTests.cs
Assets/Scripts/Gameplay/Tests/ShotControllerFlickGateTests.cs
Assets/Scripts/Gameplay/Tests/ShotControllerPuttModeTests.cs
Assets/Scripts/Gameplay/Tests/ShotControllerSeamParityTests.cs
Assets/Scripts/Gameplay/Tests/ShotControllerTests.cs
Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs
Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs
Assets/Scripts/Gameplay/Tests/ShotTimingTelemetryTests.cs
Assets/Scripts/Gameplay/Tests/SpinSelectorMappingTests.cs
Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs
Assets/Scripts/Gameplay/Tests/StatProviderBusTests.cs
Assets/Scripts/Gameplay/Tests/TournamentRoundLoopTests.cs
Assets/Scripts/Gameplay/Tests/VersusHudTests.cs
Assets/Scripts/Gameplay/TournamentContext/TournamentRoundContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsRoot.cs
Assets/Scripts/Gameplay/UI/ShotUI/AimLineBendRenderer.cs
Assets/Scripts/Gameplay/UI/ShotUI/ArrowGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/AssemblyInfo.cs
Assets/Scripts/Gameplay/UI/ShotUI/BallButtonWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/BallConeAlphaMirror.cs
Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs
Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleSpriteBinder.cs
Assets/Scripts/Gameplay/UI/ShotUI/ClubSelectionBroadcast.cs
Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs
Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs
Assets/Scripts/Gameplay/UI/ShotUI/ConeMeshGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotExecutionBand.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotExecutionContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotExecutionSampling.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotSchemeSigmaCalibrator.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotSwing.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotSwingGates.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/BotSwingPlan.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/FlickBotExecutor.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/FreeSwingBotExecutor.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/IBotSchemeExecutor.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/NeedleBotExecutor.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Bot/PendulumBotExecutor.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/ControlScheme.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/ControlSchemeService.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickGradePopBinder.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingAnalyzerChip.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingColors.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingLaneView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingMath.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/IShotSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleArcGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleArcView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleColors.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleMath.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedlePowerCircleView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleTapCatcher.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumBarView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumFadingView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumLaneView.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumMath.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/PlaceholderSchemeDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeConfirmContent.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeConfirmDecision.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeGradePop.cs
Assets/Scripts/Gameplay/UI/ShotUI/Controls/ShotSchemeHost.cs
Assets/Scripts/Gameplay/UI/ShotUI/DebugShotPanel.cs
Assets/Scripts/Gameplay/UI/ShotUI/FadeDrawButtonWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/BallContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/ClubContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/FakeStateLock.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/ShotModeContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/SpinContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/HUD/WindContext.cs
Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteData.cs
Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/IHoleOutTrigger.cs
Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs
Assets/Scripts/Gameplay/UI/ShotUI/MapTargetReadoutWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/MapViewCaptureDriver.cs
Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs
Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs
Assets/Scripts/Gameplay/UI/ShotUI/OutsideClickCatcher.cs
Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTier.cs
Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTierResolver.cs
Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTierService.cs
Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselDrag.cs
Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselMath.cs
Assets/Scripts/Gameplay/UI/ShotUI/SelectorDragRouter.cs
Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs
Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs
Assets/Scripts/Gameplay/UI/ShotUI/ShotInProgressUiGate.cs
Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs
Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutMath.cs
Assets/Scripts/Gameplay/UI/ShotUI/ShotTelemetryRelay.cs
Assets/Scripts/Gameplay/UI/ShotUI/SpinButtonWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/TeeIdleGlowController.cs
Assets/Scripts/Gameplay/UI/ShotUI/TimingSlabGraphic.cs
Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs
Assets/Scripts/Gameplay/UI/ShotUI/VersusHudController.cs
Assets/Scripts/Gameplay/UI/ShotUI/WindIndicatorWidget.cs
Assets/Scripts/Gps/ActivityDtos.cs
Assets/Scripts/Gps/ActivityService.cs
Assets/Scripts/Gps/BadgeService.cs
Assets/Scripts/Gps/Geohash.cs
Assets/Scripts/Gps/GpsDtos.cs
Assets/Scripts/Gps/GpsFixStore.cs
Assets/Scripts/Gps/GpsScoreAttachment.cs
Assets/Scripts/Gps/GpsSessionTracker.cs
Assets/Scripts/Gps/GpsTrustSignals.cs
Assets/Scripts/Gps/LocationProvider.cs
Assets/Scripts/Gps/MapProjection.cs
Assets/Scripts/Gps/ProfileDtos.cs
Assets/Scripts/Gps/RecognitionService.cs
Assets/Scripts/Gps/RoundSession.cs
Assets/Scripts/Gps/ScoreDtos.cs
Assets/Scripts/Gps/ScoreHistoryService.cs
Assets/Scripts/Gps/ScoreService.cs
Assets/Scripts/Gps/ScoreStatsService.cs
Assets/Scripts/Gps/Tests/ActivityServiceJsonTests.cs
Assets/Scripts/Gps/Tests/GeohashTests.cs
Assets/Scripts/Gps/Tests/GpsProfilePackTests.cs
Assets/Scripts/Gps/Tests/GpsScoreAttachmentTests.cs
Assets/Scripts/Gps/Tests/GpsSessionTrackerTests.cs
Assets/Scripts/Gps/Tests/GpsTestDoubles.cs
Assets/Scripts/Gps/Tests/GpsTrustSignalsTests.cs
Assets/Scripts/Gps/Tests/MapProjectionTests.cs
Assets/Scripts/Gps/Tests/RecognitionServiceTests.cs
Assets/Scripts/Gps/Tests/RoundSessionTests.cs
Assets/Scripts/Gps/Tests/ScoreServiceTests.cs
Assets/Scripts/Gps/Tests/VenueServiceTests.cs
Assets/Scripts/Gps/VenueService.cs
Assets/Scripts/HoleMetadata.cs
Assets/Scripts/InventoryCatalogAdapter.cs
Assets/Scripts/InventorySync/IInventoryCatalog.cs
Assets/Scripts/InventorySync/IInventoryTransport.cs
Assets/Scripts/InventorySync/InventoryCodec.cs
Assets/Scripts/InventorySync/InventoryGrants.cs
Assets/Scripts/InventorySync/InventoryMerge.cs
Assets/Scripts/InventorySync/InventoryProjector.cs
Assets/Scripts/InventorySync/InventoryRaise.cs
Assets/Scripts/InventorySync/InventorySnapshot.cs
Assets/Scripts/InventorySync/InventorySyncBehaviour.cs
Assets/Scripts/InventorySync/InventorySyncService.cs
Assets/Scripts/InventorySync/InventoryWriteBehind.cs
Assets/Scripts/InventorySync/Tests/InventoryBootOutcomeTests.cs
Assets/Scripts/InventorySync/Tests/InventoryCodecLoanTests.cs
Assets/Scripts/InventorySync/Tests/InventoryCodecTests.cs
Assets/Scripts/InventorySync/Tests/InventoryGrantLedgerTests.cs
Assets/Scripts/InventorySync/Tests/InventoryMergeTests.cs
Assets/Scripts/InventorySync/Tests/InventoryProjectorTests.cs
Assets/Scripts/InventorySync/Tests/InventorySyncServiceTests.cs
Assets/Scripts/ItemManager.cs
Assets/Scripts/LiveStatProviderHost.cs
Assets/Scripts/Net/ApiClient.cs
Assets/Scripts/Net/ApiEnvelope.cs
Assets/Scripts/Net/ApiResult.cs
Assets/Scripts/Net/Endpoints.cs
Assets/Scripts/Net/HttpMessages.cs
Assets/Scripts/Net/IAuthTokenProvider.cs
Assets/Scripts/Net/ICoroutineRunner.cs
Assets/Scripts/Net/Tests/ApiClientTests.cs
Assets/Scripts/Net/Tests/NetTestDoubles.cs
Assets/Scripts/Net/UnityWebRequestTransport.cs
Assets/Scripts/NoticesRuntime/NoticeService.cs
Assets/Scripts/NoticesRuntime/RemoteNoticeDtos.cs
Assets/Scripts/NoticesRuntime/RemoteNoticeSource.cs
Assets/Scripts/Physics/Core/AeroConfig.cs
Assets/Scripts/Physics/Core/AeroModel.cs
Assets/Scripts/Physics/Core/BallPhysicsModifiers.cs
Assets/Scripts/Physics/Core/BallSimulation.cs
Assets/Scripts/Physics/Core/BridgeObstacleData.cs
Assets/Scripts/Physics/Core/ClubSpec.cs
Assets/Scripts/Physics/Core/CoefficientLut.cs
Assets/Scripts/Physics/Core/CupSpec.cs
Assets/Scripts/Physics/Core/HeightmapData.cs
Assets/Scripts/Physics/Core/IGroundProvider.cs
Assets/Scripts/Physics/Core/ISurfaceProvider.cs
Assets/Scripts/Physics/Core/PuttConfig.cs
Assets/Scripts/Physics/Core/ShotInput.cs
Assets/Scripts/Physics/Core/SpinState.cs
Assets/Scripts/Physics/Core/SurfaceConfig.cs
Assets/Scripts/Physics/Core/SurfaceType.cs
Assets/Scripts/Physics/Core/Trajectory.cs
Assets/Scripts/Physics/Core/TreeObstacleData.cs
Assets/Scripts/Physics/Core/WindConfig.cs
Assets/Scripts/Physics/Core/WindModel.cs
Assets/Scripts/Physics/Math/Unity/FP3Extensions.cs
Assets/Scripts/Physics/Math/fp.cs
Assets/Scripts/Physics/Math/fpMath.cs
Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs
Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs
Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs
Assets/Scripts/Physics/Runtime/BridgeObstacleLoader.cs
Assets/Scripts/Physics/Runtime/BridgeObstacleProvider.cs
Assets/Scripts/Physics/Runtime/HeightProvider.cs
Assets/Scripts/Physics/Runtime/HeightmapLoader.cs
Assets/Scripts/Physics/Runtime/HoleDataIO.cs
Assets/Scripts/Physics/Runtime/Phase1TestController.cs
Assets/Scripts/Physics/Runtime/PhaseTestController.cs
Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs
Assets/Scripts/Physics/Runtime/SurfaceMarker.cs
Assets/Scripts/Physics/Runtime/TreeObstacleLoader.cs
Assets/Scripts/Physics/Runtime/TreeObstacleProvider.cs
Assets/Scripts/Physics/Stats/BallStats.cs
Assets/Scripts/Physics/Stats/CharacterStats.cs
Assets/Scripts/Physics/Stats/ClubStats.cs
Assets/Scripts/Physics/Stats/PutterStats.cs
Assets/Scripts/Physics/Stats/ResolvedShotModifiers.cs
Assets/Scripts/Physics/Stats/ShotInputBuilder.cs
Assets/Scripts/Physics/Stats/StatBundle.cs
Assets/Scripts/Physics/Stats/StatCaps.cs
Assets/Scripts/Physics/Stats/StatCoefficients.cs
Assets/Scripts/Physics/Stats/StatModifierResolver.cs
Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs
Assets/Scripts/Physics/Tests/AeroConstantModeTests.cs
Assets/Scripts/Physics/Tests/AerodynamicsTests.cs
Assets/Scripts/Physics/Tests/AimCameraFramingTests.cs
Assets/Scripts/Physics/Tests/AudioEmitterTests.cs
Assets/Scripts/Physics/Tests/AutoClubSelectorTests.cs
Assets/Scripts/Physics/Tests/BakedHeightProviderTests.cs
Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs
Assets/Scripts/Physics/Tests/BallAnimatorTests.cs
Assets/Scripts/Physics/Tests/BallPlacementIntegrationTests.cs
Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs
Assets/Scripts/Physics/Tests/BridgeCollisionTests.cs
Assets/Scripts/Physics/Tests/CupCaptureSimTests.cs
Assets/Scripts/Physics/Tests/Editor/AllEditModeTestRunner.cs
Assets/Scripts/Physics/Tests/Editor/Iter2cTestRunner.cs
Assets/Scripts/Physics/Tests/Editor/Iter4ShotCapture.cs
Assets/Scripts/Physics/Tests/Editor/Iter4TestRunner.cs
Assets/Scripts/Physics/Tests/Editor/Iter5TestRunner.cs
Assets/Scripts/Physics/Tests/Editor/Iter6TestRunner.cs
Assets/Scripts/Physics/Tests/Editor/Iter8TestRunner.cs
Assets/Scripts/Physics/Tests/FadeDrawTiltTests.cs
Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs
Assets/Scripts/Physics/Tests/HoleDataFormatTests.cs
Assets/Scripts/Physics/Tests/HoleSessionDriverTests.cs
Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs
Assets/Scripts/Physics/Tests/NextShotHandoffTests.cs
Assets/Scripts/Physics/Tests/PhysicsLabControllerHandleShotResolvedTests.cs
Assets/Scripts/Physics/Tests/PhysicsLabControllerLabVsProdTests.cs
Assets/Scripts/Physics/Tests/PlacementEntriesTests.cs
Assets/Scripts/Physics/Tests/PlacementSnapTests.cs
Assets/Scripts/Physics/Tests/ProjectileMathTests.cs
Assets/Scripts/Physics/Tests/PuttTests.cs
Assets/Scripts/Physics/Tests/PutterAimLineTests.cs
Assets/Scripts/Physics/Tests/PutterGreenReaderBakeTests.cs
Assets/Scripts/Physics/Tests/PutterModeSurfaceControllerTests.cs
Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs
Assets/Scripts/Physics/Tests/RepositionClubReDecideTests.cs
Assets/Scripts/Physics/Tests/RollAndPuttTuningTests.cs
Assets/Scripts/Physics/Tests/ShotInputBuilderTests.cs
Assets/Scripts/Physics/Tests/StatResolverTests.cs
Assets/Scripts/Physics/Tests/SurfaceTests.cs
Assets/Scripts/Physics/Tests/TreeCollisionTests.cs
Assets/Scripts/Physics/Tests/TreeOccludeFadeDriverTests.cs
Assets/Scripts/Physics/Tests/ViewerTests.cs
Assets/Scripts/Physics/Tests/WaterSplashControllerTests.cs
Assets/Scripts/Physics/Tests/WindTests.cs
Assets/Scripts/Physics/Tests/fpMathTests.cs
Assets/Scripts/Physics/Viewer/AimRotationHelper.cs
Assets/Scripts/Physics/Viewer/AssemblyInfo.cs
Assets/Scripts/Physics/Viewer/AutoClubSelector.cs
Assets/Scripts/Physics/Viewer/BallAnimator.cs
Assets/Scripts/Physics/Viewer/BallAudioEmitter.cs
Assets/Scripts/Physics/Viewer/BallTrailController.cs
Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/AudioFidelityCapture.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/BotClubCalibrationHarness.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/BotSchemeCalibrationHarness.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/GameViewSizeUtil.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/LiveStatLogTee.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/ObBoundaryCaptureMenu.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/ObRecoveryCaptureMenu.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs
Assets/Scripts/Physics/Viewer/Bot/Editor/ZoneBakeAfterClipMenu.cs
Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs
Assets/Scripts/Physics/Viewer/Bot/ObBoundaryCaptureBot.cs
Assets/Scripts/Physics/Viewer/Bot/ObRecoveryCaptureBot.cs
Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs
Assets/Scripts/Physics/Viewer/Bot/VersusHudCaptureBot.cs
Assets/Scripts/Physics/Viewer/Bot/VersusHudNavCaptureBot.cs
Assets/Scripts/Physics/Viewer/Bot/WaterSplashCaptureRig.cs
Assets/Scripts/Physics/Viewer/Bot/ZoneBakeAfterClipBot.cs
Assets/Scripts/Physics/Viewer/BotClubSync.cs
Assets/Scripts/Physics/Viewer/BotTreeProbe.cs
Assets/Scripts/Physics/Viewer/CameraModeDebugHUD.cs
Assets/Scripts/Physics/Viewer/ChaseCamera.cs
Assets/Scripts/Physics/Viewer/DashboardUI.cs
Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs
Assets/Scripts/Physics/Viewer/Editor/GreenTuningPanelBuilder.cs
Assets/Scripts/Physics/Viewer/Editor/PutterGreenReaderSceneSetup.cs
Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2cMenu.cs
Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2dMenu.cs
Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2eMenu.cs
Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2fMenu.cs
Assets/Scripts/Physics/Viewer/Editor/SmokeRunnerPutterConeMenu.cs
Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs
Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs
Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs
Assets/Scripts/Physics/Viewer/HoleCompletionBridge.cs
Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs
Assets/Scripts/Physics/Viewer/IModeSetter.cs
Assets/Scripts/Physics/Viewer/LabHoleBinder.cs
Assets/Scripts/Physics/Viewer/LandingBannerController.cs
Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs
Assets/Scripts/Physics/Viewer/OBDropResolver.cs
Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs
Assets/Scripts/Physics/Viewer/PhysicsLabController.cs
Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs
Assets/Scripts/Physics/Viewer/PlacementSnapHelper.cs
Assets/Scripts/Physics/Viewer/PutterAimLine.cs
Assets/Scripts/Physics/Viewer/PutterConeSmokeCapture.cs
Assets/Scripts/Physics/Viewer/PutterGreenReader.cs
Assets/Scripts/Physics/Viewer/PutterModeSurfaceController.cs
Assets/Scripts/Physics/Viewer/ShotPreset.cs
Assets/Scripts/Physics/Viewer/ShotPresetCatalog.cs
Assets/Scripts/Physics/Viewer/SmokeCaptureCupSpeedGate.cs
Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs
Assets/Scripts/Physics/Viewer/SmokeRunner2dHost.cs
Assets/Scripts/Physics/Viewer/SmokeRunner2eHost.cs
Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs
Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs
Assets/Scripts/Physics/Viewer/TestGreenLabSetup.cs
Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs
Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs
Assets/Scripts/Physics/Viewer/VersusBot.cs
Assets/Scripts/Physics/Viewer/VersusMatchController.cs
Assets/Scripts/Physics/Viewer/WaterSplashController.cs
Assets/Scripts/Save/ClubOwnership.cs
Assets/Scripts/Save/ISavePersister.cs
Assets/Scripts/Save/LocalJsonPersister.cs
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/SaveDataHost.cs
Assets/Scripts/Save/SaveSchemaMigrator.cs
Assets/Scripts/Save/Tests/BootExecutionOrderTests.cs
Assets/Scripts/Save/Tests/ClubOwnershipTests.cs
Assets/Scripts/Save/Tests/GachaTicketTests.cs
Assets/Scripts/Save/Tests/PlayMode/SaveLayerPlayModeTests.cs
Assets/Scripts/Save/Tests/SaveLayerTests.cs
Assets/Scripts/SceneSnapshot/ManualPropId.cs
Assets/Scripts/Social/GiftDtos.cs
Assets/Scripts/Social/GiftItemName.cs
Assets/Scripts/Social/GiftService.cs
Assets/Scripts/Social/LoanDtos.cs
Assets/Scripts/Social/LoanService.cs
Assets/Scripts/Social/Tests/GiftItemNameTests.cs
Assets/Scripts/Social/Tests/GiftVoteServiceTests.cs
Assets/Scripts/Social/Tests/LoanServiceTests.cs
Assets/Scripts/Social/UserDetailDto.cs
Assets/Scripts/Social/UserService.cs
Assets/Scripts/Social/VoteDtos.cs
Assets/Scripts/Social/VoteService.cs
Assets/Scripts/StaminaRuntimeService.cs
Assets/Scripts/Telemetry/TelemetryBehaviour.cs
Assets/Scripts/Telemetry/TelemetryConfig.cs
Assets/Scripts/Telemetry/TelemetryService.cs
Assets/Scripts/Telemetry/Tests/TelemetryServiceTests.cs
Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs
Assets/Scripts/TestSupport/TestBoot.cs
Assets/Scripts/Tournaments/BotFieldConfig.cs
Assets/Scripts/Tournaments/BotFieldGenerator.cs
Assets/Scripts/Tournaments/BotFieldMath.cs
Assets/Scripts/Tournaments/CharacterSnapshot.cs
Assets/Scripts/Tournaments/EntryState.cs
Assets/Scripts/Tournaments/HoleResult.cs
Assets/Scripts/Tournaments/ICharacterStatsProvider.cs
Assets/Scripts/Tournaments/ITournamentBackend.cs
Assets/Scripts/Tournaments/ITournamentClock.cs
Assets/Scripts/Tournaments/ITournamentEntryStore.cs
Assets/Scripts/Tournaments/ITournamentSeams.cs
Assets/Scripts/Tournaments/LocalTournamentBackend.cs
Assets/Scripts/Tournaments/PrizeBand.cs
Assets/Scripts/Tournaments/SaveBackedEntryStore.cs
Assets/Scripts/Tournaments/ShotCommand.cs
Assets/Scripts/Tournaments/StubTournamentBackend.cs
Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs
Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs
Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs
Assets/Scripts/Tournaments/Tests/SaveBackedEntryStoreTests.cs
Assets/Scripts/Tournaments/Tests/TournamentAdapterTests.cs
Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs
Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs
Assets/Scripts/Tournaments/Tests/TournamentEligibilityTests.cs
Assets/Scripts/Tournaments/Tests/TournamentSnapshotImmunityTests.cs
Assets/Scripts/Tournaments/TournamentCardStateMapper.cs
Assets/Scripts/Tournaments/TournamentCsvLoader.cs
Assets/Scripts/Tournaments/TournamentDefinition.cs
Assets/Scripts/Tournaments/TournamentEligibility.cs
Assets/Scripts/Tournaments/TournamentEnums.cs
Assets/Scripts/Tournaments/TournamentLeaderboardEntry.cs
Assets/Scripts/Tournaments/TournamentRestrictions.cs
Assets/Scripts/Tournaments/TournamentResult.cs
Assets/Scripts/TournamentsRuntime/AssemblyInfo.cs
Assets/Scripts/TournamentsRuntime/CharacterManagerStatsProvider.cs
Assets/Scripts/TournamentsRuntime/HoleParProviderAdapter.cs
Assets/Scripts/TournamentsRuntime/ItemRewardServiceAdapter.cs
Assets/Scripts/TournamentsRuntime/RemoteTournamentBackend.cs
Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs
Assets/Scripts/TournamentsRuntime/RemoteTournamentSource.cs
Assets/Scripts/TournamentsRuntime/RewardPointsServiceAdapter.cs
Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs
Assets/Scripts/TournamentsRuntime/Tests/CatalogArtPolicyTests.cs
Assets/Scripts/TournamentsRuntime/Tests/NoticeResolutionTests.cs
Assets/Scripts/TournamentsRuntime/Tests/RemoteScheduleTests.cs
Assets/Scripts/TournamentsRuntime/Tests/ScheduleRefreshTests.cs
Assets/Scripts/TournamentsRuntime/Tests/TournamentAsyncBoardTests.cs
Assets/Scripts/TournamentsRuntime/Tests/TournamentDescriptionTests.cs
Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs
Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs
Assets/Scripts/TournamentsRuntime/TournamentArtPolicy.cs
Assets/Scripts/TournamentsRuntime/TournamentArtService.cs
Assets/Scripts/TournamentsRuntime/TournamentBackendPolicy.cs
Assets/Scripts/TournamentsRuntime/TournamentDescription.cs
Assets/Scripts/TournamentsRuntime/TournamentDisplayName.cs
Assets/Scripts/TournamentsRuntime/TournamentNetDtos.cs
Assets/Scripts/TournamentsRuntime/TournamentRulesText.cs
Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs
Assets/Scripts/TournamentsRuntime/TournamentService.cs
Assets/Scripts/TournamentsRuntime/TournamentSubmitQueue.cs
Assets/Scripts/TournamentsRuntime/TournamentVenueLine.cs
Assets/Scripts/UI/AboutSubmenu.cs
Assets/Scripts/UI/Account/AccountUiBridge.cs
Assets/Scripts/UI/Account/CreateUsernameScreenController.cs
Assets/Scripts/UI/Account/EmailConfirmationScreenController.cs
Assets/Scripts/UI/Account/LoginScreenController.cs
Assets/Scripts/UI/Account/PasswordRequirements.cs
Assets/Scripts/UI/Account/ResetPasswordScreenController.cs
Assets/Scripts/UI/Account/SignUpScreenController.cs
Assets/Scripts/UI/Account/StarterGate.cs
Assets/Scripts/UI/Account/UsernameClaim.cs
Assets/Scripts/UI/Account/UsernameRules.cs
Assets/Scripts/UI/AppVariantInfo.cs
Assets/Scripts/UI/AuthGate.cs
Assets/Scripts/UI/BuildInfo/AppVersion.cs
Assets/Scripts/UI/BuildInfo/BuildStamp.cs
Assets/Scripts/UI/ButtonPressFeedback.cs
Assets/Scripts/UI/Common/PaginationDotStrip.cs
Assets/Scripts/UI/Common/StreakFlameView.cs
Assets/Scripts/UI/ControlsSubmenu.cs
Assets/Scripts/UI/Core/SafeAreaFitter.cs
Assets/Scripts/UI/Editor/ClubControlArrowDemoRecorder.cs
Assets/Scripts/UI/Editor/ClubRosterDemoRecorder.cs
Assets/Scripts/UI/Editor/DailyMissionPillDemoRecorder.cs
Assets/Scripts/UI/Editor/DemoShowcaseRecorder.cs
Assets/Scripts/UI/Editor/FlickPullMappingVerify.cs
Assets/Scripts/UI/Editor/FlickPullReflect.cs
Assets/Scripts/UI/Editor/FlickShotViewVerify.cs
Assets/Scripts/UI/Editor/GachaDemoRecorder.cs
Assets/Scripts/UI/Editor/GachaRevealDemoRecorder.cs
Assets/Scripts/UI/Editor/GameplayLocalizationDemoRecorder.cs
Assets/Scripts/UI/Editor/GeneralShopDemoRecorder.cs
Assets/Scripts/UI/Editor/GolferTestDemoRecorder.cs
Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs
Assets/Scripts/UI/Editor/GpsFlowDemoRecorder.cs
Assets/Scripts/UI/Editor/InGameSettingsDemoRecorder.cs
Assets/Scripts/UI/Editor/LanguageSwitchDemoRecorder.cs
Assets/Scripts/UI/Editor/LoadingTipsDemoRecorder.cs
Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs
Assets/Scripts/UI/Editor/MapViewStrictCropDemoRecorder.cs
Assets/Scripts/UI/Editor/MissDuffFlickVerify.cs
Assets/Scripts/UI/Editor/MissionsDemoRecorder.cs
Assets/Scripts/UI/Editor/NoticeSlideDemoRecorder.cs
Assets/Scripts/UI/Editor/PaginationDotsDemoRecorder.cs
Assets/Scripts/UI/Editor/PowerGaugeMarkerDemoRecorder.cs
Assets/Scripts/UI/Editor/PowerGaugeMarkerVerifyBot.cs
Assets/Scripts/UI/Editor/PracticeMapDuringShotDemoRecorder.cs
Assets/Scripts/UI/Editor/PutterSelectorVerify.cs
Assets/Scripts/UI/Editor/QualityTierDemoRecorder.cs
Assets/Scripts/UI/Editor/QualityTierVerificationRecorder.cs
Assets/Scripts/UI/Editor/QuitTransitionDemoRecorder.cs
Assets/Scripts/UI/Editor/RankingsDemoRecorder.cs
Assets/Scripts/UI/Editor/RosterLocalizationDemoRecorder.cs
Assets/Scripts/UI/Editor/ShotAimParityDemoRecorder.cs
Assets/Scripts/UI/Editor/ShotTimingTelemetryVerify.cs
Assets/Scripts/UI/Editor/SkyRotationDemoRecorder.cs
Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs
Assets/Scripts/UI/Editor/StaminaShopDemoRecorder.cs
Assets/Scripts/UI/Editor/StarterSelectionDemoRecorder.cs
Assets/Scripts/UI/Editor/StoreHistoryDemoRecorder.cs
Assets/Scripts/UI/Editor/TapFeedbackDemoRecorder.cs
Assets/Scripts/UI/Editor/TeeIdleGlowDemoRecorder.cs
Assets/Scripts/UI/Editor/TicketIconDerive.cs
Assets/Scripts/UI/Editor/TournamentBannerDemoRecorder.cs
Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs
Assets/Scripts/UI/Editor/TournamentDeniedDemoRecorder.cs
Assets/Scripts/UI/Editor/TournamentsModeCardDemoRecorder.cs
Assets/Scripts/UI/Editor/TreeOccludeFadeCaptureBot.cs
Assets/Scripts/UI/FadeController.cs
Assets/Scripts/UI/Gacha/GachaBannerArt.cs
Assets/Scripts/UI/Gacha/GachaBannerCard.cs
Assets/Scripts/UI/Gacha/GachaBannerModel.cs
Assets/Scripts/UI/Gacha/GachaCarouselController.cs
Assets/Scripts/UI/Gacha/GachaContentCatalogs.cs
Assets/Scripts/UI/Gacha/GachaHistoryRecord.cs
Assets/Scripts/UI/Gacha/GachaHistoryRow.cs
Assets/Scripts/UI/Gacha/GachaHistoryRowBall.cs
Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs
Assets/Scripts/UI/Gacha/GachaHistoryStore.cs
Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs
Assets/Scripts/UI/Gacha/GachaPrizeCardBinder.cs
Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs
Assets/Scripts/UI/Gacha/GachaPullFlow.cs
Assets/Scripts/UI/Gacha/GachaRatesModalController.cs
Assets/Scripts/UI/Gacha/GachaRatesText.cs
Assets/Scripts/UI/Gacha/GachaRevealModalController.cs
Assets/Scripts/UI/Gacha/GachaRewardType.cs
Assets/Scripts/UI/Gacha/GachaTabController.cs
Assets/Scripts/UI/Gacha/GachaTicketArt.cs
Assets/Scripts/UI/Gacha/GachaTicketManager.cs
Assets/Scripts/UI/Gacha/PrizeRecord.cs
Assets/Scripts/UI/Gacha/TicketCatalog.cs
Assets/Scripts/UI/Gacha/TicketType.cs
Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs
Assets/Scripts/UI/Gps/BadgeCellView.cs
Assets/Scripts/UI/Gps/CheckInConfirmModalController.cs
Assets/Scripts/UI/Gps/Editor/GpsAuthExtrasBuilder.cs
Assets/Scripts/UI/Gps/Editor/GpsAuthExtrasEditorRun.cs
Assets/Scripts/UI/Gps/Editor/GpsGeometryAudit.cs
Assets/Scripts/UI/Gps/Editor/GpsGiftVoteBuilder.cs
Assets/Scripts/UI/Gps/Editor/GpsGiftVoteEditorRun.cs
Assets/Scripts/UI/Gps/Editor/GpsPolishBuilder.cs
Assets/Scripts/UI/Gps/Editor/GpsPolishProbe.cs
Assets/Scripts/UI/Gps/Editor/GpsProfilePackBuilder.cs
Assets/Scripts/UI/Gps/Editor/GpsProfilePromptOnEntryRun.cs
Assets/Scripts/UI/Gps/Editor/GpsRoundsBuilder.cs
Assets/Scripts/UI/Gps/Editor/ScoreUploadEditorRun.cs
Assets/Scripts/UI/Gps/Editor/ScoreUploadScreenBuilder.cs
Assets/Scripts/UI/Gps/GiftSendModalController.cs
Assets/Scripts/UI/Gps/GpsAuthExtrasFlow.cs
Assets/Scripts/UI/Gps/GpsAvatarScreenController.cs
Assets/Scripts/UI/Gps/GpsBadgesScreenController.cs
Assets/Scripts/UI/Gps/GpsGate.cs
Assets/Scripts/UI/Gps/GpsGiftScreenController.cs
Assets/Scripts/UI/Gps/GpsGolfProfileScreenController.cs
Assets/Scripts/UI/Gps/GpsHubRoundRow.cs
Assets/Scripts/UI/Gps/GpsHubScreenController.cs
Assets/Scripts/UI/Gps/GpsNavBarBinder.cs
Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs
Assets/Scripts/UI/Gps/GpsProfileScreenController.cs
Assets/Scripts/UI/Gps/GpsRoundsScreenController.cs
Assets/Scripts/UI/Gps/GpsScreenEntryMotion.cs
Assets/Scripts/UI/Gps/GpsScreenTransition.cs
Assets/Scripts/UI/Gps/GpsUiColor.cs
Assets/Scripts/UI/Gps/GpsVoteScreenController.cs
Assets/Scripts/UI/Gps/GpsWelcomeScreenController.cs
Assets/Scripts/UI/Gps/HoleRowView.cs
Assets/Scripts/UI/Gps/RoundCompleteModalController.cs
Assets/Scripts/UI/Gps/RoundSpotRowView.cs
Assets/Scripts/UI/Gps/ScoreUploadDraft.cs
Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs
Assets/Scripts/UI/Gps/ShimmerBlock.cs
Assets/Scripts/UI/Gps/VenuePickerModalController.cs
Assets/Scripts/UI/Gps/VoteCardView.cs
Assets/Scripts/UI/Gps/VoteCreateModalController.cs
Assets/Scripts/UI/GraphicsSubmenu.cs
Assets/Scripts/UI/HUD/BallContextPopulator.cs
Assets/Scripts/UI/HUD/ClubContextPopulator.cs
Assets/Scripts/UI/HUD/LabInventoryStub.cs
Assets/Scripts/UI/HUD/PlayerContextPopulator.cs
Assets/Scripts/UI/HUD/PuttPathPredictor.cs
Assets/Scripts/UI/Hints/Editor/ScreenHintMenu.cs
Assets/Scripts/UI/Hints/Editor/ScreenHintModalBuilder.cs
Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs
Assets/Scripts/UI/Hints/ScreenHintCatalog.cs
Assets/Scripts/UI/Hints/ScreenHintModalController.cs
Assets/Scripts/UI/Hints/ScreenHintPresenter.cs
Assets/Scripts/UI/Hints/ScreenHintResolver.cs
Assets/Scripts/UI/Hints/ScreenHintStore.cs
Assets/Scripts/UI/HoleData.cs
Assets/Scripts/UI/HoleDatabase.cs
Assets/Scripts/UI/HoleDatabaseLoader.cs
Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionAutoWire.cs
Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionIteration3.cs
Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionSmokeRunner.cs
Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionTaskRunner.cs
Assets/Scripts/UI/HoleSelection/HoleCardController.cs
Assets/Scripts/UI/HoleSelection/HoleProgressionDebug.cs
Assets/Scripts/UI/HoleSelection/HoleProgressionService.cs
Assets/Scripts/UI/HoleSelection/HoleProgressionStoreAdapter.cs
Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs
Assets/Scripts/UI/Home/DailyMissionPillController.cs
Assets/Scripts/UI/Home/LoanOfferPillController.cs
Assets/Scripts/UI/Home/NoticePageSlider.cs
Assets/Scripts/UI/HomeScreenController.cs
Assets/Scripts/UI/Inventory/BagCarouselController.cs
Assets/Scripts/UI/Inventory/BagClubCard.cs
Assets/Scripts/UI/Inventory/BagClubModalController.cs
Assets/Scripts/UI/Inventory/BagDetailPanel.cs
Assets/Scripts/UI/Inventory/BagSelectionModalController.cs
Assets/Scripts/UI/Inventory/BagThumbnailCard.cs
Assets/Scripts/UI/Inventory/BallCarouselController.cs
Assets/Scripts/UI/Inventory/BallCompareController.cs
Assets/Scripts/UI/Inventory/BallData.cs
Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs
Assets/Scripts/UI/Inventory/BallDetailPanel.cs
Assets/Scripts/UI/Inventory/BallSegmentedBar.cs
Assets/Scripts/UI/Inventory/BallThumbnailCard.cs
Assets/Scripts/UI/Inventory/BallThumbnailEmptyCard.cs
Assets/Scripts/UI/Inventory/ClubCarouselController.cs
Assets/Scripts/UI/Inventory/ClubCompareController.cs
Assets/Scripts/UI/Inventory/ClubCsvParser.cs
Assets/Scripts/UI/Inventory/ClubData.cs
Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs
Assets/Scripts/UI/Inventory/ClubDetailPanel.cs
Assets/Scripts/UI/Inventory/ClubFilterBar.cs
Assets/Scripts/UI/Inventory/ClubInfoText.cs
Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs
Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs
Assets/Scripts/UI/Inventory/Editor/BagClubModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BagsContentAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallCarouselAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallCompareBuilder.cs
Assets/Scripts/UI/Inventory/Editor/BallDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallDetailPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/BallManagerSetup.cs
Assets/Scripts/UI/Inventory/Editor/BallThumbnailCardFix.cs
Assets/Scripts/UI/Inventory/Editor/BallThumbnailEmptyCardFix.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareRightPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ClubDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubLevelUpModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubManagerSetup.cs
Assets/Scripts/UI/Inventory/Editor/ClubThumbnailCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ItemRightPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseClubCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseModalBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemsContentBuilder.cs
Assets/Scripts/UI/Inventory/InventoryScreenController.cs
Assets/Scripts/UI/Inventory/ItemCarouselController.cs
Assets/Scripts/UI/Inventory/ItemDataRuntime.cs
Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs
Assets/Scripts/UI/Inventory/ItemDetailPanel.cs
Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs
Assets/Scripts/UI/Inventory/ItemUseClubCard.cs
Assets/Scripts/UI/Inventory/ItemUseModalController.cs
Assets/Scripts/UI/Inventory/PlayerItemData.cs
Assets/Scripts/UI/Inventory/Tests/BallRarityCsvTests.cs
Assets/Scripts/UI/Inventory/Tests/ClubInfoTextTests.cs
Assets/Scripts/UI/Inventory/Tests/ClubOverlayMergeTests.cs
Assets/Scripts/UI/Inventory/Tests/ClubRosterCsvTests.cs
Assets/Scripts/UI/Inventory/Tests/ClubRosterProd.cs
Assets/Scripts/UI/Inventory/Tests/ClubSeedingAgainstLiveCsvTests.cs
Assets/Scripts/UI/Inventory/Tests/LoadoutTokensTests.cs
Assets/Scripts/UI/LanguageSubmenu.cs
Assets/Scripts/UI/LoadingBar.cs
Assets/Scripts/UI/LoadingScreenController.cs
Assets/Scripts/UI/LoadingTipCatalog.cs
Assets/Scripts/UI/LoadingTipSequencer.cs
Assets/Scripts/UI/LoadingTipStore.cs
Assets/Scripts/UI/Loans/Editor/LoanDemoRecorder.cs
Assets/Scripts/UI/Loans/Editor/LoanPolishProbe.cs
Assets/Scripts/UI/Loans/Editor/LoanSettingsLiveCheck.cs
Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs
Assets/Scripts/UI/Loans/Editor/LoanUiCaptureBot.cs
Assets/Scripts/UI/Loans/LoanBadgeView.cs
Assets/Scripts/UI/Loans/LoanModalController.cs
Assets/Scripts/UI/Loans/LoanOfferModalController.cs
Assets/Scripts/UI/Loans/LoanOffersToggle.cs
Assets/Scripts/UI/Loans/LoanRecipientRow.cs
Assets/Scripts/UI/Loans/LoanRescindModalController.cs
Assets/Scripts/UI/Loans/LoanReturnModalController.cs
Assets/Scripts/UI/Loans/LoanRibbonView.cs
Assets/Scripts/UI/LogoScreenController.cs
Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs
Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs
Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs
Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs
Assets/Scripts/UI/MissionSelection/HoleMapCalibration.cs
Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs
Assets/Scripts/UI/MissionSelection/MissionCardController.cs
Assets/Scripts/UI/MissionSelection/MissionLauncher.cs
Assets/Scripts/UI/MissionSelection/MissionLoadoutResolver.cs
Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs
Assets/Scripts/UI/Modals/Editor/SchemeConfirmModalBuilder.cs
Assets/Scripts/UI/Modals/InGameSettingsModalController.cs
Assets/Scripts/UI/Modals/ModalBackdropDismiss.cs
Assets/Scripts/UI/Modals/ModalController.cs
Assets/Scripts/UI/Modals/ModalScrim.cs
Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs
Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs
Assets/Scripts/UI/Modals/VersusResultHandler.cs
Assets/Scripts/UI/ModeSelect/Editor/ModeCardPreview.cs
Assets/Scripts/UI/ModeSelect/Editor/ModesFallbackSync.cs
Assets/Scripts/UI/ModeSelect/ModeCardController.cs
Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs
Assets/Scripts/UI/ModeSelect/ModeData.cs
Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs
Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs
Assets/Scripts/UI/PersistentUIManager.cs
Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs
Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorder.cs
Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorderB.cs
Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorderC.cs
Assets/Scripts/UI/Polish/Editor/GamePolishProbe.cs
Assets/Scripts/UI/Polish/Editor/GamePolishProbeB.cs
Assets/Scripts/UI/Polish/Editor/GamePolishProbeC.cs
Assets/Scripts/UI/Polish/Editor/GamePolishTestReport.cs
Assets/Scripts/UI/Polish/Editor/GpsNavStillCapture.cs
Assets/Scripts/UI/Polish/Editor/PressFeedbackScope.cs
Assets/Scripts/UI/Polish/Editor/RetrofitParityRecorder.cs
Assets/Scripts/UI/Polish/GameShimmerSites.cs
Assets/Scripts/UI/Polish/KeyboardInset.cs
Assets/Scripts/UI/Polish/LayeredPush.cs
Assets/Scripts/UI/Polish/ModalNumbers.cs
Assets/Scripts/UI/Polish/NavSlotHighlight.cs
Assets/Scripts/UI/Polish/PaintMotion.cs
Assets/Scripts/UI/Polish/PendingSpend.cs
Assets/Scripts/UI/Polish/ResultChoreography.cs
Assets/Scripts/UI/Polish/ScreenEntryMotion.cs
Assets/Scripts/UI/Polish/ShimmerHost.cs
Assets/Scripts/UI/Polish/Tests/CenterTitleDissolveTests.cs
Assets/Scripts/UI/Polish/Tests/CountDownTests.cs
Assets/Scripts/UI/Polish/Tests/GpsPolishMotionTests.cs
Assets/Scripts/UI/Polish/Tests/GpsScreenTransitionTests.cs
Assets/Scripts/UI/Polish/Tests/LayeredPushTests.cs
Assets/Scripts/UI/Polish/Tests/ModalNumbersTests.cs
Assets/Scripts/UI/Polish/Tests/ModalPopTests.cs
Assets/Scripts/UI/Polish/Tests/PendingSpendTests.cs
Assets/Scripts/UI/Polish/Tests/PressFeedbackCoverageTests.cs
Assets/Scripts/UI/Polish/Tests/RpArmingTests.cs
Assets/Scripts/UI/Polish/Tests/ScreenEntryMotionTests.cs
Assets/Scripts/UI/Polish/Tests/ScrollFeelTests.cs
Assets/Scripts/UI/Polish/Tests/ShimmerHostTests.cs
Assets/Scripts/UI/Polish/Tests/ToastFadeParityTests.cs
Assets/Scripts/UI/Polish/Tests/UiMotionEaseTests.cs
Assets/Scripts/UI/Polish/Tests/UiMotionTests.cs
Assets/Scripts/UI/Polish/UiMotion.cs
Assets/Scripts/UI/Polish/UiMotionRunner.cs
Assets/Scripts/UI/Polish/UiSelection.cs
Assets/Scripts/UI/ProTipCard.cs
Assets/Scripts/UI/Rankings/BackendLeaderboardProvider.cs
Assets/Scripts/UI/Rankings/Core/ILeaderboardProvider.cs
Assets/Scripts/UI/Rankings/Core/ITimeProvider.cs
Assets/Scripts/UI/Rankings/Core/LeaderboardPeriodKey.cs
Assets/Scripts/UI/Rankings/GolfinCharacterSync.cs
Assets/Scripts/UI/Rankings/LeaderboardDtos.cs
Assets/Scripts/UI/Rankings/LeaderboardManager.cs
Assets/Scripts/UI/Rankings/LeaderboardProviderPolicy.cs
Assets/Scripts/UI/Rankings/LocalFakeLeaderboardProvider.cs
Assets/Scripts/UI/Rankings/NetworkTimeProvider.cs
Assets/Scripts/UI/Rankings/RankingsCardWidget.cs
Assets/Scripts/UI/Rankings/RankingsScreenController.cs
Assets/Scripts/UI/Rankings/Tests/BackendLeaderboardTests.cs
Assets/Scripts/UI/Rankings/Tests/LeaderboardTests.cs
Assets/Scripts/UI/Rankings/Top3CardWidget.cs
Assets/Scripts/UI/RewardGranter.cs
Assets/Scripts/UI/Roster/Data/AutomaticStatAllocation.cs
Assets/Scripts/UI/Roster/Data/CharacterLevelUpData.cs
Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs
Assets/Scripts/UI/Roster/Data/ManualSPAllocation.cs
Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs
Assets/Scripts/UI/Roster/Data/RarityStatCaps.cs
Assets/Scripts/UI/Roster/Data/StatAllocationStrategy.cs
Assets/Scripts/UI/Roster/Editor/CompareAutoWire.cs
Assets/Scripts/UI/Roster/Editor/CompareRightPanelDiffBuilder.cs
Assets/Scripts/UI/Roster/Editor/DetailPanelAutoWire.cs
Assets/Scripts/UI/Roster/Editor/LevelUpModalAutoWire.cs
Assets/Scripts/UI/Roster/Editor/PaginationDotSetup.cs
Assets/Scripts/UI/Roster/Editor/RosterDebugTools.cs
Assets/Scripts/UI/Roster/Editor/StaminaLiveMeterDemoMenu.cs
Assets/Scripts/UI/Roster/Editor/StatusIconBuilder.cs
Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs
Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs
Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs
Assets/Scripts/UI/Roster/UI/CarouselController.cs
Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs
Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs
Assets/Scripts/UI/Roster/UI/CompareController.cs
Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs
Assets/Scripts/UI/Roster/UI/NameIconFitter.cs
Assets/Scripts/UI/Roster/UI/RosterScreenController.cs
Assets/Scripts/UI/Roster/UI/StartingCharacterConfirmModalController.cs
Assets/Scripts/UI/Roster/UI/StatBar.cs
Assets/Scripts/UI/ScreenDeactivator.cs
Assets/Scripts/UI/ScreenManager.cs
Assets/Scripts/UI/SettingsController.cs
Assets/Scripts/UI/SettingsMenuItem.cs
Assets/Scripts/UI/Shop/Editor/StoreHistoryAcceptanceRun.cs
Assets/Scripts/UI/Shop/Editor/StoreHistoryInstaller.cs
Assets/Scripts/UI/Shop/GeneralShopCard.cs
Assets/Scripts/UI/Shop/GeneralShopModel.cs
Assets/Scripts/UI/Shop/GeneralShopScreenController.cs
Assets/Scripts/UI/Shop/ShopModel.cs
Assets/Scripts/UI/Shop/ShopTransaction.cs
Assets/Scripts/UI/Shop/StaminaMenuRow.cs
Assets/Scripts/UI/Shop/StaminaShopCard.cs
Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs
Assets/Scripts/UI/Shop/StaminaShopSelectionScreenController.cs
Assets/Scripts/UI/Shop/StaminaShopSession.cs
Assets/Scripts/UI/Shop/StoreHistoryRecord.cs
Assets/Scripts/UI/Shop/StoreHistoryRow.cs
Assets/Scripts/UI/Shop/StoreHistoryScreenController.cs
Assets/Scripts/UI/Shop/StoreHistoryStore.cs
Assets/Scripts/UI/Shop/Tests/GeneralShopAdmitResolutionTests.cs
Assets/Scripts/UI/Shop/Tests/GeneralShopCategoryTests.cs
Assets/Scripts/UI/Shop/Tests/StaminaShopAddEnergyTests.cs
Assets/Scripts/UI/SoundSettingsSubmenu.cs
Assets/Scripts/UI/SplashScreenController.cs
Assets/Scripts/UI/StandaloneGate.cs
Assets/Scripts/UI/StandaloneShellBoot.cs
Assets/Scripts/UI/SubmenuClickBoundary.cs
Assets/Scripts/UI/SwipeDetector.cs
Assets/Scripts/UI/TapFeedbackConfig.cs
Assets/Scripts/UI/TapFeedbackController.cs
Assets/Scripts/UI/TapFeedbackFX.cs
Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs
Assets/Scripts/UI/Toast/ToastController.cs
Assets/Scripts/UI/Tournaments/TournamentCountdown.cs
Assets/Scripts/UI/Tournaments/TournamentDevEntryButton.cs
Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs
Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs
Assets/Scripts/UI/Tournaments/TournamentResultModalController.cs
Assets/Scripts/UI/Tournaments/TournamentResultPresenter.cs
Assets/Scripts/UI/Tournaments/TournamentRoundHandler.cs
Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs
Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs
Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs
Assets/Scripts/UI/UserProfileSubmenu.cs
Assets/Scripts/Utilities/RuntimeActiveStateManager.cs
Assets/Scripts/Utilities/TextGradients.cs
Assets/Scripts/Utilities/UIAutoWire.cs
```

## File Tree (Data)

```
Assets/Data/Bags.csv
Assets/Data/Bags.csv.meta
Assets/Data/Balls.csv
Assets/Data/Balls.csv.meta
Assets/Data/CREATE_DATABASE.md
Assets/Data/CREATE_DATABASE.md.meta
Assets/Data/CharacterDatabase.asset
Assets/Data/CharacterDatabase.asset.meta
Assets/Data/Characters.csv
Assets/Data/Characters.csv.meta
Assets/Data/HoleDatabase.asset
Assets/Data/HoleDatabase.asset.meta
Assets/Data/HoleDatabase.csv
Assets/Data/HoleDatabase.csv.meta
Assets/Data/HoleTees.csv
Assets/Data/HoleTees.csv.meta
Assets/Data/Items.csv
Assets/Data/Items.csv.meta
Assets/Data/LevelUpCosts.csv
Assets/Data/LevelUpCosts.csv.meta
Assets/Data/README_HOLES.md
Assets/Data/README_HOLES.md.meta
```

## MonoBehaviours

| Class | File | Singleton | Key Interfaces |
|---|---|---|---|
| AudioManager | Assets/Scripts/Audio/AudioManager.cs | Yes |  |
| SfxPlayer | Assets/Scripts/Audio/SfxPlayer.cs | Yes | ISfxGates |
| AuthService | Assets/Scripts/Auth/AuthService.cs | Yes |  |
| BagDatabaseCSV | Assets/Scripts/BagDatabaseCSV.cs | Yes |  |
| BagManager | Assets/Scripts/BagManager.cs | Yes |  |
| BallManager | Assets/Scripts/BallManager.cs | Yes |  |
| BannerService | Assets/Scripts/BannersRuntime/BannerService.cs | Yes |  |
| BannerSlotBinder | Assets/Scripts/BannersRuntime/BannerSlotBinder.cs | Yes |  |
| CharacterManager | Assets/Scripts/CharacterManager.cs | Yes |  |
| ClubManager | Assets/Scripts/ClubManager.cs | Yes |  |
| ContentService | Assets/Scripts/ContentRuntime/ContentService.cs | Yes |  |
| BridgeAnchor | Assets/Scripts/Course/BridgeAnchor.cs |  |  |
| BunkerSurfaceInfo | Assets/Scripts/Course/BunkerSurfaceInfo.cs |  |  |
| GreenSurfaceInfo | Assets/Scripts/Course/GreenSurfaceInfo.cs |  |  |
| SurfaceMarker | Assets/Scripts/Course/SurfaceMarker.cs |  |  |
| RewardPointsDebugPanel | Assets/Scripts/Debug/RewardPointsDebugPanel.cs | Yes |  |
| ScreenshotHelper | Assets/Scripts/Debug/ScreenshotCapture/ScreenshotHelper.cs |  |  |
| WalkCamera | Assets/Scripts/Debug/WalkCamera.cs |  |  |
| DevFpsOverlay | Assets/Scripts/Dev/DevFpsOverlay.cs |  |  |
| PerfBaselineBot | Assets/Scripts/Dev/PerfBaselineBot.cs | Yes |  |
| LoanSyncBehaviour | Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs | Yes | ILoanReconciler |
| ServerBalanceSyncBehaviour | Assets/Scripts/EconomyRuntime/ServerBalanceSyncBehaviour.cs | Yes | IServerBalanceSink |
| GreenDiagGizmoHost | Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.cs |  |  |
| DevAutoSignInRunner | Assets/Scripts/Editor/DevTools/DevAutoSignIn.cs | Yes |  |
| DailyClearHost | Assets/Scripts/Editor/Missions/DailyClearHarness.cs | Yes |  |
| TournamentLoopClearHelper | Assets/Scripts/Editor/Tournaments/TournamentLoopCaptureHarness.cs | Yes |  |
| TournamentLoopBotHost | Assets/Scripts/Editor/Tournaments/TournamentLoopCaptureHarness.cs | Yes |  |
| GolferPresenter | Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs | Yes |  |
| GolferPresenter | Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs | Yes |  |
| GolferTestBootstrap | Assets/Scripts/Gameplay/Golfer/GolferTestBootstrap.cs |  |  |
| GolferTestBootstrap | Assets/Scripts/Gameplay/Golfer/GolferTestBootstrap.cs |  |  |
| DebugShotInputSource | Assets/Scripts/Gameplay/Input/DebugShotInputSource.cs |  | IShotInputSource |
| InputSystemSource | Assets/Scripts/Gameplay/Input/InputSystemSource.cs |  | IShotInputSource |
| ShotConeTestDriver | Assets/Scripts/Gameplay/Input/ShotConeTestDriver.cs |  |  |
| ShotController | Assets/Scripts/Gameplay/Input/ShotController.cs |  |  |
| ActionButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonWidget.cs |  |  |
| ActionButtonsRoot | Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsRoot.cs |  |  |
| BallConeAlphaMirror | Assets/Scripts/Gameplay/UI/ShotUI/BallConeAlphaMirror.cs |  |  |
| CentralBallWidget | Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs |  | IPointerClickHandler |
| ClubHandleDragger | Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs |  |  |
| ClubHandleSpriteBinder | Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleSpriteBinder.cs |  |  |
| ConeAlphaController | Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs |  |  |
| FlickGradePopBinder | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickGradePopBinder.cs |  |  |
| FlickSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickSchemeDriver.cs |  | IShotSchemeDriver |
| FreeSwingAnalyzerChip | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingAnalyzerChip.cs |  |  |
| FreeSwingSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingSchemeDriver.cs |  | IShotSchemeDriver |
| FreeSwingTraceView | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceView.cs |  |  |
| NeedleSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleSchemeDriver.cs |  | IShotSchemeDriver |
| NeedleTapCatcher | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleTapCatcher.cs |  | IPointerDownHandler |
| PendulumFadingView | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumFadingView.cs |  |  |
| PendulumSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumSchemeDriver.cs |  | IShotSchemeDriver |
| PlaceholderSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/PlaceholderSchemeDriver.cs |  | IShotSchemeDriver |
| SchemeGradePop | Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeGradePop.cs | Yes |  |
| ShotSchemeHost | Assets/Scripts/Gameplay/UI/ShotUI/Controls/ShotSchemeHost.cs | Yes |  |
| DebugShotPanel | Assets/Scripts/Gameplay/UI/ShotUI/DebugShotPanel.cs |  |  |
| HoleCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs |  |  |
| HoleCompleteCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs |  |  |
| HoleCompleteWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs |  |  |
| HoleIndicatorWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs |  |  |
| MapTargetReadoutWidget | Assets/Scripts/Gameplay/UI/ShotUI/MapTargetReadoutWidget.cs |  |  |
| MapViewCaptureDriver | Assets/Scripts/Gameplay/UI/ShotUI/MapViewCaptureDriver.cs | Yes |  |
| MapViewController | Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs | Yes |  |
| OtherButtonsFader | Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs |  |  |
| OutsideClickCatcher | Assets/Scripts/Gameplay/UI/ShotUI/OutsideClickCatcher.cs |  | IPointerClickHandler |
| PlayerCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs |  |  |
| PowerGaugeWidget | Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs |  |  |
| SelectorCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs |  |  |
| SelectorCarouselDrag | Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselDrag.cs |  |  |
| SelectorDragRouter | Assets/Scripts/Gameplay/UI/ShotUI/SelectorDragRouter.cs |  |  |
| SelectorOverlayWidget | Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs |  |  |
| SettingsButton | Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs |  |  |
| ShotConeView | Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs |  |  |
| ShotInProgressUiGate | Assets/Scripts/Gameplay/UI/ShotUI/ShotInProgressUiGate.cs |  |  |
| ShotLayoutController | Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs | Yes |  |
| SpinPanelWidget | Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs |  | IPointerDownHandler, IDragHandler |
| TeeIdleGlowController | Assets/Scripts/Gameplay/UI/ShotUI/TeeIdleGlowController.cs |  |  |
| TurnBannerWidget | Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs |  |  |
| VersusHudController | Assets/Scripts/Gameplay/UI/ShotUI/VersusHudController.cs |  |  |
| WindIndicatorWidget | Assets/Scripts/Gameplay/UI/ShotUI/WindIndicatorWidget.cs |  |  |
| HoleMetadata | Assets/Scripts/HoleMetadata.cs |  |  |
| InventoryCatalogAdapter | Assets/Scripts/InventoryCatalogAdapter.cs | Yes | IInventoryCatalog |
| InventorySyncBehaviour | Assets/Scripts/InventorySync/InventorySyncBehaviour.cs | Yes |  |
| ItemManager | Assets/Scripts/ItemManager.cs | Yes |  |
| LiveStatProviderHost | Assets/Scripts/LiveStatProviderHost.cs | Yes |  |
| NetCoroutineRunner | Assets/Scripts/Net/ICoroutineRunner.cs | Yes | ICoroutineRunner |
| NoticeService | Assets/Scripts/NoticesRuntime/NoticeService.cs | Yes |  |
| HeightProvider | Assets/Scripts/Physics/Runtime/HeightProvider.cs |  |  |
| Phase1TestController | Assets/Scripts/Physics/Runtime/Phase1TestController.cs |  |  |
| PhaseTestController | Assets/Scripts/Physics/Runtime/PhaseTestController.cs |  |  |
| SurfaceMarker | Assets/Scripts/Physics/Runtime/SurfaceMarker.cs |  |  |
| CaptureRunner | Assets/Scripts/Physics/Tests/Editor/Iter4ShotCapture.cs |  |  |
| BallAnimator | Assets/Scripts/Physics/Viewer/BallAnimator.cs | Yes |  |
| BallAudioEmitter | Assets/Scripts/Physics/Viewer/BallAudioEmitter.cs |  |  |
| BallTrailController | Assets/Scripts/Physics/Viewer/BallTrailController.cs |  |  |
| LoopV2SmokeBot | Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs |  |  |
| ObBoundaryCaptureBot | Assets/Scripts/Physics/Viewer/Bot/ObBoundaryCaptureBot.cs | Yes |  |
| ObRecoveryCaptureBot | Assets/Scripts/Physics/Viewer/Bot/ObRecoveryCaptureBot.cs | Yes |  |
| VersusHudCaptureBot | Assets/Scripts/Physics/Viewer/Bot/VersusHudCaptureBot.cs |  |  |
| VersusHudNavCaptureBot | Assets/Scripts/Physics/Viewer/Bot/VersusHudNavCaptureBot.cs | Yes |  |
| WaterSplashCaptureRig | Assets/Scripts/Physics/Viewer/Bot/WaterSplashCaptureRig.cs | Yes |  |
| ZoneBakeAfterClipBot | Assets/Scripts/Physics/Viewer/Bot/ZoneBakeAfterClipBot.cs | Yes |  |
| CameraModeDebugHUD | Assets/Scripts/Physics/Viewer/CameraModeDebugHUD.cs |  |  |
| ChaseCamera | Assets/Scripts/Physics/Viewer/ChaseCamera.cs |  | IModeSetter |
| DashboardUI | Assets/Scripts/Physics/Viewer/DashboardUI.cs |  |  |
| GreenTuningPanel | Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs |  |  |
| HoleCompleteDriver | Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs |  | Golfin.Gameplay.UI.ShotUI.IHoleOutTrigger |
| HoleCompletionBridge | Assets/Scripts/Physics/Viewer/HoleCompletionBridge.cs |  |  |
| HoleSessionDriver | Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs |  |  |
| LabHoleBinder | Assets/Scripts/Physics/Viewer/LabHoleBinder.cs |  |  |
| LandingBannerController | Assets/Scripts/Physics/Viewer/LandingBannerController.cs |  |  |
| LoopCameraDirector | Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs |  |  |
| ObGroundSkirt | Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs |  |  |
| PhysicsLabController | Assets/Scripts/Physics/Viewer/PhysicsLabController.cs | Yes |  |
| PhysicsLabUI | Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs |  |  |
| PutterAimLine | Assets/Scripts/Physics/Viewer/PutterAimLine.cs |  |  |
| PutterConeSmokeCapture | Assets/Scripts/Physics/Viewer/PutterConeSmokeCapture.cs |  |  |
| PutterGreenReader | Assets/Scripts/Physics/Viewer/PutterGreenReader.cs |  |  |
| SmokeCaptureCupSpeedGate | Assets/Scripts/Physics/Viewer/SmokeCaptureCupSpeedGate.cs | Yes |  |
| SmokeRunner2cHost | Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs |  |  |
| SmokeRunner2dHost | Assets/Scripts/Physics/Viewer/SmokeRunner2dHost.cs |  |  |
| SmokeRunner2eHost | Assets/Scripts/Physics/Viewer/SmokeRunner2eHost.cs |  |  |
| SmokeRunner2fHost | Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs | Yes |  |
| SurfaceRolloutHarness | Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs |  |  |
| TestGreenLabSetup | Assets/Scripts/Physics/Viewer/TestGreenLabSetup.cs |  |  |
| TrajectoryRenderer | Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs |  |  |
| VersusBot | Assets/Scripts/Physics/Viewer/VersusBot.cs |  |  |
| VersusMatchController | Assets/Scripts/Physics/Viewer/VersusMatchController.cs |  |  |
| WaterSplashController | Assets/Scripts/Physics/Viewer/WaterSplashController.cs | Yes |  |
| SaveDataHost | Assets/Scripts/Save/SaveDataHost.cs | Yes |  |
| ManualPropId | Assets/Scripts/SceneSnapshot/ManualPropId.cs |  |  |
| TelemetryBehaviour | Assets/Scripts/Telemetry/TelemetryBehaviour.cs | Yes |  |
| TournamentService | Assets/Scripts/TournamentsRuntime/TournamentService.cs | Yes |  |
| AboutSubmenu | Assets/Scripts/UI/AboutSubmenu.cs |  |  |
| CreateUsernameScreenController | Assets/Scripts/UI/Account/CreateUsernameScreenController.cs | Yes |  |
| EmailConfirmationScreenController | Assets/Scripts/UI/Account/EmailConfirmationScreenController.cs | Yes |  |
| LoginScreenController | Assets/Scripts/UI/Account/LoginScreenController.cs | Yes |  |
| ResetPasswordScreenController | Assets/Scripts/UI/Account/ResetPasswordScreenController.cs | Yes |  |
| SignUpScreenController | Assets/Scripts/UI/Account/SignUpScreenController.cs | Yes |  |
| BuildStamp | Assets/Scripts/UI/BuildInfo/BuildStamp.cs |  |  |
| ButtonPressFeedback | Assets/Scripts/UI/ButtonPressFeedback.cs |  | IPointerDownHandler |
| StreakFlameView | Assets/Scripts/UI/Common/StreakFlameView.cs |  |  |
| ControlsSubmenu | Assets/Scripts/UI/ControlsSubmenu.cs | Yes |  |
| SafeAreaFitter | Assets/Scripts/UI/Core/SafeAreaFitter.cs |  |  |
| ClubControlArrowDemoRunner | Assets/Scripts/UI/Editor/ClubControlArrowDemoRecorder.cs | Yes |  |
| ClubRosterDemoRunner | Assets/Scripts/UI/Editor/ClubRosterDemoRecorder.cs | Yes |  |
| DailyMissionPillDemoRunner | Assets/Scripts/UI/Editor/DailyMissionPillDemoRecorder.cs | Yes |  |
| DailyMissionPillVerifier | Assets/Scripts/UI/Editor/DailyMissionPillDemoRecorder.cs | Yes |  |
| DemoShowcaseRunner | Assets/Scripts/UI/Editor/DemoShowcaseRecorder.cs | Yes |  |
| FlickPullMappingRunner | Assets/Scripts/UI/Editor/FlickPullMappingVerify.cs | Yes |  |
| FlickShotViewRunner | Assets/Scripts/UI/Editor/FlickShotViewVerify.cs | Yes |  |
| GachaDemoRunner | Assets/Scripts/UI/Editor/GachaDemoRecorder.cs | Yes |  |
| GachaRevealDemoRunner | Assets/Scripts/UI/Editor/GachaRevealDemoRecorder.cs | Yes |  |
| GameplayLocalizationDemoRunner | Assets/Scripts/UI/Editor/GameplayLocalizationDemoRecorder.cs | Yes |  |
| GeneralShopDemoRunner | Assets/Scripts/UI/Editor/GeneralShopDemoRecorder.cs | Yes |  |
| GolferTestDemoRunner | Assets/Scripts/UI/Editor/GolferTestDemoRecorder.cs | Yes |  |
| GolferTestVerificationRunner | Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs | Yes |  |
| GpsFlowDemoRunner | Assets/Scripts/UI/Editor/GpsFlowDemoRecorder.cs | Yes |  |
| InGameSettingsDemoRunner | Assets/Scripts/UI/Editor/InGameSettingsDemoRecorder.cs | Yes |  |
| LanguageSwitchDemoRunner | Assets/Scripts/UI/Editor/LanguageSwitchDemoRecorder.cs | Yes |  |
| LoadingTipsDemoRunner | Assets/Scripts/UI/Editor/LoadingTipsDemoRecorder.cs | Yes |  |
| MapViewStrictCropDemoRunner | Assets/Scripts/UI/Editor/MapViewStrictCropDemoRecorder.cs | Yes |  |
| MissDuffFlickRunner | Assets/Scripts/UI/Editor/MissDuffFlickVerify.cs | Yes |  |
| MissionsDemoRunner | Assets/Scripts/UI/Editor/MissionsDemoRecorder.cs | Yes |  |
| NoticeSlideDemoRunner | Assets/Scripts/UI/Editor/NoticeSlideDemoRecorder.cs | Yes |  |
| PaginationDotsDemoRunner | Assets/Scripts/UI/Editor/PaginationDotsDemoRecorder.cs | Yes |  |
| PowerGaugeMarkerDemoRunner | Assets/Scripts/UI/Editor/PowerGaugeMarkerDemoRecorder.cs | Yes |  |
| PowerGaugeMarkerVerifyRunner | Assets/Scripts/UI/Editor/PowerGaugeMarkerVerifyBot.cs | Yes |  |
| PracticeMapDuringShotRunner | Assets/Scripts/UI/Editor/PracticeMapDuringShotDemoRecorder.cs | Yes |  |
| PutterSelectorVerifyRunner | Assets/Scripts/UI/Editor/PutterSelectorVerify.cs | Yes |  |
| QualityTierDemoRunner | Assets/Scripts/UI/Editor/QualityTierDemoRecorder.cs | Yes |  |
| QualityTierVerificationRunner | Assets/Scripts/UI/Editor/QualityTierVerificationRecorder.cs | Yes |  |
| QuitTransitionDemoRunner | Assets/Scripts/UI/Editor/QuitTransitionDemoRecorder.cs | Yes |  |
| RankingsDemoRunner | Assets/Scripts/UI/Editor/RankingsDemoRecorder.cs | Yes |  |
| RosterLocalizationDemoRunner | Assets/Scripts/UI/Editor/RosterLocalizationDemoRecorder.cs | Yes |  |
| ShotAimParityRunner | Assets/Scripts/UI/Editor/ShotAimParityDemoRecorder.cs | Yes |  |
| ShotTimingTelemetryRunner | Assets/Scripts/UI/Editor/ShotTimingTelemetryVerify.cs | Yes |  |
| SkyRotationDemoRunner | Assets/Scripts/UI/Editor/SkyRotationDemoRecorder.cs | Yes |  |
| StaminaMeterDemoRunner | Assets/Scripts/UI/Editor/StaminaLiveMeterDemoRecorder.cs | Yes |  |
| StaminaShopDemoRunner | Assets/Scripts/UI/Editor/StaminaShopDemoRecorder.cs | Yes |  |
| StarterSelectionDemoRunner | Assets/Scripts/UI/Editor/StarterSelectionDemoRecorder.cs | Yes |  |
| StoreHistoryDemoRunner | Assets/Scripts/UI/Editor/StoreHistoryDemoRecorder.cs | Yes |  |
| TapFXDemoRunner | Assets/Scripts/UI/Editor/TapFeedbackDemoRecorder.cs | Yes |  |
| TeeIdleGlowDemoRunner | Assets/Scripts/UI/Editor/TeeIdleGlowDemoRecorder.cs | Yes |  |
| TournamentBannerDemoRunner | Assets/Scripts/UI/Editor/TournamentBannerDemoRecorder.cs | Yes |  |
| TournamentDemoRunner | Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs | Yes |  |
| TournamentDeniedDemoRunner | Assets/Scripts/UI/Editor/TournamentDeniedDemoRecorder.cs | Yes |  |
| TournamentsModeCardDemoRunner | Assets/Scripts/UI/Editor/TournamentsModeCardDemoRecorder.cs | Yes |  |
| TreeOccludeFadeCaptureRunner | Assets/Scripts/UI/Editor/TreeOccludeFadeCaptureBot.cs | Yes |  |
| FadeController | Assets/Scripts/UI/FadeController.cs | Yes |  |
| GachaBannerCard | Assets/Scripts/UI/Gacha/GachaBannerCard.cs | Yes |  |
| GachaCarouselController | Assets/Scripts/UI/Gacha/GachaCarouselController.cs | Yes | IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler |
| GachaHistoryRow | Assets/Scripts/UI/Gacha/GachaHistoryRow.cs | Yes |  |
| GachaHistoryRowBall | Assets/Scripts/UI/Gacha/GachaHistoryRowBall.cs | Yes |  |
| GachaHistoryScreenController | Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs | Yes |  |
| GachaHistoryTabStrip | Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs | Yes |  |
| GachaPrizesScreenController | Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs | Yes |  |
| GachaTabController | Assets/Scripts/UI/Gacha/GachaTabController.cs | Yes |  |
| GachaTicketManager | Assets/Scripts/UI/Gacha/GachaTicketManager.cs | Yes |  |
| GameplaySceneLoader | Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs | Yes |  |
| BadgeCellView | Assets/Scripts/UI/Gps/BadgeCellView.cs |  |  |
| Runner | Assets/Scripts/UI/Gps/Editor/GpsAuthExtrasEditorRun.cs | Yes |  |
| Driver | Assets/Scripts/UI/Gps/Editor/GpsGiftVoteEditorRun.cs | Yes |  |
| Driver | Assets/Scripts/UI/Gps/Editor/GpsPolishProbe.cs | Yes |  |
| Runner | Assets/Scripts/UI/Gps/Editor/GpsProfilePromptOnEntryRun.cs | Yes |  |
| Driver | Assets/Scripts/UI/Gps/Editor/ScoreUploadEditorRun.cs | Yes |  |
| GpsAvatarScreenController | Assets/Scripts/UI/Gps/GpsAvatarScreenController.cs | Yes |  |
| GpsBadgesScreenController | Assets/Scripts/UI/Gps/GpsBadgesScreenController.cs | Yes |  |
| GpsGiftScreenController | Assets/Scripts/UI/Gps/GpsGiftScreenController.cs | Yes |  |
| GpsGolfProfileScreenController | Assets/Scripts/UI/Gps/GpsGolfProfileScreenController.cs | Yes |  |
| GpsHubRoundRow | Assets/Scripts/UI/Gps/GpsHubRoundRow.cs |  |  |
| GpsHubScreenController | Assets/Scripts/UI/Gps/GpsHubScreenController.cs | Yes |  |
| GpsNavBarBinder | Assets/Scripts/UI/Gps/GpsNavBarBinder.cs | Yes |  |
| GpsNavBarHighlight | Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs | Yes |  |
| GpsProfileScreenController | Assets/Scripts/UI/Gps/GpsProfileScreenController.cs | Yes |  |
| GpsRoundsScreenController | Assets/Scripts/UI/Gps/GpsRoundsScreenController.cs | Yes |  |
| GpsScreenEntryMotion | Assets/Scripts/UI/Gps/GpsScreenEntryMotion.cs |  |  |
| GpsVoteScreenController | Assets/Scripts/UI/Gps/GpsVoteScreenController.cs | Yes |  |
| GpsWelcomeScreenController | Assets/Scripts/UI/Gps/GpsWelcomeScreenController.cs | Yes |  |
| HoleRowView | Assets/Scripts/UI/Gps/HoleRowView.cs |  |  |
| RoundSpotRowView | Assets/Scripts/UI/Gps/RoundSpotRowView.cs |  |  |
| ScoreUploadFlowController | Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs | Yes |  |
| ShimmerBlock | Assets/Scripts/UI/Gps/ShimmerBlock.cs |  |  |
| VoteCardView | Assets/Scripts/UI/Gps/VoteCardView.cs |  |  |
| GraphicsSubmenu | Assets/Scripts/UI/GraphicsSubmenu.cs |  |  |
| BallContextPopulator | Assets/Scripts/UI/HUD/BallContextPopulator.cs | Yes |  |
| ClubContextPopulator | Assets/Scripts/UI/HUD/ClubContextPopulator.cs | Yes |  |
| LabInventoryStub | Assets/Scripts/UI/HUD/LabInventoryStub.cs | Yes |  |
| PlayerContextPopulator | Assets/Scripts/UI/HUD/PlayerContextPopulator.cs | Yes |  |
| ScreenHintVerifyRunner | Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs | Yes |  |
| ScreenHintPresenter | Assets/Scripts/UI/Hints/ScreenHintPresenter.cs | Yes |  |
| HoleDatabaseLoader | Assets/Scripts/UI/HoleDatabaseLoader.cs | Yes |  |
| SmokeTestMonoBehaviour | Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionSmokeRunner.cs | Yes |  |
| HoleCardController | Assets/Scripts/UI/HoleSelection/HoleCardController.cs | Yes |  |
| HoleProgressionDebug | Assets/Scripts/UI/HoleSelection/HoleProgressionDebug.cs | Yes |  |
| HoleSelectionScreenController | Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs | Yes |  |
| DailyMissionPillController | Assets/Scripts/UI/Home/DailyMissionPillController.cs | Yes |  |
| LoanOfferPillController | Assets/Scripts/UI/Home/LoanOfferPillController.cs | Yes |  |
| NoticePageSlider | Assets/Scripts/UI/Home/NoticePageSlider.cs |  |  |
| HomeScreenController | Assets/Scripts/UI/HomeScreenController.cs | Yes |  |
| BagCarouselController | Assets/Scripts/UI/Inventory/BagCarouselController.cs | Yes |  |
| BagClubCard | Assets/Scripts/UI/Inventory/BagClubCard.cs |  |  |
| BagDetailPanel | Assets/Scripts/UI/Inventory/BagDetailPanel.cs | Yes |  |
| BagThumbnailCard | Assets/Scripts/UI/Inventory/BagThumbnailCard.cs |  |  |
| BallCarouselController | Assets/Scripts/UI/Inventory/BallCarouselController.cs | Yes |  |
| BallCompareController | Assets/Scripts/UI/Inventory/BallCompareController.cs | Yes |  |
| BallDatabaseCSV | Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs | Yes |  |
| BallDetailPanel | Assets/Scripts/UI/Inventory/BallDetailPanel.cs | Yes |  |
| BallSegmentedBar | Assets/Scripts/UI/Inventory/BallSegmentedBar.cs |  |  |
| BallThumbnailCard | Assets/Scripts/UI/Inventory/BallThumbnailCard.cs | Yes |  |
| BallThumbnailEmptyCard | Assets/Scripts/UI/Inventory/BallThumbnailEmptyCard.cs |  |  |
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | Yes |  |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | Yes |  |
| ClubDatabaseCSV | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | Yes |  |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | Yes |  |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs |  |  |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | Yes |  |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs |  |  |
| ItemCarouselController | Assets/Scripts/UI/Inventory/ItemCarouselController.cs | Yes |  |
| ItemDatabaseCSV | Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs | Yes |  |
| ItemDetailPanel | Assets/Scripts/UI/Inventory/ItemDetailPanel.cs | Yes |  |
| ItemThumbnailCard | Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs | Yes |  |
| ItemUseClubCard | Assets/Scripts/UI/Inventory/ItemUseClubCard.cs |  |  |
| LanguageSubmenu | Assets/Scripts/UI/LanguageSubmenu.cs |  |  |
| LoadingBar | Assets/Scripts/UI/LoadingBar.cs |  |  |
| LoadingScreenController | Assets/Scripts/UI/LoadingScreenController.cs |  |  |
| LoanDemoRunner | Assets/Scripts/UI/Loans/Editor/LoanDemoRecorder.cs | Yes | ICoroutineRunner |
| LoanPolishRunner | Assets/Scripts/UI/Loans/Editor/LoanPolishProbe.cs | Yes | ICoroutineRunner |
| LoanSettingsLiveRunner | Assets/Scripts/UI/Loans/Editor/LoanSettingsLiveCheck.cs | Yes |  |
| LoanCaptureRunner | Assets/Scripts/UI/Loans/Editor/LoanUiCaptureBot.cs | Yes | ICoroutineRunner |
| LoanBadgeView | Assets/Scripts/UI/Loans/LoanBadgeView.cs |  |  |
| LoanOffersToggle | Assets/Scripts/UI/Loans/LoanOffersToggle.cs | Yes |  |
| LoanRecipientRow | Assets/Scripts/UI/Loans/LoanRecipientRow.cs |  |  |
| LoanRibbonView | Assets/Scripts/UI/Loans/LoanRibbonView.cs |  |  |
| LogoScreenController | Assets/Scripts/UI/LogoScreenController.cs |  |  |
| VersusResultScreenController | Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs | Yes |  |
| MissionCardController | Assets/Scripts/UI/MissionSelection/MissionCardController.cs |  |  |
| MissionSelectionScreenController | Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs | Yes |  |
| ModalBackdropDismiss | Assets/Scripts/UI/Modals/ModalBackdropDismiss.cs |  | IPointerClickHandler |
| ModalController | Assets/Scripts/UI/Modals/ModalController.cs |  |  |
| VersusResultHandler | Assets/Scripts/UI/Modals/VersusResultHandler.cs | Yes |  |
| ModeCardController | Assets/Scripts/UI/ModeSelect/ModeCardController.cs | Yes |  |
| ModeCarouselController | Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs | Yes |  |
| ModeSelectScreenController | Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs | Yes |  |
| ModesDatabaseCSV | Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs | Yes |  |
| PersistentUIManager | Assets/Scripts/UI/PersistentUIManager.cs | Yes |  |
| GamePolishDemoRunner | Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorder.cs | Yes |  |
| GamePolishDemoRunnerB | Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorderB.cs | Yes |  |
| GamePolishDemoRunnerC | Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorderC.cs | Yes |  |
| Driver | Assets/Scripts/UI/Polish/Editor/GamePolishProbe.cs | Yes |  |
| Driver | Assets/Scripts/UI/Polish/Editor/GamePolishProbeB.cs | Yes |  |
| Driver | Assets/Scripts/UI/Polish/Editor/GamePolishProbeC.cs | Yes |  |
| Runner | Assets/Scripts/UI/Polish/Editor/GpsNavStillCapture.cs | Yes |  |
| Driver | Assets/Scripts/UI/Polish/Editor/RetrofitParityRecorder.cs | Yes |  |
| KeyboardInsetBinder | Assets/Scripts/UI/Polish/KeyboardInset.cs |  |  |
| NavSlotHighlight | Assets/Scripts/UI/Polish/NavSlotHighlight.cs | Yes |  |
| ScreenEntryMotion | Assets/Scripts/UI/Polish/ScreenEntryMotion.cs |  |  |
| ShimmerHost | Assets/Scripts/UI/Polish/ShimmerHost.cs |  |  |
| Host | Assets/Scripts/UI/Polish/Tests/GpsPolishMotionTests.cs | Yes |  |
| Host | Assets/Scripts/UI/Polish/Tests/ModalNumbersTests.cs | Yes |  |
| DummyHost | Assets/Scripts/UI/Polish/Tests/UiMotionTests.cs |  |  |
| UiMotionRunner | Assets/Scripts/UI/Polish/UiMotionRunner.cs |  |  |
| UiFadeGeneration | Assets/Scripts/UI/Polish/UiSelection.cs |  |  |
| ProTipCard | Assets/Scripts/UI/ProTipCard.cs |  | IPointerClickHandler |
| GolfinCharacterSync | Assets/Scripts/UI/Rankings/GolfinCharacterSync.cs | Yes |  |
| LeaderboardManager | Assets/Scripts/UI/Rankings/LeaderboardManager.cs | Yes |  |
| RankingsCardWidget | Assets/Scripts/UI/Rankings/RankingsCardWidget.cs | Yes |  |
| RankingsScreenController | Assets/Scripts/UI/Rankings/RankingsScreenController.cs | Yes |  |
| Top3CardWidget | Assets/Scripts/UI/Rankings/Top3CardWidget.cs | Yes |  |
| CharacterLevelUpDatabase | Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs | Yes |  |
| CharacterDatabaseCSV | Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs | Yes |  |
| RewardPointsManager | Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs | Yes |  |
| CarouselController | Assets/Scripts/UI/Roster/UI/CarouselController.cs | Yes |  |
| CharacterDetailPanel | Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs | Yes |  |
| CharacterThumbnailCard | Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs | Yes |  |
| CompareController | Assets/Scripts/UI/Roster/UI/CompareController.cs | Yes |  |
| RosterScreenController | Assets/Scripts/UI/Roster/UI/RosterScreenController.cs | Yes |  |
| StatBar | Assets/Scripts/UI/Roster/UI/StatBar.cs |  |  |
| ScreenDeactivator | Assets/Scripts/UI/ScreenDeactivator.cs |  |  |
| ScreenManager | Assets/Scripts/UI/ScreenManager.cs | Yes |  |
| SettingsController | Assets/Scripts/UI/SettingsController.cs | Yes |  |
| SettingsMenuItem | Assets/Scripts/UI/SettingsMenuItem.cs |  |  |
| Runner | Assets/Scripts/UI/Shop/Editor/StoreHistoryAcceptanceRun.cs | Yes |  |
| GeneralShopCard | Assets/Scripts/UI/Shop/GeneralShopCard.cs | Yes |  |
| GeneralShopScreenController | Assets/Scripts/UI/Shop/GeneralShopScreenController.cs | Yes |  |
| StaminaMenuRow | Assets/Scripts/UI/Shop/StaminaMenuRow.cs |  |  |
| StaminaShopCard | Assets/Scripts/UI/Shop/StaminaShopCard.cs |  |  |
| StaminaShopDetailScreenController | Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs | Yes |  |
| StaminaShopSelectionScreenController | Assets/Scripts/UI/Shop/StaminaShopSelectionScreenController.cs | Yes |  |
| StoreHistoryRow | Assets/Scripts/UI/Shop/StoreHistoryRow.cs | Yes |  |
| StoreHistoryScreenController | Assets/Scripts/UI/Shop/StoreHistoryScreenController.cs | Yes |  |
| SoundSettingsSubmenu | Assets/Scripts/UI/SoundSettingsSubmenu.cs | Yes |  |
| SplashScreenController | Assets/Scripts/UI/SplashScreenController.cs | Yes |  |
| SubmenuClickBoundary | Assets/Scripts/UI/SubmenuClickBoundary.cs |  | IPointerClickHandler |
| SwipeDetector | Assets/Scripts/UI/SwipeDetector.cs |  | IBeginDragHandler, IEndDragHandler |
| TapFeedbackController | Assets/Scripts/UI/TapFeedbackController.cs |  |  |
| TapFeedbackFX | Assets/Scripts/UI/TapFeedbackFX.cs |  |  |
| ToastController | Assets/Scripts/UI/Toast/ToastController.cs | Yes |  |
| TournamentDevEntryButton | Assets/Scripts/UI/Tournaments/TournamentDevEntryButton.cs | Yes |  |
| TournamentHoleSelectionScreenController | Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs | Yes |  |
| TournamentLeaderboardScreenController | Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs | Yes |  |
| TournamentResultPresenter | Assets/Scripts/UI/Tournaments/TournamentResultPresenter.cs | Yes |  |
| TournamentRoundHandler | Assets/Scripts/UI/Tournaments/TournamentRoundHandler.cs | Yes |  |
| TournamentSelectionCard | Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs |  |  |
| TournamentSelectionScreenController | Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs | Yes |  |
| UserProfileSubmenu | Assets/Scripts/UI/UserProfileSubmenu.cs | Yes |  |
| RuntimeActiveStateManager | Assets/Scripts/Utilities/RuntimeActiveStateManager.cs |  |  |

## Singletons

- **AudioManager** (Assets/Scripts/Audio/AudioManager.cs) (DontDestroyOnLoad)
- **AuthService** (Assets/Scripts/Auth/AuthService.cs) (DontDestroyOnLoad)
- **BagManager** (Assets/Scripts/BagManager.cs) (DontDestroyOnLoad)
- **BallManager** (Assets/Scripts/BallManager.cs) (DontDestroyOnLoad)
- **CharacterManager** (Assets/Scripts/CharacterManager.cs) (DontDestroyOnLoad)
- **has** (Assets/Scripts/ClubManager.cs) (DontDestroyOnLoad)
- **DemoConfig** (Assets/Scripts/Demo/DemoConfig.cs) 
- **GachaPullService** (Assets/Scripts/Economy/GachaPullService.cs) 
- **MissionClaimResult** (Assets/Scripts/Economy/MissionsClient.cs) 
- **PointsService** (Assets/Scripts/Economy/PointsService.cs) 
- **ProgressService** (Assets/Scripts/Economy/ProgressService.cs) 
- **ShopPurchaseService** (Assets/Scripts/Economy/ShopPurchaseService.cs) 
- **of** (Assets/Scripts/Gameplay/Missions/MissionProgressionService.cs) 
- **ActivityService** (Assets/Scripts/Gps/ActivityService.cs) 
- **BadgeService** (Assets/Scripts/Gps/BadgeService.cs) 
- **GpsSessionTracker** (Assets/Scripts/Gps/GpsSessionTracker.cs) 
- **RecognitionService** (Assets/Scripts/Gps/RecognitionService.cs) 
- **RoundSession** (Assets/Scripts/Gps/RoundSession.cs) 
- **ScoreHistoryService** (Assets/Scripts/Gps/ScoreHistoryService.cs) 
- **merges** (Assets/Scripts/Gps/ScoreService.cs) 
- **ScoreStatsService** (Assets/Scripts/Gps/ScoreStatsService.cs) 
- **VenueService** (Assets/Scripts/Gps/VenueService.cs) 
- **InventorySyncService** (Assets/Scripts/InventorySync/InventorySyncService.cs) 
- **ItemManager** (Assets/Scripts/ItemManager.cs) (DontDestroyOnLoad)
- **ApiClient** (Assets/Scripts/Net/ApiClient.cs) 
- **NetCoroutineRunner** (Assets/Scripts/Net/ICoroutineRunner.cs) (DontDestroyOnLoad)
- **BallAnimator** (Assets/Scripts/Physics/Viewer/BallAnimator.cs) 
- **SaveDataHost** (Assets/Scripts/Save/SaveDataHost.cs) (DontDestroyOnLoad)
- **GiftService** (Assets/Scripts/Social/GiftService.cs) 
- **LoanService** (Assets/Scripts/Social/LoanService.cs) 
- **UserService** (Assets/Scripts/Social/UserService.cs) 
- **VoteService** (Assets/Scripts/Social/VoteService.cs) 
- **TelemetryBehaviour** (Assets/Scripts/Telemetry/TelemetryBehaviour.cs) (DontDestroyOnLoad)
- **TelemetryEvent** (Assets/Scripts/Telemetry/TelemetryService.cs) 
- **AsmCSharp** (Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs) (DontDestroyOnLoad)
- **TournamentArtService** (Assets/Scripts/TournamentsRuntime/TournamentArtService.cs) 
- **FadeController** (Assets/Scripts/UI/FadeController.cs) (DontDestroyOnLoad)
- **GachaCarouselController** (Assets/Scripts/UI/Gacha/GachaCarouselController.cs) 
- **GachaTicketManager** (Assets/Scripts/UI/Gacha/GachaTicketManager.cs) (DontDestroyOnLoad)
- **GameplaySceneLoader** (Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs) 
- **HoleProgressionService** (Assets/Scripts/UI/HoleSelection/HoleProgressionService.cs) 
- **SchemeConfirmModalController** (Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs) 
- **ModesDatabaseCSV** (Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs) (DontDestroyOnLoad)
- **PersistentUIManager** (Assets/Scripts/UI/PersistentUIManager.cs) (DontDestroyOnLoad)
- **NetworkTimeProvider** (Assets/Scripts/UI/Rankings/NetworkTimeProvider.cs) 
- **CharacterLevelUpDatabase** (Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs) 
- **CharacterDatabaseCSV** (Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs) (DontDestroyOnLoad)
- **RewardPointsManager** (Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs) (DontDestroyOnLoad)
- **SettingsController** (Assets/Scripts/UI/SettingsController.cs) 
- **ToastController** (Assets/Scripts/UI/Toast/ToastController.cs) 

## Events (Action delegates)

| Class | Event |
|---|---|
| SfxBus | `public static event Action<SfxId> OnPlay;` |
| AuthService | `public static event Action<AuthSession> SignedIn;` |
| AuthService | `public static event Action<AuthResult> PasswordRecovery;` |
| BagManager | `public event System.Action<int>? OnBagChanged;` |
| BagManager | `public event System.Action<int>? OnEquippedBagChanged;` |
| BallManager | `public event System.Action? OnInventoryChanged;` |
| BannerService | `public static event Action? OnBannersChanged;` |
| CharacterManager | `public event System.Action<string>? OnCharacterLeveledUp;` |
| CharacterManager | `public event System.Action<string>? OnCharacterSelected;` |
| CharacterManager | `public event System.Action? OnRosterChanged;` |
| has | `public event System.Action<string>? OnClubEquipped;` |
| has | `public event System.Action<string>? OnClubLeveledUp;` |
| has | `public event System.Action? OnInventoryChanged;` |
| has | `public event System.Action<string>? OnClubRepaired;` |
| ContentService | `public static event Action? OnCacheRefreshed;` |
| PendingOpsQueue | `public event Action OnChanged;` |
| PointsService | `public event Action<int> OnBalanceChanged;` |
| PointsService | `public event Action<int> OnDisplayBalanceChanged;` |
| ShotController | `public event Action<ShotInputState>                     OnStateChanged;` |
| ShotController | `public event Action<ShotInput, BallPhysicsModifiers>    OnShotResolved;` |
| ShotController | `public event Action<ShotInput, BallPhysicsModifiers>    OnShotResolvedImmediate;` |
| ShotController | `public static event System.Action<float> FlickRejected;` |
| ShotController | `public static event System.Action ShotCancelled;` |
| ActiveCourseContext | `public static event Action OnCourseChanged;` |
| BallStateMachine | `public event Action<BallStateChange> OnStateChanged;` |
| BallStateMachine | `public event Action<ShotResult>      OnShotComplete;` |
| GameSession | `public static event System.Action<int, int>? OnTournamentHoleComplete;` |
| GameSession | `public static event System.Action OnTurnChanged;` |
| GameSession | `public static event System.Action OnHistoryChanged;` |
| GameSession | `public static event System.Action<HoleCompletionData> OnHoleComplete;` |
| GameSession | `public static event System.Action<MatchOutcome, int, int> OnMatchComplete;` |
| GameSession | `public static event System.Action OnSessionReset;` |
| GameSession | `public static event System.Action OnRoundStarted;` |
| DailyMissionState | `public static event Action OnChanged;` |
| MissionGoalEvaluator | `public event Action? OnGoalsChanged;` |
| at | `public static event Action? OnChanged;` |
| MissionSessionBag | `public static event System.Action? OnChanged;` |
| ClubSelectionBroadcast | `public static event Action<int> OnClubChanged;` |
| ClubSelectionBroadcast | `public static event Action<bool> OnPutterModeChanged;` |
| ControlSchemeService | `public static event Action<ControlScheme> OnSchemeChanged;` |
| ControlSchemeService | `public static event Action<ControlScheme, ControlScheme, string> OnSchemeChangedDetailed;` |
| NeedleTapCatcher | `public event Action OnTapped;` |
| BallContext | `public static event Action? OnSelectedChanged;` |
| BallContext | `public static event Action? OnBagChanged;` |
| BallContext | `public static event Action<int>? OnSelectionRequested;` |
| ClubContext | `public static event Action? OnSelectedChanged;` |
| ClubContext | `public static event Action? OnBagChanged;` |
| ClubContext | `public static event Action<int>? OnSelectionRequested;  // widget → populator` |
| HoleContext | `public static event System.Action OnChanged;` |
| MatchContext | `public static event System.Action OnChanged;` |
| MatchContext | `public static event System.Action OnActiveChanged;` |
| PlayerContext | `public static event System.Action OnChanged;` |
| ShotModeContext | `public static event Action? OnChanged;` |
| SpinContext | `public static event Action? OnChanged;` |
| WindContext | `public static event System.Action OnChanged;` |
| MapViewController | `public event Action<float> OnMapOpened;` |
| MapViewController | `public event Action<float> OnMapClosed;` |
| QualityTierService | `public static event Action<QualityTier> OnTierChanged;` |
| subscribes | `public static event System.Action<float> FlickRejected;` |
| subscribes | `public static event System.Action ShotCancelled;` |
| BadgeService | `public event Action OnBadgesChanged;` |
| RoundSession | `public event Action<ActivityDto?>? OnActiveChanged;` |
| ScoreStatsService | `public event Action OnStatsChanged;` |
| InventorySyncService | `public event Action<BootOutcome>? OnBootFinished;` |
| InventorySyncService | `public event Action? OnRestored;` |
| InventorySyncService | `public static event Action<int>? OnTicketGrantsApplied;` |
| ItemManager | `public event System.Action? OnInventoryChanged;` |
| NoticeService | `public static event Action? OnNoticesChanged;` |
| BallAnimator | `public event Action<TerrainHit> OnHit;` |
| LoopCameraDirector | `public event System.Action<ChaseCamera.Mode> OnModeChanged;` |
| PhysicsLabController | `public event Action<ShotReadout> OnShotFired;` |
| PhysicsLabController | `public event Action<bool, int> OnRepeatabilityResult;` |
| PhysicsLabController | `public event Action OnPlacementEntriesChanged;` |
| PhysicsLabController | `public event System.Action<int> OnClubChanged;` |
| VersusMatchController | `public static event System.Action OnMatchReadyToBegin;` |
| SaveDataHost | `public event Action? OnSaved;` |
| GiftService | `public event Action OnItemsChanged;` |
| GiftService | `public event Action OnSupportersChanged;` |
| LoanService | `public event Action OnLoansChanged;` |
| UserService | `public event Action<UserDetailDto> OnDetailChanged;` |
| UserService | `public event Action OnDiscoverChanged;` |
| VoteService | `public event Action OnVotesChanged;` |
| TournamentArtService | `public event Action<string>? ArtCached;` |
| TournamentService | `public static event Action? OnScheduleChanged;` |
| PendingHoleSubmit | `public event Action? OnChanged;` |
| GachaHistoryStore | `public static event Action? OnChanged;` |
| GachaTicketManager | `public event System.Action<TicketType, int>? OnTicketsChanged;` |
| RoundSpotRowView | `public event Action<RoundSpotRowView>? OnAction;` |
| HoleCardController | `public event System.Action<HoleCardController> OnCardTapped;` |
| HoleCardController | `public event System.Action<HoleCardController> OnActionButtonClicked;` |
| NoticePageSlider | `public event Action<int> OnDragCommitted;` |
| BagCarouselController | `public event System.Action<int>? OnBagSelected;` |
| BagClubCard | `public event System.Action? OnActionClicked;` |
| BagThumbnailCard | `public event System.Action? OnClicked;` |
| BallCarouselController | `public event System.Action<string>? OnBallSelected;` |
| ClubCarouselController | `public event System.Action<string>? OnClubSelected;` |
| ClubFilterBar | `public event System.Action<ClubType?>? OnFilterChanged;` |
| ItemCarouselController | `public event System.Action<string>? OnItemSelected;` |
| ItemUseClubCard | `public event System.Action? OnUseRepairKit;` |
| LoanRecipientRow | `public event Action<LoanRecipientRow>? Clicked;` |
| MissionCardController | `public event System.Action<MissionCardController>? OnCardTapped;` |
| MissionCardController | `public event System.Action<MissionCardController>? OnActionButtonClicked;` |
| ModalController | `public static event System.Action? ModalStackEmptied;` |
| ModeCardController | `public event System.Action<ModeCardController> OnCardTapped;` |
| ModeCardController | `public event System.Action<ModeCardController> OnPlayClicked;` |
| ModeCardController | `public event System.Action<ModeCardController> OnTaglineTapped;` |
| RewardPointsManager | `public event System.Action<int>? OnPointsChanged;` |
| CarouselController | `public event System.Action<string> OnCharacterSelected; // Change to event` |
| ScreenManager | `public static event System.Action<ScreenId>? ScreenChanged;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnExpanded;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnCollapsed;` |
| GeneralShopCard | `public event Action<GeneralShopCard> OnBuyClicked;` |
| StaminaMenuRow | `public event Action<StaminaMenuRow> OnBuyClicked;` |
| StaminaShopCard | `public event Action<StaminaShopCard> OnCardTapped;` |
| StoreHistoryStore | `public static event Action? OnChanged;` |
| TournamentResultModalController | `public event Action<string>? OnClaimed;` |

## Serialized Fields Summary

| Class | File | SerializeField Count |
|---|---|---|
| AudioManager | Assets/Scripts/Audio/AudioManager.cs | 6 |
| SfxLibrary | Assets/Scripts/Audio/SfxLibrary.cs | 1 |
| SfxPlayer | Assets/Scripts/Audio/SfxPlayer.cs | 1 |
| BagDataRuntime | Assets/Scripts/BagDatabaseCSV.cs | 1 |
| BannerSlotBinder | Assets/Scripts/BannersRuntime/BannerSlotBinder.cs | 6 |
| CharacterManager | Assets/Scripts/CharacterManager.cs | 2 |
| WalkCamera | Assets/Scripts/Debug/WalkCamera.cs | 1 |
| BagSelectionModalAutoWire | Assets/Scripts/Editor/BagSelectionModalAutoWire.cs | 1 |
| IndicatorWidgetBuilder | Assets/Scripts/Editor/CanvasScalerMigration/IndicatorWidgetBuilder.cs | 2 |
| TreeBrushTool | Assets/Scripts/Editor/CourseImporter/TreeBrushTool.cs | 8 |
| SkyPreset | Assets/Scripts/Gameplay/Environment/SkyPreset.cs | 11 |
| SkyPresetLibrary | Assets/Scripts/Gameplay/Environment/SkyPresetLibrary.cs | 5 |
| GolferPresenter | Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs | 13 |
| InputSystemSource | Assets/Scripts/Gameplay/Input/InputSystemSource.cs | 1 |
| ShotConeTestDriver | Assets/Scripts/Gameplay/Input/ShotConeTestDriver.cs | 3 |
| ShotController | Assets/Scripts/Gameplay/Input/ShotController.cs | 7 |
| ActionButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonWidget.cs | 4 |
| ActionButtonsRoot | Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsRoot.cs | 3 |
| AimLineBendRenderer | Assets/Scripts/Gameplay/UI/ShotUI/AimLineBendRenderer.cs | 3 |
| ArrowGraphic | Assets/Scripts/Gameplay/UI/ShotUI/ArrowGraphic.cs | 3 |
| BallButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/BallButtonWidget.cs | 2 |
| BallConeAlphaMirror | Assets/Scripts/Gameplay/UI/ShotUI/BallConeAlphaMirror.cs | 4 |
| CentralBallWidget | Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs | 7 |
| ClubButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs | 2 |
| ClubHandleDragger | Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs | 7 |
| ConeAlphaController | Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs | 2 |
| ConeMeshGraphic | Assets/Scripts/Gameplay/UI/ShotUI/ConeMeshGraphic.cs | 14 |
| FlickGradePopBinder | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickGradePopBinder.cs | 2 |
| FreeSwingAnalyzerChip | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingAnalyzerChip.cs | 11 |
| FreeSwingLaneView | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingLaneView.cs | 13 |
| FreeSwingSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingSchemeDriver.cs | 12 |
| FreeSwingTraceGraphic | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceGraphic.cs | 5 |
| is | Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingTraceView.cs | 5 |
| NeedleArcGraphic | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleArcGraphic.cs | 6 |
| NeedleArcView | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleArcView.cs | 21 |
| of | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedlePowerCircleView.cs | 15 |
| NeedleSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleSchemeDriver.cs | 12 |
| NeedleTapCatcher | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleTapCatcher.cs | 1 |
| PendulumBarView | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumBarView.cs | 7 |
| of | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumFadingView.cs | 1 |
| of | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumLaneView.cs | 8 |
| PendulumSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumSchemeDriver.cs | 8 |
| PlaceholderSchemeDriver | Assets/Scripts/Gameplay/UI/ShotUI/Controls/PlaceholderSchemeDriver.cs | 1 |
| SchemeGradePop | Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeGradePop.cs | 10 |
| ShotSchemeHost | Assets/Scripts/Gameplay/UI/ShotUI/Controls/ShotSchemeHost.cs | 2 |
| DebugShotPanel | Assets/Scripts/Gameplay/UI/ShotUI/DebugShotPanel.cs | 11 |
| FadeDrawButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/FadeDrawButtonWidget.cs | 2 |
| HoleCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs | 6 |
| HoleCompleteCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs | 24 |
| HoleCompleteWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs | 4 |
| HoleIndicatorWidget | Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs | 7 |
| MapViewController | Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs | 26 |
| OtherButtonsFader | Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs | 1 |
| PlayerCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs | 9 |
| PowerGaugeGraphic | Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs | 11 |
| PowerGaugeWidget | Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs | 6 |
| PutterTrackGraphic | Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs | 10 |
| SelectorCardWidget | Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs | 6 |
| SelectorCarouselDrag | Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselDrag.cs | 3 |
| SelectorDragRouter | Assets/Scripts/Gameplay/UI/ShotUI/SelectorDragRouter.cs | 8 |
| lives | Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs | 25 |
| ShotConeView | Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs | 24 |
| ShotInProgressUiGate | Assets/Scripts/Gameplay/UI/ShotUI/ShotInProgressUiGate.cs | 8 |
| ShotLayoutController | Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs | 9 |
| SpinButtonWidget | Assets/Scripts/Gameplay/UI/ShotUI/SpinButtonWidget.cs | 1 |
| SpinPanelWidget | Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs | 9 |
| TeeIdleGlowController | Assets/Scripts/Gameplay/UI/ShotUI/TeeIdleGlowController.cs | 8 |
| TimingSlabGraphic | Assets/Scripts/Gameplay/UI/ShotUI/TimingSlabGraphic.cs | 4 |
| TurnBannerWidget | Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs | 8 |
| VersusHudController | Assets/Scripts/Gameplay/UI/ShotUI/VersusHudController.cs | 6 |
| WindIndicatorWidget | Assets/Scripts/Gameplay/UI/ShotUI/WindIndicatorWidget.cs | 2 |
| LiveStatProviderHost | Assets/Scripts/LiveStatProviderHost.cs | 1 |
| HeightProvider | Assets/Scripts/Physics/Runtime/HeightProvider.cs | 1 |
| PhysicsLabControllerLabVsProdTests | Assets/Scripts/Physics/Tests/PhysicsLabControllerLabVsProdTests.cs | 1 |
| BallAnimator | Assets/Scripts/Physics/Viewer/BallAnimator.cs | 1 |
| BallTrailController | Assets/Scripts/Physics/Viewer/BallTrailController.cs | 7 |
| Scenarios | Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs | 1 |
| ChaseCamera | Assets/Scripts/Physics/Viewer/ChaseCamera.cs | 4 |
| DashboardUI | Assets/Scripts/Physics/Viewer/DashboardUI.cs | 1 |
| GreenTuningPanelBuilder | Assets/Scripts/Physics/Viewer/Editor/GreenTuningPanelBuilder.cs | 1 |
| PutterGreenReaderSceneSetup | Assets/Scripts/Physics/Viewer/Editor/PutterGreenReaderSceneSetup.cs | 1 |
| GreenTuningPanel | Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs | 9 |
| HoleCompleteDriver | Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs | 2 |
| HoleCompletionBridge | Assets/Scripts/Physics/Viewer/HoleCompletionBridge.cs | 2 |
| HoleSessionDriver | Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs | 1 |
| LabHoleBinder | Assets/Scripts/Physics/Viewer/LabHoleBinder.cs | 1 |
| LandingBannerController | Assets/Scripts/Physics/Viewer/LandingBannerController.cs | 1 |
| LoopCameraDirector | Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs | 9 |
| PhysicsLabController | Assets/Scripts/Physics/Viewer/PhysicsLabController.cs | 32 |
| PhysicsLabUI | Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs | 2 |
| PutterAimLine | Assets/Scripts/Physics/Viewer/PutterAimLine.cs | 9 |
| PutterGreenReader | Assets/Scripts/Physics/Viewer/PutterGreenReader.cs | 15 |
| SurfaceRolloutHarness | Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs | 6 |
| TestGreenLabSetup | Assets/Scripts/Physics/Viewer/TestGreenLabSetup.cs | 1 |
| TrajectoryRenderer | Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs | 1 |
| TreeOccludeFadeDriver | Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs | 1 |
| VersusBot | Assets/Scripts/Physics/Viewer/VersusBot.cs | 6 |
| VersusMatchController | Assets/Scripts/Physics/Viewer/VersusMatchController.cs | 4 |
| WaterSplashController | Assets/Scripts/Physics/Viewer/WaterSplashController.cs | 7 |
| ManualPropId | Assets/Scripts/SceneSnapshot/ManualPropId.cs | 1 |
| AboutSubmenu | Assets/Scripts/UI/AboutSubmenu.cs | 5 |
| CreateUsernameScreenController | Assets/Scripts/UI/Account/CreateUsernameScreenController.cs | 5 |
| EmailConfirmationScreenController | Assets/Scripts/UI/Account/EmailConfirmationScreenController.cs | 6 |
| LoginScreenController | Assets/Scripts/UI/Account/LoginScreenController.cs | 14 |
| ResetPasswordScreenController | Assets/Scripts/UI/Account/ResetPasswordScreenController.cs | 17 |
| SignUpScreenController | Assets/Scripts/UI/Account/SignUpScreenController.cs | 25 |
| ButtonPressFeedback | Assets/Scripts/UI/ButtonPressFeedback.cs | 2 |
| StreakFlameView | Assets/Scripts/UI/Common/StreakFlameView.cs | 1 |
| ControlsSubmenu | Assets/Scripts/UI/ControlsSubmenu.cs | 6 |
| SafeAreaFitter | Assets/Scripts/UI/Core/SafeAreaFitter.cs | 2 |
| GameplayLocalizationDemoRecorder | Assets/Scripts/UI/Editor/GameplayLocalizationDemoRecorder.cs | 1 |
| LocalizationEditorHelper | Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs | 1 |
| FadeController | Assets/Scripts/UI/FadeController.cs | 1 |
| GachaBannerCard | Assets/Scripts/UI/Gacha/GachaBannerCard.cs | 17 |
| GachaCarouselController | Assets/Scripts/UI/Gacha/GachaCarouselController.cs | 12 |
| GachaHistoryRow | Assets/Scripts/UI/Gacha/GachaHistoryRow.cs | 4 |
| GachaHistoryRowBall | Assets/Scripts/UI/Gacha/GachaHistoryRowBall.cs | 9 |
| GachaHistoryScreenController | Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs | 5 |
| GachaHistoryTabStrip | Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs | 1 |
| GachaPrizesScreenController | Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs | 9 |
| GachaRatesModalController | Assets/Scripts/UI/Gacha/GachaRatesModalController.cs | 7 |
| RarityFxTier | Assets/Scripts/UI/Gacha/GachaRevealModalController.cs | 23 |
| GameplaySceneLoader | Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs | 2 |
| BadgeCellView | Assets/Scripts/UI/Gps/BadgeCellView.cs | 7 |
| CheckInConfirmModalController | Assets/Scripts/UI/Gps/CheckInConfirmModalController.cs | 8 |
| GiftSendModalController | Assets/Scripts/UI/Gps/GiftSendModalController.cs | 7 |
| GpsAvatarScreenController | Assets/Scripts/UI/Gps/GpsAvatarScreenController.cs | 24 |
| GpsBadgesScreenController | Assets/Scripts/UI/Gps/GpsBadgesScreenController.cs | 10 |
| GpsGiftScreenController | Assets/Scripts/UI/Gps/GpsGiftScreenController.cs | 9 |
| GpsGolfProfileScreenController | Assets/Scripts/UI/Gps/GpsGolfProfileScreenController.cs | 15 |
| GpsHubRoundRow | Assets/Scripts/UI/Gps/GpsHubRoundRow.cs | 7 |
| GpsHubScreenController | Assets/Scripts/UI/Gps/GpsHubScreenController.cs | 23 |
| GpsProfileScreenController | Assets/Scripts/UI/Gps/GpsProfileScreenController.cs | 24 |
| GpsRoundsScreenController | Assets/Scripts/UI/Gps/GpsRoundsScreenController.cs | 42 |
| GpsVoteScreenController | Assets/Scripts/UI/Gps/GpsVoteScreenController.cs | 15 |
| GpsWelcomeScreenController | Assets/Scripts/UI/Gps/GpsWelcomeScreenController.cs | 2 |
| HoleRowView | Assets/Scripts/UI/Gps/HoleRowView.cs | 6 |
| RoundCompleteModalController | Assets/Scripts/UI/Gps/RoundCompleteModalController.cs | 11 |
| RoundSpotRowView | Assets/Scripts/UI/Gps/RoundSpotRowView.cs | 11 |
| ScoreUploadFlowController | Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs | 98 |
| ShimmerBlock | Assets/Scripts/UI/Gps/ShimmerBlock.cs | 1 |
| VenuePickerModalController | Assets/Scripts/UI/Gps/VenuePickerModalController.cs | 5 |
| VoteCardView | Assets/Scripts/UI/Gps/VoteCardView.cs | 16 |
| VoteCreateModalController | Assets/Scripts/UI/Gps/VoteCreateModalController.cs | 6 |
| GraphicsSubmenu | Assets/Scripts/UI/GraphicsSubmenu.cs | 7 |
| ScreenHintModalController | Assets/Scripts/UI/Hints/ScreenHintModalController.cs | 9 |
| ScreenHintPresenter | Assets/Scripts/UI/Hints/ScreenHintPresenter.cs | 2 |
| HoleDatabaseLoader | Assets/Scripts/UI/HoleDatabaseLoader.cs | 3 |
| HoleCardController | Assets/Scripts/UI/HoleSelection/HoleCardController.cs | 28 |
| HoleProgressionDebug | Assets/Scripts/UI/HoleSelection/HoleProgressionDebug.cs | 1 |
| FilterPill | Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs | 8 |
| DailyMissionPillController | Assets/Scripts/UI/Home/DailyMissionPillController.cs | 19 |
| serve | Assets/Scripts/UI/Home/LoanOfferPillController.cs | 22 |
| NoticePageSlider | Assets/Scripts/UI/Home/NoticePageSlider.cs | 8 |
| HomeScreenController | Assets/Scripts/UI/HomeScreenController.cs | 38 |
| BagCarouselController | Assets/Scripts/UI/Inventory/BagCarouselController.cs | 11 |
| BagClubCard | Assets/Scripts/UI/Inventory/BagClubCard.cs | 21 |
| BagClubModalController | Assets/Scripts/UI/Inventory/BagClubModalController.cs | 6 |
| BagDetailPanel | Assets/Scripts/UI/Inventory/BagDetailPanel.cs | 10 |
| BagSelectionModalController | Assets/Scripts/UI/Inventory/BagSelectionModalController.cs | 4 |
| BagThumbnailCard | Assets/Scripts/UI/Inventory/BagThumbnailCard.cs | 6 |
| BallCarouselController | Assets/Scripts/UI/Inventory/BallCarouselController.cs | 10 |
| BallCompareController | Assets/Scripts/UI/Inventory/BallCompareController.cs | 35 |
| BallDatabaseCSV | Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs | 1 |
| BallDetailPanel | Assets/Scripts/UI/Inventory/BallDetailPanel.cs | 24 |
| BallThumbnailCard | Assets/Scripts/UI/Inventory/BallThumbnailCard.cs | 6 |
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | 9 |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | 50 |
| and | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | 1 |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | 42 |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs | 1 |
| ClubLevelUpModalController | Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs | 57 |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | 11 |
| ItemThumbnailCardBuilder | Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs | 1 |
| ItemUseClubCardBuilder | Assets/Scripts/UI/Inventory/Editor/ItemUseClubCardBuilder.cs | 1 |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs | 4 |
| ItemCarouselController | Assets/Scripts/UI/Inventory/ItemCarouselController.cs | 10 |
| ItemDatabaseCSV | Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs | 1 |
| ItemDetailPanel | Assets/Scripts/UI/Inventory/ItemDetailPanel.cs | 15 |
| ItemThumbnailCard | Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs | 7 |
| ItemUseClubCard | Assets/Scripts/UI/Inventory/ItemUseClubCard.cs | 20 |
| ItemUseModalController | Assets/Scripts/UI/Inventory/ItemUseModalController.cs | 7 |
| LanguageSubmenu | Assets/Scripts/UI/LanguageSubmenu.cs | 4 |
| LoadingBar | Assets/Scripts/UI/LoadingBar.cs | 2 |
| LoadingScreenController | Assets/Scripts/UI/LoadingScreenController.cs | 5 |
| LoanBadgeView | Assets/Scripts/UI/Loans/LoanBadgeView.cs | 4 |
| LoanModalController | Assets/Scripts/UI/Loans/LoanModalController.cs | 31 |
| LoanOfferModalController | Assets/Scripts/UI/Loans/LoanOfferModalController.cs | 13 |
| LoanOffersToggle | Assets/Scripts/UI/Loans/LoanOffersToggle.cs | 10 |
| LoanRecipientRow | Assets/Scripts/UI/Loans/LoanRecipientRow.cs | 8 |
| LoanRescindModalController | Assets/Scripts/UI/Loans/LoanRescindModalController.cs | 5 |
| LoanReturnModalController | Assets/Scripts/UI/Loans/LoanReturnModalController.cs | 6 |
| LoanRibbonView | Assets/Scripts/UI/Loans/LoanRibbonView.cs | 8 |
| LogoScreenController | Assets/Scripts/UI/LogoScreenController.cs | 3 |
| MatchmakingModalController | Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs | 38 |
| that | Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs | 2 |
| VersusResultScreenController | Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs | 20 |
| MissionCardController | Assets/Scripts/UI/MissionSelection/MissionCardController.cs | 41 |
| MissionSelectionScreenController | Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs | 7 |
| InGameSettingsModalController | Assets/Scripts/UI/Modals/InGameSettingsModalController.cs | 29 |
| ModalBackdropDismiss | Assets/Scripts/UI/Modals/ModalBackdropDismiss.cs | 1 |
| ModalController | Assets/Scripts/UI/Modals/ModalController.cs | 3 |
| HoleCompleteModalController | Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs | 1 |
| SchemeConfirmModalController | Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs | 6 |
| VersusResultHandler | Assets/Scripts/UI/Modals/VersusResultHandler.cs | 2 |
| ModeCardController | Assets/Scripts/UI/ModeSelect/ModeCardController.cs | 52 |
| ModeCarouselController | Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs | 17 |
| ModeSelectScreenController | Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs | 7 |
| PersistentUIManager | Assets/Scripts/UI/PersistentUIManager.cs | 2 |
| ScreenEntryMotion | Assets/Scripts/UI/Polish/ScreenEntryMotion.cs | 1 |
| names | Assets/Scripts/UI/Polish/ShimmerHost.cs | 1 |
| ProTipCard | Assets/Scripts/UI/ProTipCard.cs | 10 |
| RankingsScreenController | Assets/Scripts/UI/Rankings/RankingsScreenController.cs | 17 |
| CharacterLevelUpDatabase | Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs | 1 |
| PlayerCharacterData | Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs | 14 |
| CharacterDatabase | Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs | 18 |
| CharacterDatabaseCSV | Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs | 1 |
| CarouselController | Assets/Scripts/UI/Roster/UI/CarouselController.cs | 9 |
| CharacterDetailPanel | Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs | 44 |
| CharacterThumbnailCard | Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs | 14 |
| CompareController | Assets/Scripts/UI/Roster/UI/CompareController.cs | 38 |
| LevelUpModalController | Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs | 49 |
| RosterScreenController | Assets/Scripts/UI/Roster/UI/RosterScreenController.cs | 4 |
| StartingCharacterConfirmModalController | Assets/Scripts/UI/Roster/UI/StartingCharacterConfirmModalController.cs | 6 |
| StatBar | Assets/Scripts/UI/Roster/UI/StatBar.cs | 7 |
| ScreenManager | Assets/Scripts/UI/ScreenManager.cs | 36 |
| SettingsMenuItem | Assets/Scripts/UI/SettingsMenuItem.cs | 7 |
| GeneralShopCard | Assets/Scripts/UI/Shop/GeneralShopCard.cs | 4 |
| StaminaMenuRow | Assets/Scripts/UI/Shop/StaminaMenuRow.cs | 12 |
| StaminaShopCard | Assets/Scripts/UI/Shop/StaminaShopCard.cs | 10 |
| StaminaShopDetailScreenController | Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs | 19 |
| StaminaShopSelectionScreenController | Assets/Scripts/UI/Shop/StaminaShopSelectionScreenController.cs | 8 |
| StoreHistoryRow | Assets/Scripts/UI/Shop/StoreHistoryRow.cs | 3 |
| StoreHistoryScreenController | Assets/Scripts/UI/Shop/StoreHistoryScreenController.cs | 4 |
| SoundSettingsSubmenu | Assets/Scripts/UI/SoundSettingsSubmenu.cs | 4 |
| SwipeDetector | Assets/Scripts/UI/SwipeDetector.cs | 1 |
| TapFeedbackConfig | Assets/Scripts/UI/TapFeedbackConfig.cs | 1 |
| TapFeedbackController | Assets/Scripts/UI/TapFeedbackController.cs | 2 |
| TapFeedbackFX | Assets/Scripts/UI/TapFeedbackFX.cs | 3 |
| GameplaySceneLoaderTests | Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs | 1 |
| ToastController | Assets/Scripts/UI/Toast/ToastController.cs | 4 |
| TournamentDevEntryButton | Assets/Scripts/UI/Tournaments/TournamentDevEntryButton.cs | 2 |
| TournamentHoleSelectionScreenController | Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs | 8 |
| TournamentLeaderboardScreenController | Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs | 4 |
| TournamentResultModalController | Assets/Scripts/UI/Tournaments/TournamentResultModalController.cs | 9 |
| TournamentResultPresenter | Assets/Scripts/UI/Tournaments/TournamentResultPresenter.cs | 3 |
| TournamentRoundHandler | Assets/Scripts/UI/Tournaments/TournamentRoundHandler.cs | 1 |
| TournamentSelectionCard | Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs | 19 |
| TournamentSelectionScreenController | Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs | 16 |
| TournamentSignupModalController | Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs | 27 |
| UserProfileSubmenu | Assets/Scripts/UI/UserProfileSubmenu.cs | 9 |
| RuntimeActiveStateManager | Assets/Scripts/Utilities/RuntimeActiveStateManager.cs | 2 |

## CSV Data Files

### Bags.csv
```
id,name,rarity,thumbnail,fullImage,description,unlocked
bag_mireo,Mireo,Rare,Mireo,Mireo,Add any 8 clubs you want to take out to the field to your bag. Remember you always need at least 1 Driver and 1 Putter.,TRUE
```
(11 rows)

### Balls.csv
```
id,name,brand,rarity,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info,thumbnailUrl,fullUrl,isDefault
ball_golfin,Golfin,Golfin,Common,0,0,0,0,0,Golfin,Golfin,The standard Golfin ball. Perfectly balanced with no stat bonuses or penalties—reliable in any situation.,,,true
```
(21 rows)

### Characters.csv
```
id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,portraitFull,startLevel,maxLevel,bio,starterCandidate,portraitUrl,fullUrl
char_james,James,Cartwright,Common,7,6,5,7,James,BigRosterJames,10,39,"Out of the amateur ranks, Cartwright arrives with driving distance that already turns heads at this level. The short game is still maturing, but scouts agree the foundations are tour-calibre.",1,,
```
(13 rows)

### HoleDatabase.csv
```
courseNameKey,holeNumber,par,descriptionKey,holeImageName,windSpeedMph,windDirectionDegrees,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount,replayReward1Type,replayReward1Amount,replayReward2Type,replayReward2Amount,replayReward3Type,replayReward3Amount,courseId
HOLE_LOMOND_1,1,5,HOLE_LOMOND_1_DESC,lomond-country-club/Hole_01,8.9,308,Points,10,RepairKit,10,Ball,5,Points,5,RepairKit,5,Ball,2,lomond-country-club
```
(19 rows)

### HoleTees.csv
```
courseId,holeNumber,teeSet,yards,color
lomond-country-club,1,back,531,blue
```
(73 rows)

### Items.csv
```
id,name,category,rarity,restorePercent,thumbnailSprite,fullSprite,proTip,info,thumbnailUrl,fullUrl
repairkit_common,Repair Kit,RepairKit,Common,50,RepairKit-Common,RepairKit-Common,"Clubs will automatically use the best repair kit available when you repair them from the Clubs tab.","Essential and efficient, this Repair Kit restores up to 50% of any club's durability. Designed for quick fixes and reliable performance, it's a must have for keeping your equipment in solid shape round after round.",,
```
(4 rows)

### LevelUpCosts.csv
```
level,cost_r,sp_reward
1,1,1
```
(241 rows)

## Quick Health

### Potential Missing Methods on CharacterManager
```
WARNING: CharacterManager.NeedsStarter() called but not found as public method
WARNING: CharacterManager.OnCharacterLeveledUp() called but not found as public method
WARNING: CharacterManager.OnCharacterSelected() called but not found as public method
WARNING: CharacterManager.OnRosterChanged() called but not found as public method
```

---
End of audit.
