# Manual QA Checklist — Negotiation Showcase Scenario

## Prerequisites

- Build and run the application.
- Load **"Negotiation Showcase"** from the test maps menu (or via `LoadNegotiationShowcaseTestMap`).
- An LLM provider/key/model must be configured in Settings (use a real LLM, not `RpFakeLlmClient`).

---

### 1. Scene Phase & Escalation Budget on Load

- [ ] On load, confirm the scene phase is **Setup**.
- [ ] Confirm `EscalationBudget` is **1** (visible via tick/debug output — check `RpSimulationService.AppendDebugLog` or the scene context exposed in the UI).
- [ ] Confirm `first_contact_not_yet_made` flag is present in `Continuity.Flags`.

---

### 2. Talking to Vexa Advances the Scene

- [ ] Select Scout Vexa (she should be at approximately `(3, 0, 1)` near the player).
- [ ] Initiate a conversation (talk action).
- [ ] After the LLM responds, verify the scene phase advances from **Setup → FirstContact** automatically.
- [ ] The `first_contact_not_yet_made` flag should be removed from `Continuity.Flags`.

---

### 3. RelationshipDelta / SceneUpdate Moves Values

- [ ] Continue the conversation for 2–3 exchanges.
- [ ] After each tick, inspect Vexa's **Trust**, **Fear**, and **Suspicion** relationship values toward `player`.
- [ ] Verify that at least one of these values has changed from its starting value (Trust 0, Fear 20, Suspicion 40) after a few exchanges — they should not stay frozen.
- [ ] If the player says something cooperative, Trust should increase. If aggressive, Fear/Suspicion should increase.

---

### 4. Escalation Budget Caps Aggressive Beats

- [ ] Send an aggressive/threatening message to Vexa.
- [ ] Note the `EscalationDelta` value in the LLM response (visible in debug log).
- [ ] Send a second aggressive message immediately after (before the budget has time to regenerate).
- [ ] Verify that the second escalation is **capped/softer** than the first — the budget at 1 with a slow regen rate (`0.05f` per tick) means it cannot recover between rapid exchanges.
- [ ] Wait a few ticks and send a third aggressive message; this one should land harder because the budget has partially regenerated.

---

### 5. Conversation Summary Persists Into Memory

- [ ] End the conversation with Vexa (End Conversation action or auto-end).
- [ ] Wait 1–2 ticks for memory compaction to occur.
- [ ] Open Vexa's memory/transcript (if inspectable via debug tools or `RpSimulationService`).
- [ ] Verify that a **MemorySummary** or **PerceivedLog digest** exists that references the conversation's outcome.
- [ ] Start a new conversation with Vexa.
- [ ] Verify her behavior reflects the prior conversation (e.g., if you were friendly before, she should be less guarded; if hostile, more fearful). The LLM should see the memory summary in her snapshot.
