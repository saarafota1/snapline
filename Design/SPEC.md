# Snapline — the candy redesign

What the reference art in `Design/references/` establishes, read off the images rather than assumed.
Where the references contradict each other or contradict what is built, that is called out rather
than quietly resolved — those are decisions, not details.

| Reference | Screen |
|---|---|
| `home.png` | Front screen |
| `levels.png` | Level select |
| `level_game.png` | Level mode gameplay |
| `endless_game.png` | Endless gameplay |
| `daily_challenge.png` | Daily challenge |
| `toolbox.png` | Store |
| `best_scores.png` | Score table |
| `popup_level_complete.png` | Level result |
| `popup_great_run.png` | Endless run result |
| `popup_no_more_moves.png` | Continue offer |
| `popup_paused.png` | Pause + settings |

All references are 941 x 1672, the same 9:16 shape as the design canvas (1080 x 1920).

---

## Economy

One currency: **coins**. Shown top-right on nearly every screen as a blue pill — coin icon, the
balance, then a green `+` that opens the store.

**Earned**

| Source | Amount |
|---|---|
| Finishing a level | +50 |
| Finishing an endless run | +180 at 42 lines / combo x6 — scales with the run |
| Daily challenge | +100 and a tool |
| Watching a rewarded ad | +50 |

**Spent**

| On | Cost |
|---|---|
| Undo | 50 |
| Shuffle | 80 |
| Hammer | 120 |
| Continuing a dead run | 80 |

Prices and rewards are on the same order, which means the balance between them decides whether tools
feel reachable. Nothing in the references pins the endless coin formula down; `+180 coins` against
`42 lines` and `best combo x6` is the only data point.

## Tools

Three, each with an owned count. The toolbox icon carries a red badge.

| Tool | Reference text | Price |
|---|---|---|
| **Undo** | "Take back your last move" | 50 |
| **Shuffle** | "Replace all three pieces" | 80 |
| **Hammer** | "Remove one block" | 120 |

**Undo needs a move history the engine does not currently keep.** `GameRun` applies a move and keeps
no way back. This is the one tool that is not simply a new button.

**The badge does not match the counts.** The toolbox badge reads `7` while the owned counts are
3 + 2 + 1 = 6. Treated as the sum unless told otherwise; the mock is probably just inconsistent.

---

## Screens

### Front screen — `home.png`
Built. Coins, tools and daily are placeholders wired to one call site each.

### Level select — `levels.png`
Status bar, title, a `8 / 60 COMPLETE | ⭐ 24 / 180 STARS` pill, then a **five-wide grid**.

- **Completed** — coloured tile cycling through the block palette, three stars beneath.
- **Current** — pink tile, play triangle, the word PLAY, no number.
- **Locked** — deep navy tile, number, padlock.
- **Milestone** — level 10 is locked but carries a treasure chest. Implies a chest every 10 levels.

**Conflict: the reference paginates, the build scrolls.** Four page dots, 25 tiles per page. The
current `LevelSelect` is one long scrolling grid. Pagination is a rewrite of that screen's
navigation, not a restyle.

### Level gameplay — `level_game.png`
Pause (left), `LEVEL 9` over the score, coin pill (right), toolbox below it.

Then two objective pills — **LINES 5 / 8** with a fill bar, **MOVES 12** — and a **star progress bar**
showing which of the three stars are still live. Board, tray of three, tools row.

The star bar is new: the build awards stars at the end from moves remaining, and never shows the
player where they stand mid-level.

### Endless gameplay — `endless_game.png`
Score pill with `BEST` beneath, a **COMBO x4 · 3.2×** pill, board, tools, tray.

**Conflict: the tool row swaps sides between the two gameplay references.** In `endless_game.png`
the tools sit *above* the tray; in `level_game.png` they sit *below* it. One of them has to move.

### Daily challenge — `daily_challenge.png`
A **7 DAY STREAK** pill, then a week strip Mon–Sun:

- past days — green tick
- today — pink `TODAY`
- future — padlock, blue; Sunday's is gold and larger

Each day shows its reward: `25` coins, `1` undo, `50` coins, `1` shuffle, `75` coins, `1` hammer,
then a chest on Sunday. Rising through the week, so the streak is worth keeping.

Below: **TODAY'S PUZZLE** — a board preview, `CLEAR 8 LINES`, `18 MOVES`, rewards `+100` coins and
`+1` hammer, and PLAY TODAY. Then `3 / 7 COMPLETE` with a chest.

