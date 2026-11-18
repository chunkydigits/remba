# Remba — specification pack

**Purpose:** Product and engineering specification for building Remba with Cursor.

**Status:** Baseline v1.0 specification, 16 November 2026. This pack records the decisions made so far. It is intended to guide implementation, but it is not a substitute for the project's coding standards, which the product owner will provide separately before implementation.

## Read in this order

1. [Product vision and requirements](01-product-requirements.md) — what Remba is, who it serves, MVP scope and product behaviour.
2. [Architecture and data model](02-architecture-data-model.md) — technology, service boundaries, Cosmos DB collections and conceptual document shapes.
3. [Functional specifications](03-functional-specifications.md) — accounts, lists, entries, attributes, entities, profiles and public lists.
4. [Widget and integrations](04-widget-and-integrations.md) — the reusable Add to Remba widget and third-party integration model.
5. [API and security principles](05-api-and-security.md) — preliminary API resource model, security, privacy and integration safeguards.
6. [Delivery plan and Cursor guidance](06-delivery-plan-and-cursor-guidance.md) — implementation sequence, acceptance criteria, open decisions and instructions for Cursor.

## Authority and interpretation

- The product requirements and decisions in this pack are the agreed baseline unless explicitly marked **proposed**, **to validate**, or **future**.
- Conceptual JSON examples illustrate the model. They are not final serialization contracts. Validate partition keys, indexing, transactional boundaries, concurrency and query patterns before locking schemas.
- Do not silently invent product behaviour for unresolved questions. Record the question and propose options before implementing a decision that materially affects users, data or security.
- Do not add features merely because they are common in other list, task or productivity apps.
- Keep MVP implementation deliberately small. Future capabilities must not complicate MVP flows without a clear architectural reason.
- The product owner will provide coding standards and repository-specific rules separately. Those standards take precedence over any generic style assumptions in this pack.

## Core product decisions at a glance

- Remba is a general-purpose platform for creating, managing, sharing and discovering lists. It is not a todo/reminder app.
- Lists can contain references to canonical entities, with personal attributes stored on each user's entry.
- Initial independent lists; collaboration is future scope.
- Three core Cosmos DB collections: `users`, `user-lists`, `entities`.
- A list and its entries are separate logical document types in `user-lists`, distinguished by `recordType`.
- One canonical entity may appear in many users' lists, but the same entity cannot appear twice in the same list in the MVP.
- Manual order is retained independently of temporary sorting. Last-used sort/view preferences are saved in list view settings for the initial single-owner model.
- Public lists are read-only to visitors. Visitors can add a referenced entity to one of their own lists, with their own independent attributes.
- External links can be attached to a list, for example a creator's YouTube or TikTok video about that list.
- The Add to Remba widget is one reusable component used on Remba itself and on external partner websites. Remba's own site is its first host and test bed.
- Angular frontend, .NET Web API, Azure hosting, Cosmos DB JSON documents, Auth0 authentication, REST API.
- Full audit history is specified as a future/nice-to-have capability, not MVP.
