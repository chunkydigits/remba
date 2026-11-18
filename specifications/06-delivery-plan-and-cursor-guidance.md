# 6. Delivery plan and Cursor guidance

## 6.1 Delivery phases

Build in small, verifiable vertical slices. Each phase should leave the application in a runnable state and include automated tests appropriate to the repository's supplied standards.

### Phase 0 — project foundation
- Review repository and the coding standards supplied by the product owner.
- Establish Angular app, .NET API, local configuration and Azure deployment structure.
- Configure Auth0 application/API settings and environment configuration.
- Configure Cosmos DB and local development approach.
- Establish health checks, structured logging, error handling and CI/CD.

### Phase 1 — accounts and identity
- Sign in/sign out.
- Resolve Auth0 subject to internal Remba user ID.
- Create/read/update own user record.
- Protect API endpoints and verify ownership enforcement.

### Phase 2 — basic lists
- Create, read, update and delete lists.
- Private by default.
- List user's own lists.
- Add list-level view settings.

### Phase 3 — entries and attributes
- Store entries as separate `user-lists` documents.
- Add/remove/update entries.
- Implement manual ordering and sorting independently.
- Implement basic custom attribute definitions and values.
- Add conservative type detection and safe type changes.
- Enforce no duplicate entity per list.

### Phase 4 — entities and matching
- Create canonical entities.
- Resolve Remba IDs and external identifiers.
- Search entities and present ambiguous matches.
- Support manual entity creation.
- Keep personal relationship data on entries.

### Phase 5 — public profiles and lists
- Profile visibility and public profile routes.
- Public list routes and read-only presentation.
- Public attribute selection/redaction.
- Stable shareable URLs and social preview metadata.
- Add entity from public list to visitor's own list.
- Add list-level external links.

### Phase 6 — reusable Add to Remba widget
- Build widget as an isolated reusable component.
- Launch it from the Remba website first.
- Implement item resolution, authentication/resume, list selection, quick list creation and add flow.
- Test it from a separate non-Angular host page.
- Finalise embedding/SDK approach and security contract.

### Phase 7 — selected external integrations
- Add partner registration/allowlisting as required.
- Integrate one or more selected third-party sites.
- Monitor widget success, failures and duplicate handling.

### Future phases
- Collaboration/shared lists.
- Browser extension.
- Analytics and aggregated reporting.
- Full audit history.
- Advanced discovery and metadata enrichment.

## 6.2 MVP definition of done

MVP is not complete until a user can:
1. Sign in and have an internal Remba user record.
2. Create a private list.
3. Define at least one custom entry attribute.
4. Add an entity, including a manually created entity when no external match exists.
5. Add the same entity to another list, but not twice to the same list.
6. Edit personal entry attributes without changing the canonical entity or another user's entry.
7. Reorder entries manually, sort by another column, and return to the original manual order.
8. Have the last-used sort/view settings retained.
9. Publish a list and view it anonymously without exposing private attributes.
10. Add an entity from another person's public list to their own list, with their own attributes.
11. Attach a relevant external video/article link to a list.
12. Use the same Add to Remba widget from the Remba website and a separate test host page.
13. Complete the widget flow without exposing Remba credentials to the host page.

## 6.3 Cursor working instructions

Cursor should treat this specification pack as the product source of truth.

Before implementing:
- Read all specification files.
- Read the repository's existing structure and all coding standards/rules supplied by the product owner.
- Identify conflicts between existing code, supplied standards and this specification; report them before making broad changes.
- Propose a concise implementation plan for the next vertical slice, including affected projects, data changes, API contracts and tests.
- Ask for clarification only when a missing decision blocks safe implementation. Otherwise record assumptions explicitly and proceed with reversible choices.

During implementation:
- Implement one phase/slice at a time; do not attempt the whole platform in one change.
- Keep the solution aligned with the confirmed stack: Angular, .NET Web API, Azure, Cosmos DB and Auth0.
- Keep business rules in the API/application layer and share them across website/widget/integrations.
- Do not duplicate the Add to Remba experience. The Remba site must use the same reusable widget intended for external sites.
- Keep the three core Cosmos collections unless a concrete data/query/transaction requirement justifies another.
- Do not embed all entries in the list document.
- Do not merge entities on name alone.
- Enforce duplicate prevention server-side and under concurrent requests.
- Keep personal attributes on entries and canonical attributes on entities.
- Keep private data private even when the canonical entity appears in public lists.
- Do not implement collaboration, analytics, browser extension or full audit history as MVP features.
- Do not add packages, abstractions, frameworks or infrastructure without explaining why they are needed.
- Do not hardcode secrets, environment-specific URLs or user IDs.
- Add tests for business rules, authorization, data access and important failure paths.
- Keep documentation and configuration examples in sync with implementation.

After each slice:
- Run the relevant automated tests and build checks.
- Report what changed, what was verified, any known gaps and the next recommended slice.
- Do not claim a test passed unless it was actually run successfully.

## 6.4 Decisions to validate before locking implementation

These are implementation/product questions not fully resolved yet:

1. **Cosmos partitioning:** determine partition keys and transactional boundaries for list/entry documents and entity lookups.
2. **Widget packaging:** choose web component, iframe or another isolated embed mechanism after a small technical spike.
3. **Public list route:** settle username/slug format and URL stability rules.
4. **Profile defaults:** decide whether public profile visibility is opt-in and how a profile with no public lists behaves.
5. **Public attributes:** define the simplest UI for selecting which entry attributes are visible on a public list.
6. **Attribute storage:** finalise how dynamic values are represented and queried, including dates and type conversion.
7. **Manual ordering:** choose a robust ordering algorithm for large lists and concurrent changes.
8. **Entity resolution providers:** select the first external identifier/provider integrations for MVP; keep the system provider-agnostic.
9. **List deletion:** choose synchronous or staged deletion behaviour consistent with Cosmos limits.
10. **Account deletion:** define anonymisation/deletion and canonical entity retention rules.
11. **Username rules:** uniqueness, case handling, reserved words and rename behaviour.
12. **Public discovery:** determine whether public lists are discoverable/searchable or only accessible by shared URL in the first release.

## 6.5 Out of scope unless separately approved

- Task/reminder functionality.
- Social network feed or follower system.
- Messaging or comments.
- Collaborative editing.
- Automatic public exposure of all user data.
- Automatic entity merging by fuzzy name match.
- Selling identifiable user lists or individual activity.
- Separate implementations of the add-item widget for first-party and third-party hosts.
