---
name: utc-datetime
description: "Return the current UTC datetime. Use this skill whenever you need to populate a timestamp field such as lastUpdated in agent tracking."
---

# UTC Datetime Skill

## Overview

This skill returns the current UTC date and time in a consistent format for use in agent tracking.

## Format

Return the current UTC datetime as an **ISO 8601** string in the format:

```
yyyy-MM-ddTHH:mm:ssZ
```

Example: `2026-02-14T09:30:00Z`

## How to Obtain the Value

Run the following PowerShell command to get the current UTC datetime:

```powershell
(Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
```

## Usage

Whenever a field requires a UTC timestamp (such as `lastUpdated` in implementation plan frontmatter), invoke this skill to obtain the current value rather than hard-coding or estimating a date.