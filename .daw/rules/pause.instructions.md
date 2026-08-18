---
applyTo: '**'
version: 1.0.0
---

# DAW — Protocolo de pausa

> Se carga SOLO cuando el usuario pide pausar o reanudar un ticket. No forma parte de la carga
> de ninguna fase.

## Pausar y reanudar un ticket

When the user wants to pause the current ticket:
1. Save the current `.daw-state.json` as `.daw-paused/[ticket].daw-state.json`.
2. Reset `.daw-state.json` to IDLE with
   `.daw/scripts/transition.py --to IDLE --action "pause: <ticket> — <reason>"`. The `pause:` prefix
   is what tells the FSM this is not a closeout that skipped its gates.
3. Report: "Ticket [ticket] paused. You can resume it any time."

When the user wants to resume a paused ticket:
1. List the paused tickets in `.daw-paused/`.
2. The user picks which one to resume.
3. Restore the saved metadata — `tier`, `ticket`, `title`, `tracker`, `block`, `gates` — into the
   CURRENT `.daw-state.json`, and append a `IDLE → <phase>` entry with
   `action: "resume: <ticket>"`. **Never overwrite the file with the saved copy:** its `history` is
   shorter than the one on disk, and history is append-only — restoring it wholesale reads as a
   truncation and gets refused.
4. Run the normal "work in progress" flow (propose, do not auto-resume).
