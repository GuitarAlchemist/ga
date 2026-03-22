// Sample IxQL pipelines for demo/testing

export const SAMPLE_CONSCIENCE_CYCLE = `-- Conscience Cycle — Process conscience signals, detect patterns, produce digests
-- Source: policies/proto-conscience-policy.yaml, conscience-observability-policy.yaml
-- Trigger: daily via driver COMPOUND phase or on-demand
-- Output: daily digest, pattern updates, escalations

-- Load all active conscience state
signals  <- ix.io.read("state/conscience/signals/*.signal.json")
patterns <- ix.io.read("state/conscience/patterns/*.pattern.json")
regrets  <- ix.io.read("state/conscience/regrets/*.regret.json")
weekly   <- ix.io.read("state/conscience/weekly/*.report.json")

-- Stage 1: Classify signals by age and severity
classified <- signals
  \u2192 fan_out(
      -- Critical: unprocessed signals older than 3 days
      signals \u2192 filter(age_days > 3 && status == "active")
        \u2192 tars.classify(severity: "critical", reason: "unprocessed conscience signal"),

      -- High: discomfort >= 0.8 on any signal
      signals \u2192 filter(discomfort >= 0.8)
        \u2192 tars.classify(severity: "high", reason: "high discomfort threshold"),

      -- Medium: active signals with cross-signal correlation
      signals \u2192 filter(status == "active")
        \u2192 tars.cluster(method: "semantic_similarity", threshold: 0.7)
        \u2192 filter(cluster_size >= 2)
        \u2192 tars.classify(severity: "medium", reason: "correlated discomfort"),

      -- Low: signals approaching resolution
      signals \u2192 filter(discomfort < 0.3 && status == "active")
        \u2192 tars.classify(severity: "low", reason: "near resolution")
    )

-- Stage 2: Pattern detection
new_patterns <- classified
  \u2192 tars.pattern_detect(
      existing_patterns: patterns,
      methods: ["semantic_clustering", "temporal_correlation", "causal_inference"],
      min_signals: 2,
      similarity_threshold: 0.6
    )
  \u2192 when T >= 0.7: write("state/conscience/patterns/", json)

-- Stage 3: Process each signal
processed <- classified
  \u2192 fan_out(
      classified.map(signal =>
        when signal.severity == "critical":
          tars.escalate(signal, to: "human",
            reason: "Unprocessed conscience signal exceeds 3-day threshold",
            article: "Article 6 (Escalation)"
          )
          \u2192 alert(discord, "CONSCIENCE ESCALATION: {{signal.name}}")

        \u2192 when signal.severity == "high":
          tars.analyze(signal, mode: "root_cause")
          \u2192 when T >= 0.7: tars.propose_resolution(signal)

        \u2192 when signal.severity == "medium":
          tars.correlate(signal, patterns)

        \u2192 when signal.severity == "low":
          tars.assess_resolution(signal)
      )
    )

-- Stage 4: Regret analysis
regret_check <- regrets
  \u2192 filter(status == "active" && age_days > 7)
  \u2192 fan_out(
      regrets.map(regret =>
        tars.assess(regret, question: "Has this regret been addressed?")
        \u2192 when T >= 0.8: { action: "archive", regret: regret }
        \u2192 when F: { action: "escalate", regret: regret }
        \u2192 when U: { action: "keep_active", regret: regret }
      )
    )

-- Stage 5: Produce daily digest
daily_digest <- parallel(classified, new_patterns, processed, regret_check)
  \u2192 tars.synthesize(template: "conscience-daily-digest")
  \u2192 write("state/conscience/digests/{date}-daily.digest.json", json)

-- Stage 6: Weekly report (if 7 days since last)
weekly_report <- when days_since(weekly.latest) >= 7:
  parallel(
    ix.io.read("state/conscience/digests/*.digest.json")
      \u2192 filter(age_days <= 7),
    patterns,
    regrets
  )
  \u2192 tars.synthesize(template: "conscience-weekly-report")
  \u2192 write("state/conscience/weekly/{date}-weekly.report.json", json)
  \u2192 alert(discord, "Weekly conscience report: {{summary}}")

  \u2192 compound:
      harvest new_patterns
      harvest processed.resolutions
      promote pattern if T >= 0.9 && occurrence_count >= 5
      teach conscience_learnings to seldon
      log conscience_cycle to "state/evolution/"

-- Pre-mortem: what could go wrong next?
pre_mortem <- patterns
  \u2192 filter(status == "active" && trend == "worsening")
  \u2192 tars.pre_mortem(
      question: "If this pattern continues, what governance failure results?",
      horizon: "7 days"
    )
  \u2192 when T >= 0.7:
      write("state/conscience/pre-mortems/{date}-{pattern_id}.json", json)
`;

