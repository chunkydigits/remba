# 3. Functional specifications

## 3.1 Authentication and user account

- Use Auth0 for authentication.
- The API validates Auth0-issued access tokens and authorises access using the authenticated subject mapped to an internal Remba `userId`.
- Do not expose raw Auth0 subject values as public Remba IDs or use them as user-facing identifiers.
- On first successful sign-in, create or retrieve the Remba user record.
- User profile information is stored in `users`; authentication credentials remain with Auth0.

Conceptual user record:

```json
{
  "id": "userId1",
  "auth0Subject": "protected-auth0-subject",
  "username": "example",
  "displayName": "Example User",
  "profile": {
    "bio": "",
    "image": "",
    "visibility": "private"
  },
  "socialProfiles": [
    { "platform": "youtube", "url": "https://youtube.com/@example" },
    { "platform": "tiktok", "url": "https://tiktok.com/@example" }
  ],
  "added": "2026-11-16T09:30:00Z",
  "edited": null
}
```

Username uniqueness, reserved names, case normalisation and rename rules must be decided before public profile URLs are implemented.

## 3.2 User profiles

- A user may have a public Remba profile, particularly when they have public lists.
- Profile visibility is an explicit user choice; do not assume every account is publicly discoverable.
- A public profile can show the user's display name, username, profile image, bio, social links and public lists, subject to the user's visibility settings.
- Private lists must never appear on a public profile.
- A list's visibility is independent of the profile's visibility.
- Social profile links may include platforms such as YouTube, TikTok and Facebook. Validate URLs and supported platform formats.

## 3.3 List management

A user can:
- Create a list.
- Set/edit its name and optional description.
- Set visibility to private or public.
- Define the kind of entity it contains, where useful, without requiring a type for generic lists.
- Define custom attributes for its entries.
- Add external links relevant to the list.
- Change display/view preferences.
- Delete a list.

Initial visibility values:
- `private` — only the owner can access it.
- `public` — anyone with access to the public URL can view it.

Unlisted, shared and collaborative permissions are future capabilities. Do not imply that a public URL is private or secret.

Deleting a list must also address its entry records. Define and test deletion behaviour so orphaned entries are not left accessible. If the deletion cannot be performed atomically at scale, use a safe staged deletion strategy and ensure reads immediately respect the deleted/disabled list state.

## 3.4 Entry management

A user can:
- Add an entity to a list.
- View entries in a list.
- Edit their own entry attributes.
- Reorder entries manually.
- Sort entries by supported columns.
- Remove an entry from a list.

An entry is the relationship between one user/list and one canonical entity. It is not the entity itself.

### Duplicate policy

For MVP, the same canonical entity must not appear more than once in the same list. The same entity may appear in multiple different lists, including lists owned by different users.

When a user tries to add an entity already present in the destination list:
- Do not create another entry.
- Return a clear duplicate outcome to the UI.
- Do not silently create a second copy or merge personal attributes.

The backend must enforce this, including concurrent requests. UI-only duplicate checks are insufficient.

### Added and edited timestamps

- `added` is set when the entry is created and is immutable.
- `edited` is null until the entry itself is modified.
- Editing an entry attribute or entry metadata updates `edited`.
- Viewing an entry does not update `edited`.
- Removing and re-adding an entity creates a new entry with a new `added` timestamp.
- Updating canonical entity metadata does not update the user's entry timestamp.

## 3.5 List attributes and entry attributes

- Users can add custom attributes relevant to a list.
- The system supports basic types: Text, Number, True/False and Date.
- Users should be able to start entering values without choosing a type first.
- Attribute definitions have stable keys; changing the display label should not break existing values.
- Attributes may be optional or required where appropriate, but required-field configuration can be deferred if it complicates MVP.
- Users can edit an attribute definition. Type changes must validate existing values and protect against data loss.
- Removing an attribute should require clear handling of existing values (for example confirm deletion); do not silently orphan or reinterpret data.

Keep the distinction between entity and entry scope:
- Entity-scope data describes the thing and can be shared/reused.
- Entry-scope data describes the user's own relationship and is private by default.

## 3.6 Entity creation and identification

When adding an item, Remba should attempt to identify whether it already exists.

Identification priority:
1. Exact known external identifier match.
2. Existing Remba entity ID.
3. Provider search or other matching strategy, where supported.
4. Manual entity creation when no suitable match exists.

Rules:
- Never merge based on name alone.
- Multiple identifiers may refer to the same entity.
- If a match is ambiguous, show candidate results and ask the user to select the correct entity.
- If there is no reliable match, allow a new entity to be created with an internal Remba ID.
- Entity creation and entry creation should be treated as one user-facing workflow, with safe recovery if one step fails.

## 3.7 Public lists

A public list is read-only to visitors.

Visitors may:
- View the list title, description and public entries.
- View only the entry/entity attributes explicitly approved for public display.
- Open the list's external links.
- Choose an entity and add it to one of their own lists.

Visitors may not:
- Edit the owner's list name, description or configuration.
- Add, remove or reorder entries in the owner's list.
- Change the owner's entry attributes.
- Change the owner's profile or social links.

### Add an item from a public list

1. Visitor selects “Add to my Remba list” on an item.
2. If not authenticated, prompt them to sign in/sign up and then resume the action.
3. Show their own lists and allow them to choose a destination. Provide a quick path to create a new list if appropriate.
4. Create a new entry in the visitor's list referencing the same canonical entity.
5. Allow the visitor to set their own relevant attributes, such as `needToWatch = true`.
6. If that entity already exists in the visitor's destination list, prevent duplication and explain that it is already there.

The publisher's entry and attributes remain unchanged. The visitor does not inherit the publisher's personal data.

## 3.8 External links on lists

A list may contain links to external content about that list, such as:
- YouTube video.
- TikTok video.
- Blog post or article.
- Podcast episode.
- Other website or social post.
- Another Remba list, if useful.

A link should support, at minimum, URL and optional title/platform metadata. Validate schemes and reject unsafe URL schemes. Display external links clearly on the list page.

This creates a bidirectional relationship: a creator can link from their video to the Remba list, and the Remba list can link back to the video.

Individual entity-level external links may be considered later (for example a review attached to a film), but list-level links are the MVP requirement.

## 3.9 Public URLs and social previews

Public lists and public profiles need stable, shareable URLs. A friendly URL could follow a pattern such as:

- `https://remba.it/@username/list-slug`
- Or a stable ID-based route with a friendly slug.

The exact route and slug strategy is to validate. Ensure URL stability if a list is renamed. Use canonical URLs and appropriate Open Graph/social metadata so links preview well in platforms such as WhatsApp, Facebook and TikTok. Do not expose private list data in metadata or server-rendered previews.

## 3.10 Search and discovery

MVP search should support finding entities to add to lists and resolving duplicates. Do not overbuild a public discovery/recommendation engine in the first release.

Search results must distinguish candidates sufficiently for a user to identify the correct entity (for example title, type, year, author or other available metadata). Avoid automatic matching when confidence is low.