**So a daily is a level, not an endless run** — the same "clear N lines in M moves" objective the
level ladder already uses, seeded from the date so everyone gets the same board. That is a small
amount of new work on top of `Levels`, not a new mode.

### Store — `toolbox.png`
Title, an open toolbox, `YOUR POWER-UPS`, then one row per tool: icon, name, description, `YOU HAVE
n`, a green `+`, and a price. Below, `NEED MORE COINS?` with `WATCH +50`, and `RESTORE PURCHASES`.

**`RESTORE PURCHASES` means real-money IAP.** Nothing else in the references implies a paid product —
there is no coin pack, no price in currency. Restore only exists for purchases that were paid for.
Either it is decoration and should go, or there is an IAP tier still to be designed. Left out until
that is settled: shipping a dead Restore button is worse than not having one, and IAP changes the
Play listing and the Data safety form.

### Score table — `best_scores.png`
Three stat pills — `24 GAMES`, `386 LINES`, `BEST COMBO x6` — then the top ten with gold, silver and
bronze medals on the first three, dates on the right. SHARE BEST and PLAY ENDLESS.

All of this data already exists in `SaveSystem`. This screen is a pure restyle.

### Level result — `popup_level_complete.png`
Banner, level number, three stars, `8 LINES IN 16 MOVES`, a `NEW BEST!` badge, then the coin award
as `+50` with the balance moving `1,240 → 1,290`. NEXT LEVEL, REPLAY, LEVELS, and a close X.

### Endless result — `popup_great_run.png`
`GREAT RUN!`, the score, `NEW BEST!` with the previous best beneath, three stat tiles (lines, best
combo, coins earned), the balance moving, then PLAY AGAIN / HOME / SHARE, and the run's rank with a
way into the score table.

### Continue offer — `popup_no_more_moves.png`
Replaces the current revive card. `Your board is full. Keep this run going?` with three ways on:

1. **SHUFFLE BOARD** — `2 LEFT`, spending an owned shuffle
2. **WATCH TO CONTINUE** — a rewarded ad, described as a free shuffle
3. **USE 80 COINS**

and `END RUN` beneath.

**Two conflicts here.**

*What shuffle does.* The store says shuffle "Replace**s** all three pieces". This popup says SHUFFLE
**BOARD**. Those are different mechanics — one redeals the tray, the other rearranges the board — and
only one of them can rescue a board with no legal move. Redealing the tray can; rearranging a full
board cannot, because a full board has no room whatever is in it.

*It supersedes the shipped revive.* The build currently offers one rewarded revive per run that
clears the three fullest rows, deliberately — the rows are chosen by fullness so a mess at the top is
actually helped. The reference offers three routes and no limit. Unlimited paid continues turn a high
score into a measure of spending rather than skill, which is exactly what the one-revive rule was
written to prevent.

### Pause — `popup_paused.png`
`PAUSED`, the level and its progress, RESUME / RESTART / LEVELS / HOME, then **MUSIC**, **SOUND** and
**VIBRATION** toggles and a `HOW TO PLAY` link.

**Music and vibration do not exist.** The game synthesises sound effects and has no music track and
no haptics. Both are real features behind those switches, and a switch that toggles nothing is worse
than no switch.

---

## What this changes in the engine

Most of the redesign is presentation. These are not:

| Need | Why it is not just art |
|---|---|
| **Coin wallet** | New persisted state, and every reward and price above has to be balanced against real play |
| **Tool inventory** | New persisted state, three counts, spent and granted from several places |
| **Undo** | `GameRun` keeps no history. Needs one move of it, and it has to survive save and restore |
| **Hammer** | Removing one arbitrary block is not a move the engine has; scoring and combo have to ignore it |
| **Daily challenge** | Date tracking, a date-seeded objective, streak state, and a rule for what breaks a streak |
| **Level pagination** | Different navigation from the scrolling grid that exists |
| **Live star bar** | The engine computes stars at the end; this needs them continuously |
| **Music, vibration** | Neither exists |
| **IAP** | Only implied by `RESTORE PURCHASES`; changes the store listing and the Data safety form |

## Open decisions

1. Tool row above or below the tray — the two gameplay references disagree.
2. Does shuffle redeal the tray or rearrange the board? Only the first can rescue a dead board.
3. Is the continue offer limited per run? Unlimited paid continues make a high score a spending
   record.
4. Is there real-money IAP, or does `RESTORE PURCHASES` go?
5. Music and vibration: build them, or drop the switches?
6. The endless coin formula. One data point: 42 lines and a x6 combo paid 180.
7. What breaks a daily streak, and what is in the Sunday chest?
