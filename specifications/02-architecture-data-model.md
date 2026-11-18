# 2. Architecture and data model

## 2.1 Confirmed technology choices

- Frontend: Angular.
- Backend: .NET Web API.
- Hosting: Microsoft Azure.
- Primary data store: Azure Cosmos DB, using JSON documents.
- Authentication: Auth0.
- Integration approach: REST API, with a reusable embeddable Add to Remba widget.

The product owner will supply existing coding standards, repository rules and preferred implementation patterns before coding starts. Do not assume a different stack or impose generic conventions that conflict with those standards.

## 2.2 Architectural shape

Use an API-first architecture. The Angular website, public list pages, Add to Remba widget and future external integrations must call the same backend application services and REST API. Do not duplicate business rules in separate frontends.

Conceptual layers:

1. **Angular application** — signed-in experience, list management, public profiles and public list pages.
2. **Reusable widget** — isolated UI for adding an item to a Remba list. It is hosted inside the Remba website first and later embedded by external sites.
3. **.NET API** — authentication/authorization, validation, application workflows, entity resolution and persistence.
4. **Cosmos DB** — users, lists/entries and canonical entities.
5. **External providers** — optional metadata/identifier lookup services, accessed server-side where credentials or provider terms require it.

The widget should not depend on the Angular application's internal components or runtime. It may use a compatible frontend technology internally, but it must be independently buildable and embeddable. Final packaging choice (for example web component, iframe or bundled script) is an implementation decision to validate. Prefer a design that isolates CSS/runtime and works on arbitrary host technologies.

## 2.3 Core Cosmos DB collections

Keep the initial data model to three core collections:

1. `users`
2. `user-lists`
3. `entities`

Additional collections require a concrete justification. Do not create containers for every entity type, list type or attribute type.

### `users`
Stores Remba-specific user profile and preferences. Auth0 remains the identity provider; do not use the raw Auth0 subject as the public Remba user ID. Create an internal Remba user ID and store the Auth0 subject as a protected identity mapping field.

### `user-lists`
Stores two logical record types:
- List documents (`recordType: "list"`).
- Entry documents (`recordType: "entry"`).

This keeps the number of core collections low while allowing entries to be independently queried and avoiding a growing embedded array in the list document.

### `entities`
Stores canonical entities shared by users' lists. An entity represents the thing itself, not an individual user's relationship with it.

## 2.4 Conceptual list document

The following is illustrative, not a locked schema:

```json
{
  "id": "listId1",
  "recordType": "list",
  "ownerId": "userId1",
  "visibility": "private",
  "name": "Films to Watch",
  "description": "Films I want to see",
  "configuration": {
    "entityType": "movie",
    "attributes": [
      {
        "key": "dateWatched",
        "name": "Date Watched",
        "type": "date",
        "scope": "entry"
      },
      {
        "key": "needToWatch",
        "name": "Need to Watch",
        "type": "boolean",
        "scope": "entry"
      }
    ]
  },
  "viewSettings": {
    "sortBy": "name",
    "sortDirection": "ascending",
    "viewMode": "list"
  },
  "externalLinks": [],
  "added": "2026-11-16T09:30:00Z",
  "addedByUserId": "userId1",
  "edited": null,
  "changedByUserId": null
}
```

Notes:
- Use `name` and `description` as clear top-level fields in the implementation unless a strong reason emerges to use a generic attribute bag for core system fields.
- `configuration.attributes` contains definitions for user-defined attributes. Each definition must have a stable key independent of its display name.
- `entityType` may be absent or generic for lists that contain arbitrary personal notes or mixed content. Do not force a category where the user has not chosen one.
- `viewSettings` initially belongs to the list because lists have one owner. When collaboration is introduced, view settings must become per-user so one person's sort choice does not affect another's view.
- Do not maintain an unbounded `items` or `entryIds` array on this document. Query entry records by `listId` and owner/partition scope.

## 2.5 Conceptual entry document

```json
{
  "id": "entryId1",
  "recordType": "entry",
  "listId": "listId1",
  "ownerId": "userId1",
  "entityId": "entityId1",
  "added": "2026-11-16T09:35:00Z",
  "edited": null,
  "order": 1,
  "attributes": {
    "needToWatch": true,
    "dateWatched": null
  }
}
```

Notes:
- Entry documents are independently stored and queried.
- `listId` is a structural relationship, not a user-defined attribute.
- `ownerId` is the Remba user who owns the entry/list relationship.
- `entityId` references the canonical entity.
- `order` is the persistent manual order. It must not be overwritten when a user sorts by another column.
- Entry `added` is immutable. `edited` remains null until the entry itself is changed.
- Changes to the canonical entity do not update the entry's `edited` value.
- A public list visitor adding an entity creates a new entry owned by that visitor; it does not reuse or modify the publisher's entry.

