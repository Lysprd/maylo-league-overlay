using System;
using System.Linq;
using System.Collections.Generic;

namespace MayhemCompanion {
 public class OfferSession {
  public const int MaxSlots = 5;
  public Stat[] Choices = new Stat[MaxSlots];
  public AugmentHit[] Positions = new AugmentHit[MaxSlots];
  public DateTime[] SlotLastSeen = new DateTime[MaxSlots];
  public bool Visible, Refreshing;
  public int Revision;
  public int ExpectedCardCount { get; private set; }
  DateTime missingSince = DateTime.MinValue;
  public const int HoldMilliseconds = 550;
  const int SlotHoldMilliseconds = 2400;
  const int ClickRerollHoldMilliseconds = 1500;
  const int ClickPickHoldMilliseconds = 90;
  AugmentHit clickedSlot;
  bool clickCanPick;
  DateTime clickUntil;
  int missingFrames;
  public Stat ConfirmedPick { get; private set; }
  public bool PendingPick { get { return clickCanPick; } }
  public int PendingRerollId { get { return clickedSlot != null && !clickCanPick ? clickedSlot.Stat.Id : 0; } }

  public bool TrackClick(double x, double y, DateTime now) {
   if (!Visible || y < 0.14 || y > 0.92) return false;
   var nearest = Positions.Where(p => p != null).OrderBy(p => Math.Abs(p.X - x)).FirstOrDefault();
   if (nearest == null) return false;
   double halfWidth = Positions.Where(p => p != null && p != nearest)
    .Select(p => Math.Abs(p.X - nearest.X) / 2).DefaultIfEmpty(0.12).Min();
   if (Math.Abs(nearest.X - x) >= Math.Min(halfWidth, 0.12)) return false;

   // A click below the card (~Y=0.72+) is the reroll button (keep 1500ms grace so rerolls never blink).
   // A click inside the card (art, title, or description) is a pick: close promptly (~90ms + 2 fast 45ms frames).
   bool cardInterior = y >= nearest.Y - 0.22 && y <= nearest.Y + 0.25;
   clickedSlot = nearest;
   clickCanPick = cardInterior && (now - SlotLastSeen[Array.IndexOf(Positions, nearest)]).TotalMilliseconds <= ClickRerollHoldMilliseconds;
   clickUntil = now.AddMilliseconds(clickCanPick ? ClickPickHoldMilliseconds : ClickRerollHoldMilliseconds);
   missingFrames = 0;
   return true;
  }

  public void Reset() {
   Choices = new Stat[MaxSlots];
   Positions = new AugmentHit[MaxSlots];
   SlotLastSeen = new DateTime[MaxSlots];
   Visible = false;
   ExpectedCardCount = 0;
   Refreshing = false;
   missingSince = DateTime.MinValue;
   clickUntil = DateTime.MinValue;
   clickedSlot = null;
   clickCanPick = false;
   missingFrames = 0;
   ConfirmedPick = null;
   Revision++;
  }

