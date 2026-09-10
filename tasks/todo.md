# asset_loans_offers — implementation plan (2026-09-10)

## Phase 1 — server (playlife)
- [ ] `backend/migrations/2026_09_10_golfin_loan_offers.sql` (idempotent, verification block)
- [ ] `routers/loans.py` — constants, `_is_locked`, `_expire` offers, `list_loans` offers_in,
      `lend` new refusals, `accept` / `decline` / `rescind`
- [ ] `routers/user.py` — `golfin_loan_offers` on update + `for_loans` on search
- [ ] `routers/followers.py` — `for_loans` on following
- [ ] `tests/test_loans.py` + `tests/test_user.py`

## Phase 2 — client C#
- [ ] `LoanDtos.cs` — OffersIn, OfferedAt/OfferExpiresAt/AnsweredAt, statuses, error keys
- [ ] `LoanService.cs` — Apply(offered), OffersIn, IsOffered, Accept/Decline/Rescind, SearchUsers
- [ ] `Endpoints.cs` — UserSearch, LoansAccept/Decline/Rescind, SocialFollowing(forLoans)
- [ ] `LoanSyncBehaviour.cs` — offered lock, terminal toasts, accepted toast, Home refresh
- [ ] `UserDetailDto.cs` / `UserService.cs` — golfin_loan_offers
- [ ] `LoanModalController.cs` — search field, RESULTS section, debounce, stale guard
- [ ] `LoanRibbonView.cs` — OFFERED status text
- [ ] `CharacterDetailPanel.cs` / `ClubDetailPanel.cs` — OFFERED state + RESCIND
- [ ] `LoanRescindModalController.cs` — NEW
- [ ] `LoanOfferModalController.cs` — NEW
- [ ] `LoanOfferPillController.cs` — NEW + HomeScreenController hook
- [ ] `LoanOffersToggle.cs` — NEW + SettingsController

## Phase 3 — authoring
- [ ] `LoanUiBuilder.cs` — search field, offer modal, rescind modal, pill, settings row

## Phase 4 — strings
- [ ] 34 rows EN+JA → import PLAN → --apply → publish texts → --check clean

## Phase 5 — verification
- [ ] EditMode tests, captures, report
