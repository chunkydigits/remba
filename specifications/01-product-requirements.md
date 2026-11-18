# 1. Product vision and requirements

## 1.1 Product summary

Remba is a platform for people to create, organise, maintain, share, publish and discover lists of things they care about, want, own, have experienced or want to remember.

Examples include:
- Films to watch, films watched, favourite films.
- Books to read or books already read.
- Products to buy, own or compare.
- Places to visit or places already visited.
- Recipes, ideas, collections, recommendations and personal reference lists.
- Lists created by a person, creator, group or organisation.

Remba should be generic and extensible. It must not be built around a fixed catalogue of categories or a narrow set of pre-defined list types. Users should be able to create lists and define the attributes that matter to them without needing technical knowledge.

**Remba is not intended to be a todo list or reminder application.** Do not introduce task management, deadlines, notifications or productivity workflows as core assumptions.

## 1.2 Product goals

1. Make it very easy to create a list and add the first few entries.
2. Let users define what information matters for a particular list and its entries.
3. Avoid duplicate representations of the same real-world thing by reusing canonical entities where possible.
4. Let users keep their own relationship data private and independent, even when they share an entity or view someone else's public list.
5. Make public lists useful as shareable content and as a way for new users to discover Remba.
6. Make adding an item from another website straightforward through a reusable widget.
7. Establish an API-first foundation so the website, widget and future integrations use the same business rules.
8. Preserve a path to future collaboration, analytics and commercial reporting without building those features prematurely.

## 1.3 Product principles

- **Flexible by default:** lists and attributes are user-defined; avoid hardcoded categories and special cases.
- **Simple for ordinary users:** technical concepts such as entity identifiers, schemas and data types should not be exposed unnecessarily.
- **Personal data stays personal:** a shared entity does not mean shared personal notes, ratings, dates or status.
- **One source of truth for an entity:** where a canonical entity is recognised, lists should reference it rather than create separate copies.
- **Consistent add experience:** all ways of adding an item use the same underlying Add-to-Remba flow and rules.
- **Share by choice:** private is the safe default; publishing a list must not expose private personal attributes.
- **Build for extension, not speculation:** keep boundaries clean, but do not implement future features before they are needed.

## 1.4 Users and primary scenarios

### List owner
A signed-in user creates a list, defines its title and optional description, adds items, records personal information about each item, changes the view and optionally publishes the list.

### Public visitor
A visitor opens a public list. They can browse it and choose an item to add to one of their own lists. They cannot modify the owner's list or entries.

### Creator or publisher
A person publishes a list and links to it from an external video, blog, podcast or website. They can also attach links to that external content from the Remba list page, creating a two-way relationship.

### Partner website visitor
A user on a supported third-party site clicks an Add to Remba control. The reusable Remba widget identifies the item, authenticates the user if needed, displays their lists, and adds the item to the selected list.

## 1.5 MVP scope

The MVP should support:

- User registration/sign-in through Auth0.
- A Remba user record associated with the authenticated identity.
- Creating, viewing, editing and deleting lists.
- Private and public list visibility.
- Adding, viewing, editing, reordering and removing entries.
- User-defined list and entry attributes, with basic types.
- Canonical entity creation and reuse.
- External identifier matching and duplicate prevention.
- Searching entities and resolving ambiguous matches.
- Public list pages and an opt-in public user profile where applicable.
- Adding an entity from a public list to the visitor's own list.
- List-level external links.
- A reusable Add to Remba widget, first hosted within Remba and designed for external embedding.
- REST API used by the Angular application and widget.
- Basic validation, authorization, privacy controls and operational logging.

MVP should not require a broad third-party integration catalogue. The widget should be developed and tested within Remba first; external partner rollout can follow once the component and integration contract are stable.

## 1.6 Explicitly future or post-MVP

- Collaborative/shared lists and multiple editors.
- Browser extension.
- Large-scale third-party partner integrations and partner self-service portal.
- Advanced analytics and aggregated trend reporting.
- Commercial reporting products or monetisation based on aggregated insights.
- Full audit-history storage and UI.
- Advanced entity metadata enrichment from multiple providers.
- Advanced search, recommendations and discovery ranking.
- Unlisted links, granular sharing, and more complex access models if user demand warrants them.

These items should be considered when making architectural choices, but should not be delivered as part of the first MVP unless separately agreed.

## 1.7 Non-functional requirements

### Usability
- Creating a list and adding an item should be understandable without training.
- Attribute type selection should not be a barrier to entering data.
- Validation messages should explain what needs correcting.
- The public list must make it obvious that it belongs to another user and is read-only.

### Performance and scale
- Do not store an entire potentially large list and all entries in one Cosmos DB document.
- Design entry queries to be scoped to a list and owner.
- Avoid requiring an unbounded array of all entry IDs on the list document.
- Paginate or progressively load entries where appropriate.

### Reliability
- Prevent accidental duplicate entries in a list, including concurrent add attempts.
- Treat external provider lookups as fallible; users must still be able to add a manually created entity when no provider match is available.
- Avoid partial operations that leave orphaned or inconsistent records where possible.

### Privacy and security
- Private lists and private attributes must never be exposed through public endpoints.
- Public visitors must not be able to mutate another user's list or entries.
- Partner sites must never receive the user's Remba credentials.
- Minimise collection and retention of personal data.

### Accessibility and responsive design
- The website and widget should be usable on mobile and desktop.
- Interactive controls should be keyboard accessible and have accessible names and focus states.
- The widget must work in constrained embedded layouts and be responsive to its host container.

## 1.8 Success measures (initial hypotheses)

Instrument the product to understand:
- Time from registration to first list created.
- Time from list creation to first entry added.
- Percentage of users who create more than one list.
- Percentage of lists made public.
- Public list views that lead to an add-to-own-list action.
- Widget opens that lead to successful additions.
- Duplicate prevention frequency and entity-match success rate.

Do not treat these as targets until baseline usage exists. Analytics must respect privacy and consent requirements.
