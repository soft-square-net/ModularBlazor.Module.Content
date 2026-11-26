# Posts of shared contents

Phase 1 of the Content Designer plan (`Module.ContentDesigner/docs/design-plan.md`, sections 2 and 8): a post is a document
with a URL, composed of shared contents that sections work on and the Desk reviews.

## Model

| Aggregate | Holds |
|---|---|
| **Post** (`articles`) | Kind (`SchemaId`/`SchemaVersion`: a DynamicFields root field), root values, slugs per culture (`post_slugs`), the ordered references to contents (`post_contents`: order, required, pinned revision), the post workflow, translations, the published snapshot |
| **ContentItem** (`contents`) | One paragraph/image/video/...: node id, field key and version, values, section, assignee, stage key, accepted revision, review note; `content_revisions` (version history) and `content_history` (audit) |
| **Pipeline** (`pipelines`) | The tenant's pipeline definition (stages, actions, who), published by Elsa; the built-in default applies until then |
| **FeedEntry** (`events`) | The outbox of the change feed |

- `ContentItem.Revision` counts its values; `ContentItem.Version` counts every change and is the optimistic token callers send back.
- `Post.DocumentJson` is the **working** document: root values plus each content's current revision, in order. It is refreshed in the same save whenever a content changes (`RecomposePostsOnContentChange`), so today's editor, the detail API and translations work as before.
- `Post.PublishedDocumentJson` is assembled at publication from the **pinned** revisions. The public API (`/public/{publicId}`) is unchanged.

## The pipeline

Stages and actions are data (`GET /pipeline`, `PUT /pipeline` with `content.pipeline`). A user action is
`POST /contents/{id}/act { action, version, note? }`: Content checks the stage it starts from, who may take it (`assignee`,
`creator`, or a permission on the content's press/category) and the note it needs, without calling Elsa.

The default:

| Stage | Kind | Edited by |
|---|---|---|
| `draft` | work | the assignee (the creator while none) |
| `desk` | review | Desk (`content.publish`): "edit as Desk" |
| `accepted` | accepted | Desk; an edit sends it back to `desk`, the accepted revision stays pinned |
| `rejected` | work | the assignee |

| Action | From → to | Who |
|---|---|---|
| `submit` | draft, rejected → desk | assignee |
| `accept` | desk → accepted (pins the revision) | `content.publish` |
| `reject` | desk, accepted → rejected (note required) | `content.publish`, `content.return` |
| `reopen` | accepted → desk | `content.publish` |

## Invariants

- A post is scheduled or published only when every **required** content has an accepted revision; optional ones without
  one are left out. Schedule and publish pin the accepted revisions; a due publication uses the pins taken when it was
  scheduled.
- A content used by any post cannot be deleted (`409`); archive it instead (posts that use it keep it, no new post can).
- A post uses a content once, and its contents' node ids are unique within it (they key the translations).

## The existing workflow

The post states and rights checks are unchanged. What the post steps now do to contents:

- `PUT /posts/{id}` with a whole document: the root goes to the post; children are matched to its contents by node id.
  A changed one gets a new revision (only a content made for this post; a shared one is edited in the library), a new one
  becomes a content, a missing one is detached (it stays in the library).
- **Approve** (reviewer): the post's own contents still in a work stage are submitted to the Desk.
- **Publish / Schedule** (Desk): the post's contents waiting in a review stage are accepted by that Desk user first, then
  the invariant is checked. The one-click flow still works; a **rejected** required content now blocks publication.

## Change feed

`GET /events?after=&take=` (`content.events` tenant-wide, for Elsa) and `GET /posts/{id}/events?after=` (anyone who sees the
post: its own events and those of the contents it uses, for the studio's hot reload). Same page shape and hold-back
(`Content:EventDelay`, 2 s) as Storage's feed. Types: `content.created`, `content.changed`, `content.state-changed`,
`content.assigned`, `content.archived`, `content.deleted`, `content.attached`, `content.detached`, `post.changed`,
`post.state-changed`. A brand-new post raises none until it is saved (its contents' `content.created` events carry it).

## Migration

`SharedContents` (all four engines) adds the tables and `articles.ContentsSplit` (false for existing rows). The data step
is `ContentBackfill`, a tenant initializer that runs after the schema migrations, behind the tenant database's advisory
lock: each unsplit post's top-level children become contents (revision 1, same node id). Scheduled and published posts'
contents are accepted and pinned; approved and awaiting-re-approval ones wait at the Desk; the rest start in `draft`.
Published snapshots are untouched. It writes no feed events.

## Not yet

- Translations stay per post (overlays on the working document); per-content translations come with the designer.
- Slugs are stored and edited (`PUT /posts/{id}/slugs`); no default slug is generated and there is no public lookup by
  slug yet (phase 8).
- `SectionId` is legacy: a content of a unit has the `section:{unit id}` scope (the department rota's grant), and
  `shifts.section-moved.v1` remaps sections to units (see the README's "Categories and units"). The column goes with S4.
- Feed retention (`events` grows); a sweep like Storage's comes with the Elsa phase.