  public bool Observe(List<AugmentHit> hits, DateTime now, bool allowSingleOpen = false) {
   ConfirmedPick = null;
   var observed = (hits ?? new List<AugmentHit>()).Where(h => h != null && h.Stat != null)
    .OrderBy(h => h.X).Take(MaxSlots).ToList();
   int minRequired = (Visible || allowSingleOpen) ? 1 : 2;
   if (observed.Count < minRequired) {
    if (!Visible) return false;
    if (!Refreshing) {
     Refreshing = true;
     missingSince = now;
    }
    missingFrames++;
    int requiredHold = clickCanPick ? ClickPickHoldMilliseconds : HoldMilliseconds;
    if (now < clickUntil || (now - missingSince).TotalMilliseconds < requiredHold || missingFrames < 2) return false;
    var pick = clickCanPick && clickedSlot != null ? clickedSlot.Stat : null;
    Reset();
    ConfirmedPick = pick;
    return true;
   }

   // A new title at the clicked position confirms a reroll. Keep its existing card center.
   int rerolledId = PendingRerollId;
   var clicked = clickedSlot;
   var replacement = clicked == null ? null : observed.FirstOrDefault(h =>
    h.Stat.Id != clicked.Stat.Id && Math.Abs(h.X - clicked.X) < AugmentOcr.MinCardSpacing);
   if (replacement != null || now >= clickUntil) {
    clickedSlot = null;
    clickCanPick = false;
   }
   missingFrames = 0;

   var merged = new List<Tuple<Stat, AugmentHit, DateTime>>();
   foreach (var h in observed) {
    var damped = h;
    if (h == replacement) {
     damped = new AugmentHit { Stat = h.Stat, X = clicked.X, Y = clicked.Y };
    } else if (Visible) {
     for (int i = 0; i < MaxSlots; i++) {
      if (Choices[i] != null && Choices[i].Id == h.Stat.Id && Positions[i] != null && Math.Abs(Positions[i].X - h.X) < 0.045) {
       damped = new AugmentHit {
        Stat = h.Stat, X = Positions[i].X,
        Y = Math.Abs(Positions[i].Y - h.Y) < 0.025 ? Positions[i].Y : h.Y
       };
       break;
      }
     }
    }
    merged.Add(Tuple.Create(h.Stat, damped, now));
   }

   if (Visible) {
    bool sameRow = observed.Count(h => Positions.Any(old => old != null && old.Stat.Id == h.Stat.Id
     && Math.Abs(old.X - h.X) < 0.035 && Math.Abs(old.Y - h.Y) < 0.065)) >= 2;
    for (int i = 0; i < MaxSlots; i++) {
     var oldStat = Choices[i];
     var oldPos = Positions[i];
     if (oldStat == null || oldPos == null) continue;
     if (oldStat.Id == rerolledId && now >= clickUntil) continue;
     if (!sameRow && (now - SlotLastSeen[i]).TotalMilliseconds > SlotHoldMilliseconds) continue;
     if (merged.Any(m => m.Item1.Id == oldStat.Id)) continue;
     if (merged.Any(m => Math.Abs(m.Item2.X - oldPos.X) < AugmentOcr.MinCardSpacing)) continue;
     if (merged.Count == MaxSlots) break;
     merged.Add(Tuple.Create(oldStat, oldPos, SlotLastSeen[i]));
    }
   }

   var finalList = merged.OrderBy(m => m.Item2.X).Take(MaxSlots).ToList();
   var next = new Stat[MaxSlots];
   var nextPos = new AugmentHit[MaxSlots];
   var nextSeen = new DateTime[MaxSlots];
   for (int i = 0; i < finalList.Count; i++) {
    next[i] = finalList[i].Item1;
    nextPos[i] = finalList[i].Item2;
    nextSeen[i] = finalList[i].Item3;
   }

   if (finalList.Count < 1) return false;
   ExpectedCardCount = Math.Max(ExpectedCardCount, finalList.Count);
   bool idsChanged = !Visible || !Choices.Select(x => x == null ? 0 : x.Id).SequenceEqual(next.Select(x => x == null ? 0 : x.Id));
   bool posMoved = false;
   if (!idsChanged) {
    for (int i = 0; i < MaxSlots; i++) {
     if (Positions[i] == null || nextPos[i] == null) continue;
     if (Math.Abs(Positions[i].X - nextPos[i].X) > 0.018 ||
         Math.Abs(Positions[i].Y - nextPos[i].Y) > 0.018) { posMoved = true; break; }
    }
   }
   bool changed = idsChanged || posMoved;
   missingSince = DateTime.MinValue;
   Visible = true;
   Refreshing = false;
   Choices = next;
   Positions = nextPos;
   SlotLastSeen = nextSeen;
   if (changed) Revision++;
   return changed;
  }
 }
}