## 2.6 Conceptual entity document

```json
{
  "id": "entityId1",
  "type": "movie",
  "name": "The Shawshank Redemption",
  "identifiers": [
    { "type": "imdb", "value": "tt0111161" }
  ],
  "metadata": {},
  "added": "2026-11-16T09:30:00Z",
  "addedByUserId": "userId1",
  "edited": null,
  "changedByUserId": null
}
```

Notes:
- Remba generates its own entity ID for every canonical entity.
- External identifiers are optional and can include multiple provider IDs.
- Examples include IMDb IDs, ISBN-10/13, MusicBrainz IDs, GTIN/EAN, Google Place IDs and Wikidata IDs.
- Names are not unique. Never merge entities on name alone.
- When no reliable public identifier exists (for example a personal idea or note), create a Remba entity with an internal ID.
- Ambiguous matches must be shown to the user for confirmation; do not automatically merge.
- Keep core identity separate from provider metadata. Retrieve/cache public metadata selectively and only in accordance with provider licensing, API terms and caching rules.
- Personal information such as watched/read dates, personal notes and the user's status belongs on the entry, not the canonical entity.

## 2.7 Attribute definitions and values

A list defines the attributes relevant to its entries. An attribute definition should support, at minimum:

- `key`: stable machine key.
- `name`: user-facing label.
- `type`: `text`, `number`, `boolean` or `date` for MVP.
- `scope`: `entry` or `entity`.
- Optional future/advanced metadata: `required`, `searchable`, `aggregatable`, `private`, `displayByDefault`.

Do not expose advanced flags in the initial UI unless a feature requires them. Attribute values should be stored as appropriate JSON types (string, number, boolean, date representation), not all coerced to strings.

The system must distinguish:
- **Entity attributes:** describe the canonical thing, e.g. title, author, director, ISBN.
- **Entry attributes:** describe this user's relationship with the thing, e.g. date read, seen, purchased, personal notes.

Recognised system fields should include relevant concepts such as `name`, `description`, `image`, `link` and `listId`. These should be consistently modelled as system fields rather than implemented as arbitrary one-off exceptions. Structural fields such as IDs and relationships remain structural properties in the data model.

## 2.8 Attribute type detection

Do not force users to select a data type before entering a value.

Initial behaviour:
- User enters a value.
- Remba may infer a likely type, such as date or number.
- If inference is uncertain or would change interpretation, ask a simple confirmation, e.g. “Does this look like a date?” with options to use Date or keep as Text.
- User can correct the type.
- Type changes later must validate/convert existing values safely. If conversion is lossy or invalid, explain the impact and do not silently discard data.

The inference mechanism should be conservative. Text is the safe fallback.

## 2.9 Sorting and ordering

- Every entry has a persistent manual `order` value.
- Manual order is the user's canonical custom sequence.
- Sorting by name, date added or another attribute changes only the displayed sort, not `order`.
- Returning to manual order restores the stored sequence.
- Save the last-used sort field, direction and view mode in `viewSettings`.
- Define a deterministic tie-breaker for equal sort values (for example manual order, then entry ID).
- Reordering entries updates their manual order values only.
- Avoid unnecessary full-list rewrites when reordering; choose an order representation that is robust for expected list sizes.

## 2.10 Audit fields and history

For relevant records, maintain current-state audit fields:
- `added`: UTC timestamp of creation.
- `addedByUserId`: user responsible for creation where applicable.
- `edited`: null until modified; then UTC timestamp of last modification.
- `changedByUserId`: user responsible for the latest modification where applicable.

For entities created by an integration or background process, the actor may be a system/integration identity rather than a human user. Model this explicitly rather than inventing a user ID.

These fields are not a full audit trail. A future `audit-history` collection may record each significant change, actor, timestamp, changed fields and old/new values. This is specified as nice-to-have/post-MVP and must not be implemented in MVP.

## 2.11 Cosmos DB design decisions to validate before implementation

The conceptual schema is not sufficient to choose a Cosmos partition key. Before finalising containers, validate:
- Primary query patterns for user lists and entries.
- Whether list and entry documents can share a partition key and transactional batch boundary.
- How entity identifiers are looked up efficiently and uniquely.
- How to enforce no duplicate entity per list under concurrent writes.
- Cross-partition query cost for public list pages and entity reuse.
- Indexing policy for dynamic attributes and sorting.
- TTL/retention needs, if any.

Do not assume Cosmos DB can enforce a unique constraint across arbitrary document properties. Use an appropriate deterministic ID/unique-key strategy or transactional design for duplicate prevention, consistent with the selected partitioning model.
