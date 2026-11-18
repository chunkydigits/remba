# 4. Reusable widget and integrations

## 4.1 Core requirement

The Add to Remba widget is a core product component and must be reusable across host environments.

**The same widget must be used on the Remba website and on third-party websites.** Remba's own website is the first host and the development/test bed. Do not build one add-item experience for the Remba website and a separate one for external partners.

The Remba site may launch the widget through an internal button or component; an external site may launch it through a partner script, button or SDK. These are different launch mechanisms, not different widget implementations.

## 4.2 Widget responsibilities

The widget owns the add-to-list user experience:
- Receive and interpret an item reference from the host.
- Resolve the item to an existing Remba entity or external identifier.
- Handle authentication or resume after sign-in.
- Retrieve the user's available lists.
- Show relevant suggested/site-standard destinations where configured.
- Allow selection of a destination list.
- Provide a quick-create-list path where appropriate.
- Add the entity to the selected list through the API.
- Prevent duplicates and show a clear result.
- Optionally allow the user to enter relevant entry attributes after selecting the list.
- Provide useful success, failure, ambiguous-match and retry states.

The host website must not need to implement Remba's list-selection, authentication, duplicate or entity-resolution rules.

## 4.3 Example invocation contract

Illustrative only; final API/SDK syntax to be designed:

```javascript
Remba.add({
  type: "movie",
  identifier: {
    type: "imdb",
    value: "tt0111161"
  }
});
```

The widget should accept one or more supported input forms:
- Existing Remba `entityId`.
- External identifier and identifier type (for example IMDb ID or ISBN).
- A provider/search reference that Remba can resolve.
- A reference to an entity shown in another public Remba list.
- A limited manual item payload for partner sites where no public identifier exists, subject to validation and anti-abuse controls.

The widget should not trust arbitrary metadata supplied by a host as canonical truth. The API must validate and resolve references server-side.

## 4.4 User experience

Typical flow:

1. User clicks “Add to Remba” on a page.
2. Widget opens in an overlay/modal or another isolated presentation appropriate to the host.
3. If the user is not signed in, show the Remba authentication flow and resume after successful authentication.
4. Widget identifies the item and displays enough information to confirm it is correct.
5. User selects an existing list or creates a new one.
6. Widget adds the item.
7. Widget confirms success, or explains that the item is already in that list.

The UI should include:
- Dropdown/list selector for the user's lists.
- A quick-add/create-list control.
- Suggested or site-standard list destinations only when configured and relevant.
- Item identity/preview.
- Clear loading, error, duplicate and success states.
- Responsive layout and keyboard accessibility.

Do not make a partner website responsible for knowing the user's Remba lists.

## 4.5 One widget, multiple hosts

### Remba website
- Use the same widget component as external hosts.
- Use it as the initial development/test bed.
- Internal launches may pass an authenticated session context through a secure first-party mechanism, but must not bypass API authorization.

### External websites
- Provide a simple integration script/SDK or embed mechanism.
- The partner page passes an item reference and invokes the widget.
- The widget communicates with Remba's API and authentication domain.
- The partner never receives the user's Remba access token, refresh token or credentials.

### Future browser extension
A browser extension may later detect supported item pages and launch the same widget. It is not MVP scope.

## 4.6 Technical isolation

The widget must not be tightly coupled to Angular app internals. It must be independently buildable and embeddable on sites using other frameworks or no framework.

Implementation options to evaluate:
- Web component with isolated styling/shadow DOM.
- Iframe-hosted widget with a small loader SDK.
- Bundled standalone JavaScript component.

Choose based on security isolation, Auth0 session handling, CSS/runtime isolation, accessibility, browser support, versioning and partner integration simplicity. The spec does not mandate one packaging approach yet.

## 4.7 Integration security

- Register/allowlist partner origins where appropriate.
- Validate origin and integration identity server-side; do not rely on a caller-supplied origin string alone.
- Use a secure token/session exchange or redirect-based authentication flow.
- Do not expose long-lived credentials or user tokens to host pages.
- Protect against CSRF, clickjacking, replay and malicious payloads as appropriate to the chosen embedding model.
- Apply rate limits and input validation.
- Restrict partner capabilities to the minimum required to resolve an item and launch the add flow.
- Treat all host-provided item metadata as untrusted input.

## 4.8 Site-standard or suggested lists

Some partner sites may have relevant destination suggestions, such as a site-specific “Books to read” list. Treat these as partner-provided suggestions or templates, not as privileged access to the user's account.

The user must remain in control of the actual destination list. A suggested destination must not create or select a list without user confirmation.

Detailed partner template configuration is a later design decision; do not overbuild it for MVP.

## 4.9 Widget acceptance criteria

- The widget can be launched from the Remba website and completes the same add flow used by an external host.
- The widget can be embedded/launched from a simple non-Angular test page.
- The host supplies an external identifier or Remba entity ID and the widget resolves it correctly.
- Unauthenticated users can sign in and return to the pending add flow.
- Authenticated users can select an existing list and add the item.
- Users can create a destination list through the widget if that feature is enabled.
- Duplicate additions are prevented by the API and surfaced clearly.
- A successful add creates a user-owned entry referencing the canonical entity.
- No Remba credentials or tokens are exposed to the partner page.
- Widget CSS does not unexpectedly alter host page styles, and host CSS does not break widget controls.
