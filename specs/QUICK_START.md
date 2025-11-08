# Spec-Driven Development - Quick Start Guide

## What is Spec-Driven Development?

Spec-Driven Development is a methodology where you write specifications before code. It helps ensure:
- Everyone understands what we're building and why
- Technical decisions align with user needs
- Implementation is complete and testable
- Less rework because requirements are clear upfront

## The Three Documents

### 1. SPEC.md - The "What" and "Why"
**Purpose**: Define what we're building from the user's perspective

**Contains**:
- Problem statement
- Target users
- User journeys
- Key features
- Success metrics
- Non-goals

**When to write**: Before any code is written

**Example questions it answers**:
- Who will use this feature?
- What problem does it solve?
- How will users interact with it?
- What outcomes matter?

### 2. PLAN.md - The "How"
**Purpose**: Define the technical approach

**Contains**:
- Architecture overview
- Technology stack
- Implementation details
- Performance targets
- Security considerations
- Migration plan

**When to write**: After the spec is approved, before implementation

**Example questions it answers**:
- What technologies will we use?
- How will components interact?
- What are the performance requirements?
- What are the risks?

### 3. TASKS.md - The "Steps"
**Purpose**: Break down the work into implementable chunks

**Contains**:
- Task breakdown by phase
- Acceptance criteria for each task
- Dependencies between tasks
- Status tracking

**When to write**: After the plan is approved, before implementation

**Example questions it answers**:
- What needs to be done?
- In what order?
- How do we know when it's done?
- What's the current status?

## Workflow

### Step 1: Write the Spec
```bash
# Create the spec document
code specs/features/my-feature/SPEC.md
```

**Focus on**:
- User problems and pain points
- User journeys (step-by-step scenarios)
- Success criteria (how do we measure success?)
- Non-goals (what are we NOT doing?)

**Review with**:
- Product managers
- Potential users
- Stakeholders

### Step 2: Write the Plan
```bash
# Create the plan document
code specs/features/my-feature/PLAN.md
```

**Focus on**:
- Architecture decisions
- Technology choices
- Integration points
- Performance requirements
- Security considerations

**Review with**:
- Technical leads
- Architects
- Security team

### Step 3: Break Down Tasks
```bash
# Create the tasks document
code specs/features/my-feature/TASKS.md
```

**Focus on**:
- Small, reviewable chunks
- Clear acceptance criteria
- Logical ordering
- Dependencies

**Review with**:
- Development team
- QA team

### Step 4: Implement
```bash
# Work through tasks one by one
# Update task status as you go
```

**For each task**:
1. Read the task description
2. Review acceptance criteria
3. Implement the solution
4. Test against acceptance criteria
5. Mark task as complete
6. Move to next task

### Step 5: Review and Iterate
```bash
# Update specs as you learn
# Reflect changes in plan and tasks
```

**When to update**:
- Requirements change
- New insights emerge
- Technical constraints discovered
- User feedback received

## Example: Fretboard Analysis System

### Spec Highlights
**Problem**: Too many chord voicings, no physical context, poor accessibility

**Users**: Music app developers, educators, guitar players

**Key Journey**: Developer integrating chord data into a learning app
- Queries GraphQL API for chords in fret span
- Requests physical playability analysis
- Receives measurements, difficulty, fingerings
- Filters by difficulty level
- Displays playable chords to users

**Success Metric**: API returns results in < 500ms, 90%+ accuracy

### Plan Highlights
**Architecture**: GraphQL API → Business Logic → Physical Calculator

**Stack**: .NET 9, HotChocolate, ASP.NET Core

**Key Decision**: Make physical analysis optional (performance)

**Performance Target**: < 500ms for typical queries

### Tasks Highlights
**Phase 1**: GraphQL Integration (3 tasks) ✅
- Add GraphQL types
- Update query methods
- Update FromAnalysis method

**Phase 2**: Testing & Documentation (5 tasks) 🔄
- Unit tests
- Integration tests
- Schema documentation
- Example queries
- API documentation

## Tips for Success

### Writing Good Specs

✅ **Do**:
- Focus on user problems, not solutions
- Include specific user journeys
- Define measurable success criteria
- List non-goals explicitly
- Use concrete examples

❌ **Don't**:
- Jump to implementation details
- Be vague about success metrics
- Forget about edge cases
- Ignore non-functional requirements
- Skip user validation

### Writing Good Plans

✅ **Do**:
- Explain architectural decisions
- Document trade-offs
- Include diagrams
- Define performance targets
- Identify risks

❌ **Don't**:
- Over-engineer
- Ignore existing systems
- Forget about security
- Skip performance considerations
- Assume unlimited resources

### Writing Good Tasks

✅ **Do**:
- Keep tasks small (< 1 day)
- Include clear acceptance criteria
- Order tasks logically
- Track dependencies
- Update status regularly

❌ **Don't**:
- Create huge tasks
- Be vague about "done"
- Ignore dependencies
- Forget to update status
- Skip testing tasks

## Common Pitfalls

### 1. Skipping the Spec
**Problem**: Jump straight to coding without understanding the problem

**Result**: Build the wrong thing, lots of rework

**Solution**: Always write the spec first, validate with users

### 2. Spec Too Vague
**Problem**: "Build a better chord system"

**Result**: Everyone has different expectations

**Solution**: Include specific user journeys and success metrics

### 3. Plan Too Detailed
**Problem**: Trying to design every class and method upfront

**Result**: Analysis paralysis, outdated before implementation

**Solution**: Focus on architecture and key decisions, not every detail

### 4. Tasks Too Large
**Problem**: "Implement entire feature" as one task

**Result**: Hard to review, hard to track progress

**Solution**: Break into small, testable chunks

### 5. Not Updating Specs
**Problem**: Specs become outdated as implementation evolves

**Result**: Specs are useless for future developers

**Solution**: Update specs when requirements or approach changes

## Tools and Templates

### Spec Template
```markdown
# Feature Name - Specification

## Overview
Brief description

## Problem Statement
What problem are we solving?

## Target Users
Who will use this?

## User Journeys
Step-by-step scenarios

## Key Features
What are we building?

## Success Metrics
How do we measure success?

## Non-Goals
What are we NOT doing?
```

### Plan Template
```markdown
# Feature Name - Technical Plan

## Architecture Overview
High-level design

## Technology Stack
What technologies?

## Implementation Details
How will it work?

## Performance Targets
How fast must it be?

## Security Considerations
What are the risks?

## Migration Plan
How do we get there?
```

### Task Template
```markdown
# Feature Name - Task Breakdown

## Phase 1: Name
### Task 1.1: Name
**Status**: NOT STARTED
**Description**: What needs to be done
**Files**: What files to modify
**Changes**: What changes to make
**Acceptance Criteria**: How do we know it's done?
```

## Next Steps

1. **Read the example**: Review `specs/features/fretboard-analysis/` for a complete example
2. **Start small**: Create a spec for a small feature first
3. **Get feedback**: Share specs with team before implementing
4. **Iterate**: Update specs as you learn
5. **Reflect**: After implementation, review what worked and what didn't

## Resources

- **Microsoft Spec-Driven Development Blog**: https://developer.microsoft.com/blog/spec-driven-development-spec-kit
- **Spec Kit GitHub**: https://github.com/github/spec-kit
- **Example Specs**: `specs/features/fretboard-analysis/`

## Questions?

- Check the example in `specs/features/fretboard-analysis/`
- Review this guide
- Ask the team
- Iterate and improve