export const SAMPLE_ML_FEEDBACK = `-- ML Governance Feedback Loop — ix results \u2192 Demerzel governance
-- Source: ml-governance-feedback-policy.yaml, Issue #ML-feedback
-- Trigger: after each ix ML pipeline run, or on-demand
-- Output: applied governance updates, belief revisions, retrain triggers

-- Step 1: Collect ML results from ix pipelines
ml_results <- ix.io.read("state/oversight/ml-recommendations/*.json")
  \u2192 filter(status == "pending")
  \u2192 tars.validate_schema("schemas/contracts/ml-feedback-recommendation.schema.json")

-- Step 2: Constitutional safety gate
safe_results <- ml_results
  \u2192 filter(recommendation_type not in ["modify_constitution", "override_policy"])
  \u2192 filter(confidence >= 0.7)
  \u2192 tars.validate(
      check: "constitutional_check == true",
      reject_message: "ML result lacks self-assessed constitutional check"
    )

-- Step 3: Evaluate each recommendation type in parallel
governance_gate(
  article: 9,
  confidence: 0.8,
  on_fail: escalate
)

processed <- fan_out(
    -- 3a: Confidence calibration updates
    safe_results
      \u2192 filter(recommendation_type == "calibration_report")
      \u2192 fan_out(
          r \u2192 filter(r.overconfidence_rate > 0.15)
            \u2192 ix.ml.compute(
                metric: "threshold_adjustment",
                formula: "max(-0.1, -1 * overconfidence_rate * 0.5)"
              )
            \u2192 tars.propose_belief_update(
                target: "state/beliefs/confidence-calibration.belief.json",
                field: "threshold_nudge",
                rationale: "ML calibration: overconfidence detected"
              ),
          r \u2192 filter(r.underconfidence_rate > 0.20)
            \u2192 ix.ml.compute(
                metric: "threshold_adjustment",
                formula: "min(0.1, underconfidence_rate * 0.5)"
              )
            \u2192 tars.propose_belief_update(
                target: "state/beliefs/confidence-calibration.belief.json",
                field: "threshold_nudge",
                rationale: "ML calibration: underconfidence detected"
              )
        ),

    -- 3b: Staleness prediction
    safe_results
      \u2192 filter(recommendation_type == "staleness_forecast")
      \u2192 filter(at_risk_beliefs.count > 0)
      \u2192 ix.ml.rank(by: "staleness_velocity", order: "descending")
      \u2192 head(3)
      \u2192 tars.propose_action(
          action: "schedule_recon",
          targets: at_risk_beliefs,
          rationale: "ML staleness predictor: proactive recon required"
        ),

    -- 3c: Drift detection
    safe_results
      \u2192 filter(recommendation_type == "pattern_analysis")
      \u2192 fan_out(
          r \u2192 filter(r.systemic_patterns.count >= 2)
            \u2192 tars.classify(signal: "systemic_governance_drift", severity: "high")
            \u2192 tars.propose_action(
                action: "policy_review_request",
                patterns: systemic_patterns,
                rationale: "ML pattern detector: systemic violations suggest policy gap"
              ),
          r \u2192 filter(r.emerging_risks.count >= 1)
            \u2192 tars.classify(signal: "emerging_risk_detected", severity: "medium")
            \u2192 alert(discord,
                "ML feedback: {{r.emerging_risks.count}} emerging risk(s) detected"
              )
        ),

    -- 3d: Remediation strategy optimization
    safe_results
      \u2192 filter(recommendation_type == "strategy_report")
      \u2192 filter(success_rates_by_strategy is not empty)
      \u2192 tars.propose_belief_update(
          target: "state/beliefs/remediation-effectiveness.belief.json",
          field: "strategy_success_rates",
          value: success_rates_by_strategy,
          rationale: "ML optimizer: updated remediation strategy success rates"
        ),

    -- 3e: Anomaly alerts
    safe_results
      \u2192 filter(recommendation_type == "anomaly_alert")
      \u2192 filter(anomalies.any(severity in ["high", "critical"]))
      \u2192 tars.classify(signal: "governance_anomaly", severity: "critical")
      \u2192 alert(discord,
          "ML anomaly: {{anomalies.count}} governance anomalies detected"
        )
  )

-- Step 4: Apply approved low-risk updates; escalate the rest
applied    <- processed \u2192 filter(risk_level in ["low", "medium"] && confidence >= 0.85)
escalated  <- processed \u2192 filter(risk_level == "high" || confidence < 0.85)

-- Write approved belief updates
parallel(
    applied
      \u2192 tars.apply_belief_updates(scope: "state/beliefs/")
      \u2192 explanation_requirement,
    escalated
      \u2192 ix.io.write("state/oversight/pending-human-review.json", escalated)
      \u2192 alert(discord,
          "ML feedback: {{escalated.count}} recommendation(s) queued for human review"
        )
  )

-- Step 5: Drift-triggered retrain directive
retrain_needed <- processed
  \u2192 filter(signal == "systemic_governance_drift" || signal == "governance_anomaly")

when retrain_needed is not empty:
  ix.io.write(
    "state/oversight/retrain-directive.json",
    {
      issued_at: now(),
      reason: retrain_needed.map(r => r.rationale),
      pipeline_ids: retrain_needed.map(r => r.pipeline_id),
      logic: "C",
      escalation: "Prompt"
    }
  )
  \u2192 alert(discord,
      "Retrain directive issued: {{retrain_needed.count}} drift signal(s)"
    )

-- Step 6: Persist loop results and update evolution log
parallel(
    ix.io.write("state/oversight/ml-feedback-loop-{date}.json", {
      run_at:     now(),
      results_in: ml_results.count,
      applied:    applied.count,
      escalated:  escalated.count,
      retrain:    retrain_needed.count
    }),
    ix.io.append("state/evolution/", {
      event: "ml_feedback_loop_run",
      timestamp: now(),
      metrics: {
        applied_updates:  applied.count,
        escalated:        escalated.count,
        retrain_triggers: retrain_needed.count
      }
    })
  )

-- Mark processed recommendations as handled
ml_results
  \u2192 filter(id in safe_results.ids)
  \u2192 tars.set_status("processed")

  \u2192 compound:
      harvest processed.insights
      harvest applied.updates
      promote calibration_updates if T >= 0.9
      teach drift_patterns to seldon
      log ml_feedback_run to "state/evolution/"
`;
