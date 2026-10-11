# Plan: Per-player tee boxes

Branch: `worktree-course-tee-boxes`

## Goal

- A course can have several tee boxes, each with its own rating and slope.
- An admin assigns each player a default tee box per half, per course.
- When scores are entered, each player's tee box starts at their default and can be changed for that player only.
- The rating and slope used for a player's round (course handicap, strokes, differential) come from the tee box chosen for that round.
- Every round in the database, past and future, references a tee box. Past rounds keep exactly the numbers that produce their current differentials.

## Decisions (confirmed)

| Question | Decision |
|---|---|
| Halves aren't tied to one course | Defaults are stored per half and per course (`PlayerHalfTeeBox`) |
| Tee changed after holes have been scored | Allowed until the round is finalized. Course handicap is recomputed and the scored holes are rescored |
| Player has no tee assigned | Use the course's default tee box (`TeeBox.IsDefault`). Every course always has exactly one |
| Past rounds | Backfilled to a tee box built from the course's current rating and slope. `RoundParticipant.TeeBoxId` is required |
| Par used in the course handicap formula | Keep the course's hole par (`Course.Holes.Sum(Par)`) everywhere. Hole par and stroke index still come from `CourseHole`. A tee box only changes rating and slope |

## Why a tee box reference *and* a snapshot

Differentials are not stored today. `HandicapRepository.GetLastNRoundInputsAsync` (`src/GolfLeague.Infrastructure/Repositories/HandicapRepository.cs:85`) reads `rp.Round.Course.CourseRating/SlopeRating` live on every recalculation, so editing a course rating silently changes every past differential.

- `RoundParticipant.TeeBoxId` records *which* tee the player played. It is required, and backfilled for every existing row.
- `RoundParticipant.CourseRating` / `SlopeRating` record *the numbers* the round was played at. Every reader uses these. Without them, editing a tee box's rating would rewrite history exactly the way editing a course does today.

The alternative is to make tee boxes immutable once a finalized round uses them, so a rating change means creating a new tee. That was rejected: it adds admin friction, and the two snapshot columns are cheap.

## 1. Domain and data model

**`TeeBox`**
- Add `bool IsDefault`.
- Add a filtered unique index on `(CourseId) WHERE IsDefault = 1`, so each course has exactly one default.

**`RoundParticipant`** gets new columns:
- `int TeeBoxId` (required) plus a `TeeBox` navigation. FK is NoAction/Restrict, never cascade. A tee box that a round used cannot be deleted.
- `double CourseRating` and `int SlopeRating` (required). This is the snapshot.
- `bool TeeBoxOverridden`. It is set to true when the tee is changed on a score-entry page, so that later changes to the player's default don't overwrite that choice.

**New entity `PlayerHalfTeeBox`** (`Id, PlayerId, HalfId, CourseId, TeeBoxId`):
- Unique on `(PlayerId, HalfId, CourseId)`.
- It is a separate table rather than a column on `PlayerHalfSetting` because the default is per course. It is not on `FlightMembership` because of row churn.
- FKs: `Player` Cascade, mirroring `PlayerHalfSetting`. `SeasonHalf`, `Course` and `TeeBox` are Restrict/NoAction, to avoid SQL Server's multiple-cascade-path error. Code clears these rows before a tee box or half is deleted.
- Multi-tenancy scoping matches `PlayerHalfSetting`: through Player and Half, with no `LeagueId` column. Confirm during implementation.

**`Course.CourseRating` / `SlopeRating`**: kept for now but no longer read by scoring. They are mirrored from the default tee (see `UpdateTeeBoxCommand`) so existing course list and statistics screens keep working. They can be dropped in a follow-up.

### Migration `AddParticipantTeeBoxes`

1. Add `TeeBox.IsDefault` (default 0).
2. **Create a default tee box for every course.**
   - If a course already has a tee box whose `CourseRating` and `SlopeRating` exactly equal the course's values, mark that one `IsDefault = 1` (lowest `Id` if there are several).
   - Otherwise insert a tee box named `Default` with the course's rating and slope, `Par = SUM(CourseHoles.Par)` (0 if the course has no holes), `TotalYardage = 0`, and `IsDefault = 1`. No `HoleTeeBox` rows are created.
