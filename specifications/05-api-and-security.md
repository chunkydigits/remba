# 5. API, security and privacy principles

## 5.1 API principles

- Use a REST API consumed by the Angular app, reusable widget and future partner integrations.
- Keep business rules in the backend application layer, not duplicated in clients.
- Use DTOs/contracts rather than exposing Cosmos documents directly.
- Validate all input server-side.
- Return consistent error shapes and meaningful validation/duplicate outcomes.
- Support pagination for entry and search results.
- Version external integration contracts deliberately; avoid breaking partner embeds without a migration path.
- Do not expose internal provider credentials or unrestricted entity-management operations to partner sites.

## 5.2 Preliminary resource model

This is a resource outline, not a final endpoint contract. Exact route names, HTTP methods, payloads and response schemas should be designed before implementation.

### Account/profile
- Get current Remba user.
- Update own profile and social links.
- Get public profile by username.

### Lists
- Create list.
- Get own list and list metadata.
- Update list metadata, visibility and attribute definitions.
- Delete list.
- List the current user's lists.
- Get public list by stable public identifier/slug.
- Update list view settings.

### Entries
- List entries for a list (paged, with sort options).
- Add entity to list.
- Get entry.
- Update entry attributes.
- Reorder entries.
- Remove entry.

### Entities
- Resolve by Remba entity ID.
- Resolve/search by external identifier.
- Search entities.
- Create a manual entity where no match exists.

### Widget/integration
- Resolve widget item input.
- Get current user's destination lists.
- Create destination list if supported.
- Add resolved entity to selected list.
- Register/validate partner integration as required by chosen security model.

Public endpoints must return only public fields. Private owner operations must always verify ownership/permissions in the API.

## 5.3 Authentication and authorization

- Auth0 authenticates users.
- The API validates access tokens, issuer, audience, signature and expiry.
- Map the authenticated Auth0 subject to an internal Remba user ID.
- Do not accept a client-supplied `ownerId` as proof of ownership.
- Every list/entry read or write must enforce the relevant owner or public visibility rules.
- Public list reads may be anonymous, but public access must not permit mutations.
- The widget must use a secure authentication flow and must not reveal credentials to the host website.

## 5.4 Privacy rules

- Private lists are accessible only to their owner and authorised backend processes.
- Public lists expose only explicitly public list data and approved entry/entity attributes.
- Entry attributes are private by default unless the owner explicitly chooses to display them publicly.
- Do not assume that making a list public makes every personal attribute public.
- Never expose another user's entry attributes when reusing the same canonical entity.
- Public profiles and social links are controlled by profile visibility settings.
- Do not leak private list names, IDs, counts or metadata through search, error messages, Open Graph tags or APIs.

## 5.5 External providers and entity metadata

- Prefer stable provider identifiers for matching.
- Respect API terms, licensing, attribution and caching restrictions.
- Keep provider access server-side where keys or terms require it.
- Store only metadata that is useful and permitted.
- Do not scrape sites or reuse copyrighted metadata/images without confirming permission and applicable terms.
- A provider outage must not prevent users from creating a manual entity.
- Keep provider-specific integration behind a service boundary so providers can be added or replaced.

## 5.6 Duplicate and concurrency protection

Duplicate prevention is a backend invariant. The API must prevent the same canonical entity being added twice to the same list, including when two requests arrive concurrently.

Before choosing the Cosmos DB implementation, validate partitioning and transaction boundaries. Possible approaches include deterministic entry IDs derived from list/entity identity or another unique mapping strategy. Do not assume a normal read-before-write check is sufficient under concurrency.

## 5.7 Data lifecycle and deletion

- Deleting an entry removes only that user's relationship to the entity; it must not delete a shared canonical entity that other lists use.
- Deleting a list removes or safely invalidates its entries.
- Deleting a user account must be designed to remove or anonymise personal records while preserving shared canonical entities where lawful and appropriate.
- Define retention and backup behaviour before launch.
- Do not store indefinite full audit history in main documents.

## 5.8 Analytics and future commercial reporting

Future aggregated reporting may identify broad trends, such as how many users have a particular film on a watchlist but have not watched it. This is not MVP functionality.

Any such reporting must:
- Use a lawful basis and clear user-facing transparency.
- Avoid selling or exposing identifiable user activity.
- Aggregate and anonymise data, with sensible minimum cohort thresholds.
- Exclude private/sensitive user content unless there is a clearly justified and lawful basis.
- Respect user privacy settings and applicable UK GDPR obligations.
- Respect external data-provider licensing and usage restrictions.

Do not design the commercial model around selling identifiable lists or individual activity.

## 5.9 Operational security

- Use managed secrets/key storage; never commit secrets.
- Apply least privilege to Azure resources and service identities.
- Log security-relevant events without logging access tokens, passwords or unnecessary personal data.
- Rate-limit authentication-sensitive, search and integration endpoints.
- Validate URLs and reject dangerous schemes such as `javascript:`.
- Use HTTPS everywhere.
- Configure CORS narrowly for known origins; CORS is not a substitute for authentication or authorization.
