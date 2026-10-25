// ---- System notes ----
// PixelTipQueue.cs
//   ONE queue for everything that pops up to teach or tell the player (UI unification step 6): the guided tutorials (shop, skills, Toggles),
//   the perk popups (PixelSkills), the first-time tips (PixelHints) and the "Did you know?" box (PixelDidYouKnow). Each source registers once
//   (Register(owner, priority, name, ready, show)) and keeps its OWN queue and conditions ('ready': e.g. the first-time minigame tips may show
//   during a minigame, the tutorials want PixelCalm.ForTip). Every frame the queue picks the highest priority source that is ready - only
//   while nothing else is up (Busy: a PixelNotice box or a tutorial dim) and after a short gap (gapSeconds) - and calls its 'show'. So two
//   sources ready in the same frame take turns instead of one box replacing the other. Adding a new kind of tip = one Register call.
//   Priority: Tutorial > PerkPopup > FirstTimeTip > DidYouKnow.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelTipQueue : MonoBehaviour
{
    public enum Priority { Tutorial = 0, PerkPopup = 1, FirstTimeTip = 2, DidYouKnow = 3 }

    private sealed class Source
    {
        public object owner;
        public Priority priority;
        public string name;
        public System.Func<bool> ready;
        public System.Action show;
    }

    [Min(0f)]
    [Tooltip("Seconds between one tip / popup closing and the next one showing.")]
    [SerializeField] private float gapSeconds = 0.3f;

    private static readonly List<Source> sources = new List<Source>();
    private float gap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { sources.Clear(); }

    /// <summary>Something is up already: a tip / notice box, or a tutorial's dim. Nothing else shows until it is gone.</summary>
    public static bool Busy => PixelNotice.IsShowing || PixelTutorialDim.Active;

    /// <summary>
    /// Adds a source of tips. 'ready' = it has something to show and its own conditions are met right now; 'show' = show ONE thing
    /// (it must put up a PixelNotice box or start a tutorial, or the queue may call it again next frame). Registering the same owner + name again replaces it.
    /// </summary>
    public static void Register(object owner, Priority priority, string name, System.Func<bool> ready, System.Action show)
    {
        sources.RemoveAll(s => s.owner == owner && s.name == name);
        sources.Add(new Source { owner = owner, priority = priority, name = name, ready = ready, show = show });
        sources.Sort((a, b) => a.priority.CompareTo(b.priority));
    }

    /// <summary>Removes every source of this owner (call from OnDestroy).</summary>
    public static void Unregister(object owner) { sources.RemoveAll(s => s.owner == owner); }

    private void Update()
    {
        if (Busy) { gap = gapSeconds; return; }
        if (gap > 0f) { gap -= Time.unscaledDeltaTime; return; }
        for (int i = 0; i < sources.Count; i++)
        {
            Source s = sources[i];
            if (s.owner is Object o && o == null) { sources.RemoveAt(i); i--; continue; }   // (its owner was destroyed)
            if (s.ready == null || !s.ready()) continue;
            s.show?.Invoke();
            gap = gapSeconds;
            return;   // one at a time
        }
    }
}