3. Add `RoundParticipants.TeeBoxId`, `CourseRating` and `SlopeRating` as nullable, plus `TeeBoxOverridden` (default 0).
4. Backfill every round row:
   ```sql
   UPDATE rp
   SET rp.TeeBoxId     = t.Id,
       rp.CourseRating = c.CourseRating,
       rp.SlopeRating  = c.SlopeRating
   FROM RoundParticipants rp
   JOIN Rounds r   ON r.Id = rp.RoundId
   JOIN Courses c  ON c.Id = r.CourseId
   JOIN TeeBoxes t ON t.CourseId = c.Id AND t.IsDefault = 1;
   ```
   These are exactly the values the live differential calculation uses today, so no existing differential or handicap changes.
5. Alter `TeeBoxId`, `CourseRating` and `SlopeRating` to NOT NULL. Add the FK and the index on `TeeBoxId`.
6. Create the filtered unique index on `TeeBoxes(CourseId) WHERE IsDefault = 1`.
7. Create `PlayerHalfTeeBoxes`.

Steps 2 and 4 are raw SQL inside the migration. Check it on LocalDB (`dotnet ef database update`) and confirm it isn't silently failing (see the self-referencing-FK memory). Before migrating, assert that there are no rows with `TeeBoxId IS NULL`, so step 5 can't fail. Before and after migrating, run `RecalculateAllHandicaps` on a copy of the data and diff the `Handicaps` rows. They must be identical.

## 2. Application layer

### New `ParticipantTeeService` (Application/Rounds)

This service is the single place that turns a tee choice into numbers.

- `ResolveDefaultAsync(round, playerIds)` returns a `TeeBox` per player. It never returns null.
  1. If the `tee_box_selection_enabled` flag is on, use `PlayerHalfTeeBox` for `(player, half, round.CourseId)`. The half is `round.HalfId`, or for tournaments the `SeasonHalf` whose dates contain `round.RoundDate`.
  2. Otherwise use the course's `IsDefault` tee.
- `Apply(participant, teeBox, course, roundType)` sets:
  - `TeeBoxId`
  - `CourseRating` and `SlopeRating` (from the tee box)
  - `CourseHandicap = StablefordScoringService.CourseHandicap(participant.HandicapIndex, slope, rating, coursePar, roundType)`

### New `ParticipantRescoringService`

This extracts the per-hole rescoring loop from `RecalculateAllRoundsCommand.cs:244-281`: strokes on hole, max gross, net, Stableford points, and totals. `RecalculateAllRoundsCommand` and the tee-change commands both use it. It has to keep the tournament vs. nine-side stroke-index handling that `SubmitHoleScoresCommand` uses.

### Participant creation sites

Each of these currently passes `course.SlopeRating/CourseRating`. Change them to resolve the default tee and call `Apply`:
- `CreateRoundCommand.cs:121`
- `GenerateHalfScheduleCommand.cs:128`
- `CreateTournamentRoundCommand.cs:159`
- `AddTournamentParticipantsCommand.cs:71`
- `TeeTimeService.CreateSubstituteParticipantAsync` (`TeeTimeService.cs:384`)

### Course and tee box commands

- **`CreateCourseCommand`**: also creates the course's `Default` tee box (`IsDefault = true`) from the rating and slope entered, in the same save.
- **`AddTeeBoxCommand`**: add an optional `IsDefault`. Setting it moves the default flag off the previous default, in one transaction.
- **New `UpdateTeeBoxCommand`**
  - Edits name, rating, slope, yardage, par and `IsDefault`.
  - When the rating or slope changes, it re-applies them to non-finalized participants that use that tee, and rescores them.
  - If the tee is the default, it mirrors the rating and slope onto `Course.CourseRating/SlopeRating`.
