# Changelog

## 2026-09-01

- Reconciled dashboard and sales reports to paid/closed restaurant orders, order payments, menu items, sales channels, and order types.
- Hardened anonymous QR capabilities, branch isolation, channel derivation, Car Pickup gating, grouped session concurrency, prepayment confirmation, and token redaction/rate limiting.
- Added the Sprint 12 pilot UAT, deployment, rollback, evidence, and branch expansion runbook.

## 2026-08-31

- Added branch-scoped ESC/POS TCP printer configuration and test printing.
- Added dynamic printer sections and menu-item routing.
- Added order confirmation that prints one ticket per section and one customer receipt.
- Added split order payments, approval-gated debt payments, and audited closed-order edits.
- Added cash-shift opening, denomination counting, and expected/count/variance reconciliation.
- Added coded sales channels with independent enablement and prepayment rules per branch.
- Added cash-shift variance and invoice-edit audit data to the manager dashboard.
- Added secure QR ordering points, single open sessions, grouped session orders, prepayment enforcement, and token rotation.
- Added manager-only order transfers with destination-session validation, a bilingual dashboard screen, and immutable audit entries.
- Added the missing bilingual stock-count workflow to the dashboard, including draft saving, variance review, and final stock adjustment.
- Expanded branch-manager defaults for restaurant operations and corrected printer navigation to use `printing.manage`.
