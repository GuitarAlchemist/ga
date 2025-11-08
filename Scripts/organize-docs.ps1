# Organize documentation files into logical categories

$docMappings = @{
    # Architecture
    "Architecture" = @(
        "MODULAR_RESTRUCTURING_PLAN.md",
        "MODULAR_RESTRUCTURING_PROGRESS.md",
        "CODE_SHARING_ARCHITECTURE.md",
        "ACTOR_MODEL.md",
        "MICROSERVICES_ANALYSIS.md",
        "ACTOR_SYSTEM_FIX.md",
        "ACTOR_SYSTEM_IMPLEMENTATION.md",
        "ACTOR_SYSTEM_STATUS.md",
        "FUNCTIONAL_MICROSERVICES.md",
        "MONADIC_MICROSERVICES_GUIDE.md",
        "MONADIC_MICROSERVICES_SUMMARY.md",
        "MONADIC_SERVICES_REFACTORING_COMPLETE.md",
        "MICROSERVICES_IMPROVEMENTS_SUMMARY.md"
    )
    
    # Guides
    "Guides" = @(
        "QUICK_START_AFTER_RESTART.md",
        "QUICK_START_NEW_FEATURES.md",
        "TESTING_GUIDE.md",
        "BUILD_FIX_GUIDE.md",
        "ASSET_DOWNLOAD_GUIDE.md",
        "ADVANCED_TECHNIQUES_GUIDE.md",
        "INTELLIGENT_AI_COMPLETE_GUIDE.md",
        "INTELLIGENT_BSP_AND_AI_GUIDE.md",
        "MEMORY_OPTIMIZATION_GUIDE.md",
        "GPU_ACCELERATION_GUIDE.md"
    )
    
    # Implementation
    "Implementation" = @(
        "IMPLEMENTATION_STATUS.md",
        "IMPLEMENTATION_COMPLETE_SUMMARY.md",
        "DSL_IMPLEMENTATION_STATUS.md",
        "DSL_IMPLEMENTATION_FINAL_SUMMARY.md",
        "AI_CODE_REORGANIZATION_PLAN.md",
        "AI_READY_API_IMPLEMENTATION.md",
        "IMPLEMENTATION-COMPLETE.md",
        "IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md",
        "TAB_CONVERSION_MICROSERVICE_STATUS.md",
        "TAB_CONVERSION_PROGRESS.md",
        "FORMAT_PARSERS_COMPLETE.md",
        "PARSER_BUGS_FIXED.md"
    )
    
    # Features
    "Features" = @(
        "AI_MUSIC_GENERATION_SERVICES.md",
        "SEMANTIC_FRETBOARD_GUIDE.md",
        "GUITAR_TAB_CONVERSION_ROADMAP.md",
        "STREAMING_API_QUICK_REFERENCE.md",
        "GPU_ACCELERATION_COMPLETE.md",
        "GRAPHITI_INTEGRATION_COMPLETE.md",
        "DSL_AND_TAB_CONVERSION_COMPLETE.md",
        "TAB_CONVERTER_REACT_DEMO_COMPLETE.md"
    )
    
    # API
    "API" = @(
        "API_ENDPOINTS_IMPLEMENTATION_PROGRESS.md",
        "STREAMING_API_COMPREHENSIVE_ANALYSIS.md",
        "STREAMING_API_QUICK_START.md",
        "STREAMING_API_ANALYSIS.md",
        "STREAMING_API_RECOMMENDATION.md",
        "STREAMING_IMPLEMENTATION_GUIDE.md",
        "STREAMING_IMPLEMENTATION_SUMMARY.md",
        "STREAMING_IMPLEMENTATION_COMPLETE.md",
        "MongoDB-API-Complete.md"
    )
    
    # Performance
    "Performance" = @(
        "EXTREME_PERFORMANCE_OPTIMIZATIONS.md",
        "MEMORY_OPTIMIZATION_GUIDE.md",
        "MEMORY_OPTIMIZATION_COMPLETE.md",
        "MEMORY_OPTIMIZATION_COMPARISON.md",
        "INDEXING_PERFORMANCE_OPTIMIZATIONS.md",
        "GPU_ACCELERATION_IMPLEMENTATION_COMPLETE.md",
        "GPU_IMPLEMENTATION_TASKS.md",
        "ADVANCED_OPTIMIZATION_OPPORTUNITIES.md"
    )
    
    # Integration
    "Integration" = @(
        "MCP_SERVER_ASPIRE_INTEGRATION.md",
        "MongoDB-Integration-Summary.md",
        "HuggingFace-Integration.md",
        "Vector-Search-Implementation-Guide.md",
        "Vector-Search-Implementation-Summary.md",
        "REDIS_AI_INTEGRATION.md",
        "REDIS_AI_QUICK_START.md",
        "MongoDB-AI-Integration-Plan.md",
        "MongoDB-Vector-Search-Local.md",
        "MCP_SETUP_COMPLETE.md",
        "TARS_MCP_INTEGRATION_PLAN.md",
        "TARS_MCP_FIXED.md",
        "TARS_MCP_GPU_INTEGRATION_COMPLETE.md"
    )
    
    # Configuration
    "Configuration" = @(
        "CONFIGURATION_STRATEGY.md",
        "MongoDB-Local-Setup.md",
        "MongoDB-Quick-Start.md",
        "SERVICE_REGISTRATION_GUIDELINES.md",
        "SOLUTION_ORGANIZATION.md",
        "PROJECT_REORGANIZATION_SUMMARY.md",
        "DATA_LAYER_UNIFICATION_STRATEGY.md"
    )
    
    # Testing
    "Testing" = @(
        "TESTING_GAPS_AND_PLAN.md",
        "CODE_COVERAGE_PLAN.md",
        "API_INTEGRATION_TESTS_COMPLETE.md",
        "HuggingFace-Testing-Summary.md"
    )
    
    # Roadmap
    "Roadmap" = @(
        "AI_FUTURE_ROADMAP.md",
        "OPTIONAL_ENHANCEMENTS_PROGRESS.md",
        "NEXT_STEPS_COMPLETE.md",
        "ANALYZER_INTEGRATION_OPPORTUNITIES.md"
    )
    
    # References
    "References" = @(
        "GUITAR_TAB_FORMATS.md",
        "MUSIC_THEORY_DSL_PROPOSAL.md",
        "VALUE_OBJECT_ANALYSIS.md",
        "ADVANCED_MONADS.md",
        "MONADS_COMPLETE_SUMMARY.md",
        "COHESIVENESS_REFACTORING_PLAN.md",
        "Vector-Search-README.md",
        "DSL_QUICK_START.md",
        "TARS_DSL_ANALYSIS_SUMMARY.md",
        "TARS_GA_INDEPENDENCE.md",
        "TARS_MCP_ANALYSIS.md",
        "TARS_MCP_DEMO.md"
    )
}

$docsPath = "docs"
$movedCount = 0

Write-Host "📚 Organizing documentation files..." -ForegroundColor Cyan

foreach ($category in $docMappings.Keys) {
    $categoryPath = Join-Path $docsPath $category
    
    foreach ($file in $docMappings[$category]) {
        $sourcePath = Join-Path $docsPath $file
        $destPath = Join-Path $categoryPath $file
        
        if (Test-Path $sourcePath) {
            Move-Item -Path $sourcePath -Destination $destPath -Force
            Write-Host "  ✓ $file → $category/" -ForegroundColor Gray
            $movedCount++
        }
    }
}

Write-Host "`n✅ Organized $movedCount documentation files" -ForegroundColor Green
Write-Host "📍 Documentation structure created in docs/ folder" -ForegroundColor Cyan

