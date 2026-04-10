# StormSpace - Knowledge Documentation

This folder contains high-level documentation for planning and building new features in StormSpace. This folder is used as context for AI planning and coding agents.

## Purpose

While the `.github/instructions` folder contains low-level coding standards and implementation patterns, this `.agent-context/knowledge` folder focuses on domain knowledge which cannot always be understood by looking at the code alone:

- **Application Context** - High-level usage context and why it exists
- **Design Information** - UI/UX concerns
- **Domain Model** - Domain design and organization of the codebase
- **Architecture Principles** - High-level patterns and decisions
- **Integration** - Integration patterns/points and dependencies

## Updates

This folder should be kept up-to-date, whenever new changes are applied to the repository. If a key new concept, technology or architecture is added, a new knowledge file must be created and added to the Contents index.

### Adding New Knowledge Documentation

Use the **Add Knowledge Documentation** prompt to create or update knowledge files:

```
/add-agent-knowledge Document the notification system
```

The prompt will:
1. Review existing knowledge files to check if the topic fits an existing document
2. Analyze the codebase to gather accurate implementation details
3. Create/update documentation following the established style
4. Update this README's Contents index for new files

**When to add knowledge documentation:**
- New bounded context or domain area added
- Significant architectural pattern introduced
- New user journey or UI/UX patterns
- Cross-cutting concern that affects multiple areas
- Complex feature requiring implementation guidance

## Contents

| Document | Description |
|----------|-------------|
| [application-context.md](./application-context.md) | StormSpace and Event Storming problem space and product vision |
| [domain-model.md](./domain-model.md) | Domain model design and breakdown of the system |
| [architecture-principles.md](./architecture-principles.md) | Key architectural decisions and patterns |
| [integration-points.md](./integration-points.md) | External systems and dependencies |
| [user-journeys.md](./user-journeys.md) | User journeys with browser selectors for automated testing |