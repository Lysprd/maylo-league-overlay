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
  DateTime missingSince = DateTime.MinValue;
  public const int HoldMilliseconds = 750;
  const int SlotHoldMilliseconds = 2400;

  public void Reset() {
   Choices = new Stat[MaxSlots];
   Positions = new AugmentHit[MaxSlots];
   SlotLastSeen = new DateTime[MaxSlots];
   Visible = false;
   Refreshing = false;
   missingSince = DateTime.MinValue;
   Revision++;
  }

  public bool Observe(List<AugmentHit> hits, DateTime now, bool allowSingleOpen = false) {
   var observed = (hits ?? new List<AugmentHit>()).Where(h => h != null && h.Stat != null)
    .OrderBy(h => h.X).Take(MaxSlots).ToList();
   int minRequired = (Visible || allowSingleOpen) ? 1 : 2;
   if (observed.Count < minRequired) {
    if (!Visible) return false;
    if (!Refreshing) {
     Refreshing = true;
     missingSince = now;
    }
    if ((now - missingSince).TotalMilliseconds < HoldMilliseconds) return false;
    Reset();
    return true;
   }

   var merged = new List<Tuple<Stat, AugmentHit, DateTime>>();
   foreach (var h in observed) {
    var damped = h;
    if (Visible) {
     for (int i = 0; i < MaxSlots; i++) {
      if (Choices[i] != null && Choices[i].Id == h.Stat.Id && Positions[i] != null && Math.Abs(Positions[i].X - h.X) < 0.025) {
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
