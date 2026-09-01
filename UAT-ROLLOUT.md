# Sprint 12 UAT and rollout runbook

This runbook is the release gate for the first real OFC branch. Automated tests establish software readiness; they do not replace cashier, manager, printer, network, or payment acceptance in the branch.

## Current status

- Software validation: ready when the commands below pass for the exact release commit.
- Real-branch UAT: **pending** until every Must-Have case is signed by the pilot branch manager.
- Production rollout: **blocked** until UAT, backup restore, and rollback rehearsal evidence is attached.
- Pilot topology: exactly one API replica. QR add/confirm serialization is process-local; do not scale the API horizontally until it uses a PostgreSQL lock and durable print outbox.
- Deferred scope: the full customer mobile self-ordering UI is Good-to-Have. Sprint 11 provides the secured QR/session/order API and landing page.

## Roles and evidence

Assign one person to each role before starting:

| Role | Responsibility |
|---|---|
| Release owner | Records commit, image digest, migration, timestamps, and deploy result |
| Branch manager | Owns UAT data and final go/no-go decision |
| Cashier | Runs order, payment, cash shift, and correction scenarios |
| Kitchen operator | Confirms ticket routing, content, and duplicate behavior |
| Inventory owner | Confirms recipes, consumption, reversals, counts, and variances |

Store screenshots, receipt/ticket photos, database backup identifier, logs, and signed results in one release folder named `YYYY-MM-DD_<commit>` outside this repository. Do not put customer data, tokens, passwords, or connection strings in evidence.

## Entry criteria

1. Freeze the release commit and record `git rev-parse HEAD` plus both deployed image digests.
2. Run from `backend`: `dotnet test "POS.slnx" --configuration Release --no-restore`.
3. Run from `frontend`: `npm run lint`, `npm test`, and `npm run build`.
4. Confirm no test failures, no lint errors, and review warnings rather than silently accepting new ones.
5. Create a PostgreSQL backup and restore it into a disposable database. Record both successful backup and restore identifiers.
6. Confirm the pilot branch, tables, default warehouse, opening float, users, permissions, payment methods, sales channels, printer IPs, and recipes are configured.
7. Confirm `CAR_PICKUP` is enabled only where intended and QR channels have the intended prepayment policy.
8. Confirm the pilot has one API replica, stable HTTPS, local printer reachability, and a wired fallback procedure for internet loss.

## Deployment sequence

1. Announce a write freeze and wait for active orders, payments, stock counts, and cash shifts to finish.
2. Capture the verified pre-deploy database backup and current application image digests.
3. Deploy the frozen commit using `docker-compose.yml`; keep `RUN_MIGRATIONS_ON_STARTUP=true` for this release.
4. Verify API startup logs show successful migrations and no repeated restart loop. Sprint 11 migration: `20260901112758_HardenQrOrderingSprint11`.
5. Verify HTTPS, login, `/api` proxying, persistent Data Protection keys, uploads, and SMTP configuration.
6. Change the bootstrap `admin` password immediately if this is a new database.
7. Run smoke cases UAT-01, UAT-06, UAT-09, UAT-12, and UAT-15 before reopening writes.
8. Start the pilot only after the release owner and branch manager sign the smoke result.

## Pilot UAT matrix

Use real configured devices but test products/customer references. Record Pass/Fail, tester, timestamp, and evidence for every row.

