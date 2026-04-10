---
applyTo: "**/.agent-context/knowledge/**"
---

# Agent Knowledge Documentation Standards

Agent knowledge documentation lives in `.agent-context/knowledge/` and is consumed by agentic AI during the planning process. It must be detailed enough to be useful for implementation planning, but not so verbose that it pollutes agent context or so brittle that minor code changes break it.

## Purpose

An agent reading a knowledge doc should understand:
- What the feature/concept does and why it exists
- Domain knowledge that it may not infer from code alone (e.g., business rules, domain-specific terminology)
- How components interact across architectural layers
- Where to find key files (paths relative to repo root)
- How to plan a new implementation or extend existing functionality

## Document Structure

```markdown
# {Topic Title}

Brief one-paragraph overview explaining what this is and why it matters.

## Architecture / Overview

High-level explanation of how components interact across layers.
Include a diagram if helpful (ASCII or Mermaid).

## Key Components

| Component | Purpose |
|-----------|---------|
| `Name` | Description |

## {Topic-Specific Sections}

Detailed explanation with tables, status flows, and file paths.

## Creating/Extending {Topic} (if applicable)

Step-by-step checklist for adding new instances.
```

## Content Guidelines

- Be concise but complete — enough detail for an agent to plan an implementation, not a tutorial
- Use tables for structured information (components, file paths, enums, statuses)
- Show file paths relative to repository root
- Include status flows for stateful entities
- Add checklists for implementation guides (e.g., "Creating a new X" sections)
- Reference related knowledge docs where relevant
- Include short code snippets only where the pattern isn't obvious from file paths alone (max 10-15 lines)

## Avoid

These make knowledge docs brittle or wasteful:

- Duplicating information already in `.github/instructions/` files
- Over-explaining obvious patterns that are clear from well-named code
- Including full code implementations (reference file paths instead)
- Verbose prose where a table or bullet list suffices
- Hardcoding specifics that change frequently (prefer describing the pattern over listing every instance)
- Granular details of every Entity property. Focus on the overall structure and key relationships instead or very important properties.
- Marketing language or padding

## Output Strategy

When creating or updating knowledge docs:

1. **Update existing file** if the topic logically extends or fits within an existing document
2. **Create new file** only if the topic represents a distinct concept not covered elsewhere
3. If creating a new file, add an entry to `.agent-context/knowledge/README.md`

## Quality Checklist

- [ ] Clear purpose — an agent reading this can understand what the feature does and how layers interact
- [ ] Architecture overview shows component relationships, not just a file list
- [ ] Tables used for structured data (components, paths, enums, statuses)
- [ ] Practical guidance — an agent can plan a new implementation or extension from this
- [ ] Status flows included for stateful entities (if applicable)
- [ ] Code examples kept short and relevant (max 10-15 lines)
- [ ] No duplication with instruction files in `.github/instructions/`
- [ ] README.md index updated (for new files)
