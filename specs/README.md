# Guitar Alchemist - Spec-Driven Development

This directory contains specifications, plans, and tasks for the Guitar Alchemist project using the Spec-Driven Development methodology.

## Structure

- **`SPEC.md`** - High-level specification describing what we're building and why (user journeys, experiences, outcomes)
- **`PLAN.md`** - Technical implementation plan (stack, architecture, constraints)
- **`TASKS.md`** - Actionable task breakdown derived from the spec and plan
- **`features/`** - Feature-specific specs, plans, and tasks

## Workflow

### 1. Specify (SPEC.md)
Define the "what" and "why":
- Who will use this?
- What problem does it solve?
- How will they interact with it?
- What outcomes matter?

### 2. Plan (PLAN.md)
Define the "how":
- Technology stack
- Architecture decisions
- Integration constraints
- Performance targets
- Security requirements

### 3. Tasks (TASKS.md)
Break down into implementable chunks:
- Small, reviewable tasks
- Each task solves a specific piece
- Can be implemented and tested in isolation

### 4. Implement
Execute tasks one by one:
- Review focused changes
- Validate against spec
- Test each piece

## Commands

When using with AI coding agents:

```
/specify - Generate or update SPEC.md
/plan - Generate or update PLAN.md
/tasks - Generate or update TASKS.md
```

## Current Features

### Fretboard Analysis System
- **Spec**: `features/fretboard-analysis/SPEC.md`
- **Plan**: `features/fretboard-analysis/PLAN.md`
- **Tasks**: `features/fretboard-analysis/TASKS.md`

Physical playability analysis for guitar chord voicings with GraphQL API exposure.

**Status**: Phase 1 Complete (GraphQL Integration) ✅

**Key Capabilities**:
- Physical measurements (fret span, finger stretch, string spacing)
- Difficulty classification (Very Easy → Impossible)
- Suggested fingerings with technique indicators
- GraphQL API with optional physical analysis
- Chord equivalence grouping
- CAGED system analysis

**Next Steps**: Phase 2 - Testing & Documentation

## Using This Spec

### For Developers

1. **Read the Spec** (`SPEC.md`) to understand what we're building and why
2. **Review the Plan** (`PLAN.md`) to understand the technical approach
3. **Check the Tasks** (`TASKS.md`) to see what's done and what's next
4. **Implement** following the task breakdown
5. **Update** task status as you complete work

### For Product Managers

1. **Review the Spec** to ensure it matches user needs
2. **Validate** user journeys and success metrics
3. **Prioritize** tasks based on business value
4. **Track** progress using task completion percentages

### For QA Engineers

1. **Use the Spec** to understand expected behavior
2. **Reference the Plan** for technical implementation details
3. **Follow the Tasks** to know what needs testing
4. **Create** test cases based on acceptance criteria

## Spec-Driven Development Benefits

### For This Project

1. **Clarity**: Everyone understands what we're building and why
2. **Alignment**: Technical decisions are tied to user needs
3. **Traceability**: Tasks map directly to spec requirements
4. **Quality**: Acceptance criteria ensure completeness
5. **Efficiency**: Less rework because requirements are clear upfront

### Lessons Learned

1. **Start with Users**: Understanding user journeys prevents building the wrong thing
2. **Be Specific**: Vague specs lead to vague implementations
3. **Iterate**: Specs evolve as we learn more
4. **Test Early**: Acceptance criteria guide testing from the start
5. **Document Decisions**: Future developers will thank you

