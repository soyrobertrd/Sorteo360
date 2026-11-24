# notification-service: the send history

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## The read side

- **The history is read-only, and that is a fact and not a scope cut.** No `data` is stored, so a
  placeholder-bearing batch cannot be rebuilt: a resend would push out a different email from the
  one the screen shows. Never add a POST, PUT, PATCH or DELETE under the history routes —
  `HistoryIsReadOnlyTests` walks the registered route table and will fail if you do.
- **Who sees what is `Core/History/HistoryScope`**, and it names no composite role (`docs/adr/0010`).
  The only thing it reads is whether the caller is a service account, which Keycloak says in
  `preferred_username` (`service-account-<clientId>`); a person with `notifications:read` sees every
  `source_service`, a service account sees only its own. OPT scoping is unreachable here, not
  declined: this service knows no `opt_code` and has no outbound client to ask.
- **The `tracking_expected` cap lives in `Core/History/DeliveryTracking`** and is applied **on read**,
  never written to the column: the column records what happened, the API reports what the send could
  know. The reader applies the same cap in SQL so a `deliveryStatus` filter matches what the rows
  actually report.
