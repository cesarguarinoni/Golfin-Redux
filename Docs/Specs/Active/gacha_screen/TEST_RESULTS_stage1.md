# Stage 1 EditMode test results (run by main thread via Unity MCP tests-run, 2026-07-09)

Both self-reviewer and reviewer lacked `tests-run`; the main orchestrator thread ran them. Raw runner summary:

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| `Golfin.Save.Tests.GachaTicketTests` | 11 | 0 | 0 |
| `SaveLayerTests` | 15 | 0 | 0 |
| `ClubOwnershipTests` | 9 | 0 | 0 |
| **Total** | **35** | **0** | **0** |

GachaTicketTests (all Passed):
- AddTickets_IncrementsBalance
- SpendTickets_Sufficient_DecrementsAndReturnsTrue
- SpendTickets_Insufficient_ReturnsFalseAndLeavesBalanceUnchanged
- SpendTickets_ExactBalance_SucceedsAndLeavesZero
- GachaTickets_SurvivesJsonRoundTrip
- GachaTickets_DefaultsToZeroOnFreshDeserialize
- CurrentSchemaVersion_Is7
- Migration_V6ToV7_SetsGachaTicketsTo10
- Migration_V6ToV7_PreservesExistingFields
- Migration_V5ToV7_ChainMigratesCorrectly
- Migration_AlreadyV7_DoesNotOverwriteExistingTickets

Runner: `mcp ai-game-developer tests-run` (EditMode), 2026-07-09. Status=Passed for each class filter.
