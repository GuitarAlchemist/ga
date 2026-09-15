# LoopPolicy.psm1 — validation for GA's AFK policy/readiness contract.
#
# This module validates policy shape and GA-specific invariants only. Path
# matching, postflight, leases, budgets, and evidence verification remain owned
# by the pinned Agent Blackbox toolkit and its GA adapter (#630).

Set-StrictMode -Version Latest

function Test-GaLoopPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$Policy
    )

    $problems = [System.Collections.Generic.List[string]]::new()

    $allowEdit = @($Policy.allow_edit)
    $protectedPaths = @($Policy.protected_paths)
    if ($allowEdit.Count -eq 0) { $problems.Add('loop_policy_missing_allow_edit') }
    if ($protectedPaths.Count -eq 0) { $problems.Add('loop_policy_missing_protected_paths') }

    foreach ($pattern in @('Common/**', 'Tests/**', 'Common/GA.Business.ML/**', 'Tests/Common/GA.Business.ML.Tests/**')) {
        if ($allowEdit -contains $pattern) {
            $problems.Add("allow_edit_too_broad_$pattern")
        }
    }

    foreach ($requiredPath in @(
        'Common/GA.Business.ML/Agents/Skills/**',
        'Common/GA.Business.ML/Agents/Mcp/**',
        'Tests/Common/GA.Business.ML.Tests/Unit/*SkillTests.cs',
        'Tests/Common/GA.Business.ML.Tests/Unit/*McpToolsTests.cs'
    )) {
        if ($allowEdit -notcontains $requiredPath) {
            $problems.Add("allow_edit_missing_$requiredPath")
        }
    }

    $levels = $Policy.verification_levels
    foreach ($level in @('L0', 'L1', 'L2', 'L3')) {
        $property = if ($levels) { $levels.PSObject.Properties[$level] } else { $null }
        if (-not $property) {
            $problems.Add("verification_level_missing_$level")
            continue
        }

        if (@($property.Value.commands).Count -eq 0) {
            $problems.Add("verification_level_missing_commands_$level")
        }
    }

    if ($levels -and $levels.PSObject.Properties['L0']) {
        if (@($levels.L0.commands) -notcontains 'pwsh Scripts/supervised-loop-preflight.ps1') {
            $problems.Add('verification_L0_missing_preflight')
        }
    }
    if ($levels -and $levels.PSObject.Properties['L2']) {
        if (@($levels.L2.commands) -notcontains 'bash Scripts/cloud-validate.sh') {
            $problems.Add('verification_L2_missing_cloud_validate')
        }
        if (@($levels.L2.platform_commands.windows) -notcontains 'pwsh Scripts/cloud-validate.ps1') {
            $problems.Add('verification_L2_missing_windows_entrypoint')
        }
    }
    if ($levels -and $levels.PSObject.Properties['L3']) {
        $l3Commands = @($levels.L3.commands)
        if ($l3Commands -notcontains 'dotnet build AllProjects.slnx -c Debug') {
            $problems.Add('verification_L3_missing_full_build')
        }
        if ($l3Commands -notcontains 'dotnet test AllProjects.slnx') {
            $problems.Add('verification_L3_missing_full_test')
        }
    }

    $postflight = $Policy.postflight
    if (-not $postflight -or $postflight.provider -ne 'GuitarAlchemist/agent-blackbox#44') {
        $problems.Add('postflight_provider_not_agent_blackbox')
    }
    if (-not $postflight -or $postflight.adapter_issue -ne 'GuitarAlchemist/ga#630') {
        $problems.Add('postflight_adapter_not_ga_630')
    }
    if (-not $postflight -or $postflight.path_matcher_provider -ne 'GuitarAlchemist/agent-blackbox#46') {
        $problems.Add('postflight_matcher_not_agent_blackbox')
    }
    if (-not $postflight -or $postflight.required_before_ready -ne $true) {
        $problems.Add('postflight_not_required_before_ready')
    }

    $review = $Policy.independent_review
    if (-not $review -or $review.mechanism -ne 'required_check') {
        $problems.Add('independent_review_not_required_check')
    }
    if (-not $review -or $review.check_name -ne 'Independent Review Verdict') {
        $problems.Add('independent_review_check_name_invalid')
    }
    if (-not $review -or $review.producer_requirement -ne 'separate_reviewer_identity_or_context') {
        $problems.Add('independent_review_producer_not_separate')
    }
    if (-not $review -or $review.author_comment_satisfies -ne $false) {
        $problems.Add('independent_review_allows_author_comment')
    }

    $draftRequires = @($Policy.promotion_gates.draft_pr.requires)
    foreach ($level in @('L0', 'L1', 'L2')) {
        if ($draftRequires -notcontains $level) {
            $problems.Add("draft_gate_missing_$level")
        }
    }
    $readyRequires = @($Policy.promotion_gates.ready_for_merge.requires)
    foreach ($gate in @('L3', 'postflight', 'independent_review')) {
        if ($readyRequires -notcontains $gate) {
            $problems.Add("ready_gate_missing_$gate")
        }
    }
    if ($Policy.promotion_gates.autonomous_merge -ne $false) {
        $problems.Add('autonomous_merge_not_disabled')
    }

    $shaBindings = @($Policy.evidence.bind_to)
    if ($Policy.evidence.schema -ne 'afk-evidence-manifest-v0.2') {
        $problems.Add('evidence_contract_not_v0.2')
    }
    foreach ($binding in @('base_sha', 'head_sha')) {
        if ($shaBindings -notcontains $binding) {
            $problems.Add("evidence_missing_$binding")
        }
    }

    return [pscustomobject]@{
        Valid = ($problems.Count -eq 0)
        Problems = @($problems)
    }
}

Export-ModuleMember -Function Test-GaLoopPolicy