- **New `DeleteTeeBoxCommand`**
  - The default tee can't be deleted; make another tee the default first.
  - A tee referenced by any `RoundParticipant` can't be deleted.
  - Otherwise it removes the `PlayerHalfTeeBox` rows first, then the tee box.
- **`UpdateCourseHolesCommand` / course rating edits**: editing the course-level rating should edit the default tee instead (or the field is removed from the course form, in favor of the tee editor). Decide during implementation; the tee editor is preferred.

### Assignment commands

- **`SetPlayerHalfTeeBoxCommand(playerId, halfId, courseId, teeBoxId?)`** (admin only)
  - Upserts the default, or deletes it when `teeBoxId` is null (the player then falls back to the course default).
  - Re-applies the resolved tee to that player's participants in that half, on that course, where all of these hold: the round is not Finalized or Cancelled, the player is not overridden, and the participant has no hole scores.
- **`SetParticipantTeeBoxCommand(roundId, playerId, teeBoxId?, userId)`**
  - Checks that the round is not Finalized or Cancelled and that the tee belongs to `round.CourseId`.
  - `teeBoxId == null` means revert to the resolved default and clears `TeeBoxOverridden`. Otherwise it sets the override.
  - It runs `Apply`, then `ParticipantRescoringService` if hole scores exist, then `MatchPlayResultCalculator.RecomputeForRoundPlayerAsync` when the half is match play (same as `SubmitHoleScoresCommand.cs:150`).
  - It is auditable (`IAmAuditableCommand`).

### Readers switched from `Round.Course.*` to the participant snapshot

| File | Change |
|---|---|
| `Infrastructure/Repositories/HandicapRepository.cs:85` | Use `rp.CourseRating` and `rp.SlopeRating` |
| `Application/Players/Queries/GetPlayerRoundsQuery.cs:73` | Same, plus the tee name for display |
| `Application/Flights/Services/MatchPlayResultCalculator.cs:220,235` | Same |
| `Application/Rounds/Commands/SubmitHoleScoresCommand.cs:214` | Use the participant's values in the `ScorecardDto` |
| `Application/Rounds/Queries/GetPlayerScorecardQuery.cs:85` | Same |
| `Application/Admin/RecalculateAllRoundsCommand.cs:143,217` | Non-finalized rounds: refresh the snapshot from the participant's current tee box, then use the snapshot. Finalized rounds are never touched |

`GetCourseStatisticsQuery` reads the course's default tee rather than course-level fields. It describes the course, not a round.

### DTOs

- `ParticipantDto` and `TeeTimePlayerScoreDto` get `TeeBoxId`, `TeeBoxName`, `CourseRating` and `SlopeRating`.
- `TeeBoxDto` gets `IsDefault`.
- `TeeTimeGroupScorecardDto` and the admin round/scorecard payloads get the course's `TeeBoxes` (id, name, rating, slope, isDefault) to fill the dropdown.
- New `PlayerHalfTeeBoxDto(HalfId, CourseId, TeeBoxId)` list on the player detail payload.

## 3. Feature flag

Add `tee_box_selection_enabled` to `FeatureFlagsCommands.cs` (near line 210), default `false`, and add a toggle on the admin Settings page, following the existing flag pattern.

When the flag is off:
- No player-default or score-entry tee UI is shown.
- The resolver ignores `PlayerHalfTeeBox`, so every player gets the course default tee. This matches today's behavior.
- The `Set*TeeBox` endpoints return 400.

These are not behind the flag, because they are data integrity rather than a feature:
- the default tee per course
- the required `TeeBoxId`
- the snapshot columns
- tee box edit and delete in the course admin

## 4. Functions (HTTP)

| Route | Purpose |
|---|---|
| `PUT v1/courses/{id}/teeboxes/{teeBoxId}` / `DELETE` | Edit or delete a tee (admin). The existing `POST` gains `isDefault` |
| `GET/PUT v1/players/{id}/halves/{halfId}/tee-boxes` | Body `{ courseId, teeBoxId }` (admin) |
| `PUT v1/rounds/{id}/participants/{playerId}/tee-box` | Body `{ teeBoxId }` (admin score entry) |
| `PUT v1/tee-times/{id}/participants/{playerId}/tee-box` | Group score entry. Same authorization as the existing `.../skip` endpoint in `TeeTimeFunctions.cs`: the caller must be in the group |

