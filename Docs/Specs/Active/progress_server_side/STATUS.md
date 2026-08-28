READY_FOR_SELF_REVIEW

iter-1. Every §6 step done, including the live E2E (§21 / acceptance item 1),
which RAN on prod end to end: two real level-ups on char_james driven through the
real widgets, all three server rows verified by SQL for both, and a cost change
published from the LIVE admin UI producing cost_changed → re-price → second tap
pays the new sum. The test change was reverted and `export --check` is clean.

Three deployment proofs in IMPLEMENTER_REPORT.md § Deployment:
  (a) dashboard backlog   577be843-4808-4aad-ade7-648d8a5f7c20
  (b) API image           playlife-api:deployment-01M13JGS6V9HWAENJS254ZAKDF (v56)
  (c) Level Costs panel   c927bde9-dc72-478a-9232-5ab78b2c158c
      + 96e5ad86-8466-466b-a3a4-8d9356ccf694, the sidebar-label fix, stamped
      6ccd4a8a2 == HEAD and READ OFF THE LIVE PAGE (the footer stamp is visible
      in the browser, which is a better §23 proof than the bundle grep).

One defect found and fixed inside this iteration: the new panel's sidebar entry
rendered the raw key `nav.level-costs`. Fixed, redeployed, and the shape closed
mechanically — PanelDef.id is now derived from the dictionary, so a panel with no
label is a compile error (proven with a tripwire).
