DONE

auth_golf_profile — approved by Cesar 2026-09-02.

  geometry   45 sites 0 FAIL 0 GONE
  lint       fail=0 on both prefabs
  tests      EditMode sweep 326 passed / 0 failed; this task's 11 tests confirmed EXECUTED
             by name, not merely "not failed"
  backend    migration live (both CHECK constraints proven by 23514), playlife-api v64 -> v65,
             PUT/GET round trip PASS, 422 on both bad enums
  strings    28 keys published (texts v29), export --check clean, content_version.txt committed
  shipped    GolfinRedux e5964a46f + 0afbb32f5 · playlife 22e79b6

Open, and carried forward rather than closed:
  * PUT /user/update cannot clear a field to NULL — the flip side of "omitted is preserved".
    A future Settings screen that lets a player UN-SET a handicap needs an explicit-null
    contract or a dedicated clear endpoint.
  * Rubik:Medium still resolves to the variable face, so Medium runs render ~5% narrow and
    the Welcome sub wraps one word later than the node. Importing a real Medium TMP asset
    would fix it across every GPS screen at once.
  * Unverified on device: the 409 duplicate-nickname path (needs a second account), the
    mobile keyboard behaviour, and the Japanese strings.