## 5. Web (`web/src`)

- **`components/admin/TeeBoxesEditor.tsx`**: add edit and delete for tees (name, rating, slope, yardage, par), plus "Make default". Deleting the default tee is disabled.
- **`pages/admin/CoursesPage.tsx`**: the course create form's rating and slope seed the default tee. Course-level editing of rating and slope goes through the default tee.
- **`pages/admin/PlayerDetailPage.tsx`** (`HalfMembershipRow`, next to the par-3 skins opt-in): add a "Default tees" select per course, with a "Course default (\<name\>)" option.
- **Score entry**: show a per-player tee `<select>` with the rating/slope label, the resulting course handicap, and an "overridden" hint.
  - Pages: `pages/admin/ScoreEntryPage.tsx`, `pages/admin/TournamentScoreEntryPage.tsx`, `pages/TeeTimeScoreEntryPage.tsx` (+ `hooks/useTeeTimeScoreEntry.ts`).
  - On change, call the mutation and invalidate the scorecard query so the stroke dots and net totals refresh.
- Player round history shows the tee name.
- Update `types/api.ts` and add mutation hooks.

## 6. Mobile (`mobile/lib`)

Scoring is in mobile scope; assigning defaults and editing tees stay web-only.
- `screens/score_entry_screen.dart` and `screens/admin/admin_score_entry_screen.dart` get a per-player tee dropdown.
- `api/api_service.dart` gets `setParticipantTeeBox` (tee-time and round variants).
- Show the selected tee's name and course handicap on the scorecard header.

## 7. Tests (`tests/GolfLeague.Functions.Tests`)

- **Migration backfill**, run on a seeded LocalDB:
  - every course has exactly one default tee
  - an existing matching tee is reused rather than duplicated
  - every participant has a `TeeBoxId`
  - every snapshot equals the course's pre-migration values
  - handicaps are unchanged
- **`ParticipantTeeService`**:
  - uses the player's half/course default, otherwise the course default
  - uses the course default when the flag is off
  - finds a tournament's half by date
- **`SetParticipantTeeBoxCommand`**:
  - course handicap is recomputed and scored holes are rescored
  - a finalized round is rejected
  - a tee from another course is rejected
  - null reverts to the default
- **`SetPlayerHalfTeeBoxCommand`**: propagates only to participants that are unscored, not overridden and not finalized.
- **Tee box commands**:
  - only one default per course
  - deleting the default or a used tee is refused
  - editing a tee's rating changes non-finalized rounds only
- **Regression**: editing a tee or the course rating no longer changes historical differentials.
- **`RecalculateAllRounds`**: leaves finalized snapshots alone.

## 8. Rollout order

1. Migration (default tees and participant backfill), entities, and snapshot readers. No behavior change; verify handicaps are identical.
2. Resolver plus creation sites. Default tee on course create. Tee box edit, delete and make-default.
3. Default assignment (backend and admin UI).
4. Score-entry override (backend, web, mobile) and the feature flag toggle.
5. Update `docs/CODEBASE_MAP.md`.
6. Follow-up: drop `Course.CourseRating` / `SlopeRating` once nothing reads them.

## Known edge cases

- Rounds in a half that were already generated pick up a new default only while they are unscored and not overridden. Rounds with scores keep their tee unless it is changed on score entry.
- Reopening a finalized round keeps its snapshot. Changing the tee after reopening is allowed (the round is no longer finalized) and rescores.
- Backfilled `Default` tee boxes have `TotalYardage = 0` and no per-hole yardages. Admins can rename them or fill them in later; scoring doesn't depend on them.
- History drift that already happened is not recoverable. If an admin edited `Course.CourseRating` mid-season before this change, historical differentials already shifted. The backfill freezes today's effective values, which is the stated goal.
