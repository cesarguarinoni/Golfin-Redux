DONE

Approved by Cesar 2026-08-28. Round-2 architect review PASS (see ARCHITECT_REVIEW.md tail).
Moved to Docs/Specs/Completed/. Unblocked content_art_bundling (now Active).

⚠️ THE LADDER WAS CORRECTED AFTER THIS APPROVAL. `content_art_bundling`'s acceptance item 8 ran
the ladder end-to-end on 2026-08-28 and found rule 2 SHADOWED: all four loaders defaulted "the
bundled row's URL" to `""` when there was no overlay, so a BUNDLED row carrying a URL compared its
own URL against `""` — always "different" — and rule 1 served the cached download in front of the
build's own sprite. §2.2 always said the build's own art wins; it did not. Fixed in the four
loaders in the same session, guarded by `ContentArtLadderHandoverTests`.

So the behaviour approved here and the behaviour on disk are not identical: what shipped at
approval rendered bundled-with-URL rows from the network cache, and what ships now renders them
from the build, as §2.2 specifies. A correction, not a change of intent — but worth knowing if
you read this folder later and wonder why the loaders differ from the approved diff.
