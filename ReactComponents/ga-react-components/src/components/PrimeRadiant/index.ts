// src/components/PrimeRadiant/index.ts
// Public exports for the Prime Radiant governance visualization engine

export { PrimeRadiant } from './PrimeRadiant';
export { ForceRadiant } from './ForceRadiant';
export { DetailPanel } from './DetailPanel';
export { ChatWidget } from './ChatWidget';
export type {
  GovernanceNode,
  GovernanceEdge,
  GovernanceGraph,
  GovernanceNodeType,
  GovernanceEdgeType,
  HealthMetrics,
  HealthStatus,
  GovernanceHealthStatus,
  FileTreeNode,
  PrimeRadiantProps,
  SelectionState,
} from './types';
export { SAMPLE_GOVERNANCE_GRAPH } from './sampleData';
export { LIVE_GOVERNANCE_GRAPH } from './liveData';
export { searchNodes, getHealthStatus, deriveGovernanceHealthStatus, applyHealthColors, buildGraphIndex } from './DataLoader';
export type { BeliefState, TetravalentStatus, BeliefEvidence, EvidenceItem } from './DataLoader';
export { createDemerzelFace, updateDemerzelFace } from './DemerzelFace';
export { createRiggedDemerzelFace, updateRiggedDemerzelFace, setRiggedEmotion } from './DemerzelRiggedFace';
export type { RiggedEmotion } from './DemerzelRiggedFace';
export { createTarsRobot, updateTarsRobot } from './TarsRobot';
export { createSolarSystem, updateSolarSystem, startLiveCloudUpdates, togglePlanetAtmosphere, toggleEarthClouds, toggleEarthBorders, toggleSnowCover, toggleAurora, toggleRingGlow, toggleJupiterStorm } from './SolarSystem';
export { startVisualCriticLoop } from './VisualCriticLoop';
export { startDemerzelDriver } from './DemerzelIxqlDriver';
export type { VisualCriticResult, VisualCriticConfig, CriticPhase } from './VisualCriticLoop';
export { DemerzelCriticOverlay } from './DemerzelCriticOverlay';
export type { CriticState } from './DemerzelCriticOverlay';
export { GalacticClock } from './GalacticClock';
export { TutorialOverlay } from './TutorialOverlay';
export { ActivityPanel } from './ActivityPanel';
export { LLMStatus, useLLMHealth } from './LLMStatus';
export { recordUsage, getSessionUsage, useSessionUsage, getQuotaPercent, resetSessionUsage } from './LLMUsageTracker';
export type { ModelUsage, SessionUsage } from './LLMUsageTracker';
export { createGhostTrails, updateGhostTrails } from './GhostTrail';
export { SeldonDashboard } from './SeldonDashboard';
export type { SeldonDashboardProps } from './SeldonDashboard';
export { createSpaceStation, updateSpaceStation } from './SpaceStation';
export { createCrystalEiffelTower } from './CrystalEiffelTower';
export type { CrystalEiffelTowerHandle } from './CrystalEiffelTower';
export { getNodeMaterial, getNodeMaterialWithGlow, getMaterialDef, disposeCrystalMaterials } from './CrystalNodeMaterials';
export { createTerminalFilaments } from './TerminalFilaments';
export type { TerminalFilamentsHandle, FilamentConfig } from './TerminalFilaments';
export type { NodeMaterialDef } from './CrystalNodeMaterials';
export { createSignalAura } from './SignalAura';
export type { SignalAuraHandle } from './SignalAura';
export { IxqlCodeGen } from './IxqlCodeGen';
export type { IxqlCodeGenProps } from './IxqlCodeGen';
export { completeCode, completeIxql, useCodeCompletion } from './CodestralComplete';
export type { UseCodeCompletionResult } from './CodestralComplete';
export { IxqlCommandInput } from './IxqlCommandInput';
export { parseIxqlCommand, evaluatePredicate } from './IxqlControlParser';
export type { IxqlCommand, IxqlPredicate, IxqlAssignment, IxqlParseResult, SelectCommand, ResetCommand, CreatePanelCommand, CreateGridPanelCommand, CreateVizCommand, CreateFormCommand, FormFieldDef, VizKind, ProjectionField, BindHealthCommand, DropCommand, CreateNodeCommand, LinkCommand, GroupCommand, SaveCommand, OnViolationCommand, ViolationSeverity, PipeStep, AggregateSpec, AggregateFunction, DiagnoseCommand, HealthCheckCommand, ShowCommand, HideCommand, FixCommand } from './IxqlControlParser';
// Phase 10: Agentic Dashboards — violation monitoring and saved queries
export { violationMonitor, useViolationMonitor, useViolationCounts } from './ViolationMonitor';
export type { ViolationRule, ActiveViolation } from './ViolationMonitor';
export { savedQueryStore, useSavedQueries } from './SavedQueryStore';
export type { SavedQuery } from './SavedQueryStore';
// IXQL UI Composition — Phase 1: Grid panels + PIPE transforms
export { IxqlGridPanel } from './IxqlGridPanel';
export type { IxqlGridPanelProps } from './IxqlGridPanel';
export { IxqlFormPanel } from './IxqlFormPanel';
export type { IxqlFormPanelProps } from './IxqlFormPanel';
export { compileGridPanel, compileViz, compileForm, applyProjection } from './IxqlWidgetSpec';
export type { PanelSpec, VizSpec, FormSpec, WidgetSpec, DataBindingSpec, ProjectionSpec, PipelineSpec, ResponsiveLayoutSpec } from './IxqlWidgetSpec';
export { IxqlVizPanel } from './IxqlVizPanel';
export type { IxqlVizPanelProps } from './IxqlVizPanel';
export { executePipeline } from './IxqlPipeEngine';
// Phase 5: Render Verification — Godel Dual-Buffer
export { generateGridProof, generateVizProof, generateFormProof, publishRenderProof, cognitiveChecksum, dataFingerprint, classifyDivergences } from './RenderProof';
export type { RenderProof, GridRenderProof, VizRenderProof, FormRenderProof, DivergenceSeverity, CaptureQuality } from './RenderProof';
// Phase 6: Hexavalent Temporal Semantics
export { DecayTracker, isValidTransition, getAllowedTransitions, publishTransition, adaptiveRefreshInterval, decayAnimationDelay, decayAnimationDuration } from './HexavalentTemporal';
export type { HexavalentValue, HexavalentTransitionEvent, DecayConfig, DecayEvent } from './HexavalentTemporal';
// Phase 7: Constitutional Case Law
export { caseLawStore, fileCase, findPrecedent, detectStandingOrder, resolveFromConfidence, generateCaseId } from './CaseLaw';
export type { ConstitutionalCase, CasePredicate, StandingOrder, CaseIndex, CaseIndexEntry, CaseStats, PrecedentResult, PrecedentTier, ResolutionAction } from './CaseLaw';
// Phase 8: Living Grammar Extension Registry
export { extensionRegistry, constitutionalGate } from './GrammarExtensionRegistry';
export type { PipeStepExtension, ExtensionStatus, ExtensionArg, GrammarVersion, GrammarProposal, ConstitutionalGateResult, TelemetryEvent } from './GrammarExtensionRegistry';
// Step 5: Truth Lattice Panel — hexavalent state transition visualization
export { IxqlTruthLatticePanel } from './IxqlTruthLatticePanel';
export type { IxqlTruthLatticePanelProps } from './IxqlTruthLatticePanel';
// Step 4: ProofVerifier — closes governance self-verification loop
export { proofVerifier } from './ProofVerifier';
export type { ProofHistoryEntry, ProofDivergenceAlert } from './ProofVerifier';
// Step 1: IxqlDispatcher — extracted from ForceRadiant God Component
export { dispatchIxqlCommand } from './IxqlDispatcher';
export type { DispatchContext, DispatchResult } from './IxqlDispatcher';
export { signalBus, useSignal, useSignals, usePublish } from './DashboardSignalBus';
export type { DashboardSignal } from './DashboardSignalBus';
export { PLANET_ASTRO_DATA } from './SolarSystem';
export type { PlanetAstroData } from './SolarSystem';
export { BacklogPanel } from './BacklogPanel';
export { AgentPanel } from './AgentPanel';
export type { AgentInfo, AgentTeam } from './AgentPanel';
export { CommitTooltip } from './CommitTooltip';
export { AlgedonicPanel } from './AlgedonicPanel';
export { createAlgedonicSignal } from './AlgedonicPanel';
export { SignalGraph } from './SignalGraph';
export { remediateSignal, useRemediation } from './DemerzelRemediation';
export type { RemediationRisk, RemediationAction, RemediationResult, UseRemediationResult } from './DemerzelRemediation';
export type { SignalGraphProps } from './SignalGraph';
export { AdminInbox } from './AdminInbox';
export { readDeepLink, writeDeepLink, getShareableUrl, shareCurrentState, useDeepLink } from './DeepLink';
export type { DeepLinkState, UseDeepLinkResult } from './DeepLink';
export { captureCanvas, captureFullPage, captureAndPost, useScreenshotCapture } from './ScreenshotCapture';
export { ScreenshotButton } from './ScreenshotButton';
export { ScreenshotPreview } from './ScreenshotPreview';
export type { AlgedonicSignal, AlgedonicSignalType, AlgedonicSeverity, AlgedonicStatus, AlgedonicPanelProps } from './AlgedonicPanel';
export { BeliefHeatmap } from './BeliefHeatmap';
export { IconRail } from './IconRail';
export type { PanelId, BuiltInPanelId, PanelGroupId, PanelGroupDef } from './PanelRegistry';
export { panelRegistry, usePanelRegistry, ICON_CATALOG, PANEL_GROUPS } from './PanelRegistry';
export type { PanelDefinition, PanelRegistration } from './PanelRegistry';
export { CICDPanel, useCICDHealth } from './CICDPanel';
export type { CICDHealth } from './CICDPanel';
export { ClaudeCodePanel } from './ClaudeCodePanel';
export { LibraryPanel } from './LibraryPanel';
export { BrainstormPanel } from './BrainstormPanel';
export { BeliefWidget, AlgedonicWidget, HexavalentWidget, StateWidget, IxqlPreview } from './GovernanceWidgets';
export type { BeliefWidgetProps, AlgedonicWidgetProps, HexavalentWidgetProps, StateWidgetProps, IxqlPreviewProps } from './GovernanceWidgets';
export { LiveNotebook } from './LiveNotebook';
export type { LiveNotebookProps } from './LiveNotebook';
export { GodotScene, setDemerzelEmotion, setDemerzelSpeaking, setDemerzelAutoCycle } from './GodotScene';
export type { GodotSceneProps, GodotInboundMessage, GodotOutboundMessage } from './GodotScene';
export { GodotSceneBuilder } from './GodotSceneBuilder';
export { godotMcp } from './GodotMcpClient';
export type { ConnectionStatus as GodotConnectionStatus } from './GodotMcpClient';
export { GisPanel } from './GisPanel';
export type { GisPanelProps } from './GisPanel';
export { PresencePanel, pushPresenceEvent } from './PresencePanel';
export type { PresencePanelProps, PresenceEvent } from './PresencePanel';
export { ConnectionLog, getConnectionLog, useConnectionLog } from './ConnectionLog';
export type { ConnectionLogEntry, ConnectionLogConfig } from './ConnectionLog';
export { GisLayerManager, createGisLayer } from './GisLayer';
export type { GisPin, GisPath, GisCluster, GisHeatPoint } from './GisLayer';
export { usePrControl } from './usePrControl';
export type { PrCommand, PrResult, PrControlHandlers } from './usePrControl';
export { LunarLander } from './LunarLander';
export type { LunarLanderProps, LunarLanderStats } from './LunarLander';
export { LunarLanderEngine } from './LunarLanderEngine';
export type { LanderState, LanderStats } from './LunarLanderEngine';
export { GodotBridge, createGodotBridge } from './GodotBridge';
export type { BridgeEvent, BridgeEventType, A2AAgentId, A2AAgentStatus, A2AAgentSkill, AgentConnectPayload, AgentDisconnectPayload, AgentStatusPayload, AgentInvokePayload, AgentResultPayload } from './GodotBridge';
export { getAgentPresence, useAgentPresence, AGENT_STATUS_COLORS, AGENT_STATUS_LABELS } from './AgentPresence';
export type { A2AAgent, AgentPresenceConfig } from './AgentPresence';
export { useGodotBridge } from './useGodotBridge';
export type { UseGodotBridgeResult } from './useGodotBridge';
export { ixqlToGis, clearIxqlPins } from './IxqlGisBridge';
export { startSignalRGisBridge } from './SignalRGisBridge';
export type { SignalRGisBridgeHandle } from './SignalRGisBridge';
// Layer 1: Multi-Model Fan-Out
export { fanOutQuery, getProvider, getProvidersByCategory, useMultiModelQuery, MODEL_PROVIDERS } from './MultiModelFanOut';
export type { ModelProvider, ProviderEndpoint, FanOutRequest, FanOutResult, FanOutResultStatus, FanOutResponse, MultiModelQueryState, UseMultiModelQueryResult } from './MultiModelFanOut';
// Layer 2: Theory Tribunal
export { TheoryTribunal } from './TheoryTribunal';
// Layer 3: Demerzel Voice
export { speakAsDemerzel, stopSpeaking, isTTSAvailable, useDemerzelVoice } from './VoxtralTTS';
export type { TTSRequest, TTSResponse, DemerzelVoiceHook } from './VoxtralTTS';
// Layer 5: Code Tribunal
export { CodeTribunal } from './CodeTribunal';
// Layer 4: Seldon Faculty
export { SeldonFacultyPanel } from './SeldonFacultyPanel';
export { getFaculty, getFacultyForDepartment, getFacultyByProvider, askFacultyMember, useSeldonFaculty } from './SeldonFaculty';
export type { FacultyMember, FacultyStatus, FacultyWithStatus, SeldonFacultyState } from './SeldonFaculty';
