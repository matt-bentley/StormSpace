---
description: "Analyze the codebase and create or update agent knowledge documentation."
name: "Add Agent Knowledge"
argument-hint: "Describe the topic, feature, or concept to document"
model: "Claude Opus 4.6"
agent: agent
---

# Add Agent Knowledge

Create or update agent knowledge documentation in `.agent-context/knowledge/` based on codebase analysis **and user insight**. Code reveals _what_ and _how_, but often not _why_, _when_, or _what tradeoffs were considered_. Actively ask the user to fill in knowledge gaps that cannot be inferred from the implementation alone.

## Workflow

### Step 1: Understand the Request

Extract the topic or concept from the user's input. Examples:
- "Document the notification system"
- "Add knowledge docs for the scheduler"
- "Document how large object storage works"

If the topic is unclear, ask for clarification.

### Step 2: Load Project Context

Read the following to understand project structure and conventions:
1. `.github/copilot-instructions.md` - Key directories, tech stack, architecture principles
2. `.github/instructions/agent-knowledge.instructions.md` - Agent knowledge documentation standards (structure, content guidelines, quality checklist)
3. `.github/instructions/` - Coding standards and patterns for relevant file types
4. `.agent-context/knowledge/README.md` - Existing agent knowledge documentation index

### Step 3: Review Existing Knowledge Files

Read each knowledge file listed in the README to understand:
- What topics are already documented
- The style and level of detail used
- Whether the requested topic fits within an existing file

### Step 4: Determine Output Strategy

Follow the output strategy in `.github/instructions/agent-knowledge.instructions.md`.

### Step 5: Research the Codebase

Analyze the implementation using the project structure from `copilot-instructions.md`:

1. **Locate relevant code** using semantic and grep searches across all layers identified in the project structure (e.g., Core, Application, Infrastructure, API, Web)

2. **Understand the architecture**:
   - How components interact across layers
   - Key aggregates/entities and their relationships
   - Events, handlers, and integration patterns
   - Configuration and setup requirements

3. **Include frontend if applicable**:
   - Components, services, and models
   - UI patterns and design system usage
   - API integration points

4. **Identify file paths** useful for developers:
   - Entity and DTO locations
   - Handler and service locations
   - Configuration files
   - Test files

5. **Review existing tests** for usage patterns and expected behavior.

### Step 5b: Ask the User to Fill Knowledge Gaps

After researching the codebase, identify aspects of the topic that **cannot be reliably inferred from code alone**. Ask the user targeted questions covering areas such as:

- **Design rationale** — Why was this approach chosen over alternatives?
- **Historical context** — Were there previous implementations or migrations? What prompted the current design?
- **Implicit constraints** — Are there performance budgets, compliance rules, or external SLAs that shaped the design?
- **Edge cases and known limitations** — Are there gotchas, workarounds, or planned improvements the code doesn't make obvious?
- **Cross-team or organisational context** — Does this feature interact with systems, teams, or processes outside the codebase?
- **Intended future direction** — Is this considered stable, or are changes planned?

Present your questions in a single batch so the user can answer efficiently. Incorporate their answers into the documentation. If the user declines to answer or doesn't know, note the gap and proceed with what's available.

### Step 6: Write Documentation

Follow the document structure, content guidelines, and avoidance rules defined in `.github/instructions/agent-knowledge.instructions.md`.

### Step 7: Update README Index

If creating a new file, add an entry to the Contents table in `.agent-context/0-knowledge/README.md`:

```markdown
| [{filename}.md](./{filename}.md) | Brief description |
```

Place the entry in logical order among existing entries.

### Step 8: Validation

After creating/updating documentation:
1. Verify all referenced file paths exist
2. Ensure code snippets are accurate
3. Confirm the documentation matches current implementation
4. Check that the README index is updated (if new file)

---

## Output Summary

When complete, report:
- **Action taken**: Created new file / Updated existing file
- **File path**: Path to the created/modified documentation
- **Topics covered**: Brief list of what was documented
- **Related files reviewed**: Key source files analyzed

---

## Agent Knowledge Doc Quality Checklist

Use the quality checklist from `.github/instructions/agent-knowledge.instructions.md`.