| ID | Scenario | Expected result |
|---|---|---|
| UAT-01 | Cashier and branch manager sign in; attempt another branch's order/report | Role permissions apply and cross-branch data is unavailable |
| UAT-02 | Create Dine-in and Takeaway orders with modifiers and a combo | Snapshot names/prices and totals are correct; combo reports as one sold item |
| UAT-03 | Disable a category for the pilot branch and order its item by UI/API | Item is unavailable, including through QR |
| UAT-04 | Disable then enable `CAR_PICKUP`; create a car order | Disabled state rejects it; enabled order records Car Pickup and location |
| UAT-05 | Configure recipes; confirm an order; cancel an item/order | Stock consumes once and reversal records the correct reason/reference |
| UAT-06 | Route food and drink to separate printers and confirm one order | One ticket per section plus one receipt; location and order number are correct |
| UAT-07 | Make one printer unavailable during a controlled test | Failure is visible to staff; no stock/order ambiguity is left unresolved |
| UAT-08 | Pay cash, card, and split; attempt debt without/with approval | Allocations total exactly; debt policy and audit attribution are enforced |
| UAT-09 | Open and close a cash shift with denomination counts | Expected cash includes attributed cash only; count and variance persist |
| UAT-10 | Correct a finalized invoice as authorized manager | Revision and immutable before/after audit are visible in reports |
| UAT-11 | Finalize a stock count with a known variance | Stock immediately matches count and adjustment reason is auditable |
| UAT-12 | Compare daily/global reports to finalized restaurant orders | Paid/closed orders, split payments, channels, products, branches, and order types reconcile; open/legacy sales do not leak in |
| UAT-13 | Enable `QR_TABLE` for pilot and disable it for another branch | Pilot accepts it; disabled branch rejects it; caller cannot choose another channel |
| UAT-14 | Scan one table QR from two phones and submit simultaneously | One session and one grouped invoice contain both additions |
| UAT-15 | Require QR prepayment, try confirm unpaid, then fully pay and confirm twice | Unpaid confirm fails; paid order sends once and does not duplicate prints |
| UAT-16 | Scan Car Pickup QR with feature enabled | Order type/channel/location derive from the active bay without manual input |
| UAT-17 | Disable a point/bay/branch after scanning and attempt add/confirm | Capability is rejected after operational state is disabled |
| UAT-18 | Settle and close a QR session, then reuse its old token | Session closes, token rotates, and old token is invalid |
| UAT-19 | Transfer an eligible order as branch manager | Destination session validation and transfer audit are correct |
| UAT-20 | Restart API/web containers during a quiet window | Login continuity, keys, uploads, schema, and data survive restart |

Any security, duplicate-charge, duplicate-print, stock-corruption, branch-isolation, or unrecoverable migration failure is an immediate no-go.

## Rollback

Rollback triggers include a P0/P1 UAT failure, migration/startup loop, incorrect stock/payment totals, branch data exposure, or sustained inability to print/operate.

1. Stop new writes and QR ordering; record the incident timestamp and last accepted order number.
2. Preserve application and PostgreSQL logs before restarting anything.
3. If data written after deployment is disposable test data, stop the stack, restore the verified pre-deploy database backup, and redeploy the previous image digests.
4. If live transactions must be preserved, do not run an automatic down migration. Keep the database forward, disable QR, and have engineering assess a compensating migration before rolling application code back.
5. Re-run smoke tests against the restored version and reconcile orders, payments, cash, and stock through the incident boundary.

The Sprint 11 migration makes inventory actor attribution nullable and strengthens session/order uniqueness. Restoring the pre-deploy backup is the safe database rollback; a blind down migration can fail after anonymous QR inventory transactions exist.

## Expansion gate

Keep the release at one pilot branch until all of the following are true:

- Every UAT row passed and is signed by the branch manager and release owner.
- At least three representative operating days completed with no unresolved P0/P1 incident.
- End-of-day sales, payments, cash, stock, and reports reconciled each day.
- Printer/network failure procedures were exercised successfully.
- Backup restore and application rollback were rehearsed.
- Support owner, escalation contacts, maintenance window, and training material are assigned for each next branch.

Expand in small batches. Re-run branch isolation, channel/prepayment, Car Pickup, printer routing, cash shift, report reconciliation, and QR cases for every branch-specific configuration.

## Sign-off

| Decision | Name | Date/time | Evidence folder | Signature |
|---|---|---|---|---|
| Release owner |  |  |  |  |
| Pilot branch manager |  |  |  |  |
| Inventory owner |  |  |  |  |
| Go / No-go |  |  |  |  |
