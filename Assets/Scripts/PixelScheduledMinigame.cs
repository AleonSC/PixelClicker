// ---- System notes ----
// PixelScheduledMinigame.cs
//   The ROUND SCHEDULE shared by the minigames that come back on a timer (2026-10-24: was a copied block in each): Black Hole, Bomb, Ghost,
//   Meteor, Ultra Holes (Pad), Evil Tree, Cube Blink, Cube Dash, Cube Evasion, Traffic, Snake, Sorting Race and Breakout derive from it.
//   - The Inspector fields: startRunning (testing), running (the shop switches it on), firstRoundDelay (seconds from the start to the first
//     round), minInterval..maxInterval (the random wait between rounds). Each minigame sets its OWN defaults in its constructor (that is what
//     its old field initializers did); values already set in a scene carry over (FormerlySerializedAs, incl. the old first-delay names).
//   - Running / Activate / Deactivate come from here. Activate starts the clock at firstRoundDelay; a minigame that must also stop a round
//     when it is switched off overrides Deactivate (base.Deactivate() first).
//   - The minigame's Awake calls StartSchedule() where it used to set 'running' and the first timer; ScheduleNext() puts the next round
//     minInterval..maxInterval away (after a round, after the dev tools' Despawn). spawnTimer counts down in the minigame's Update with
//     PixelMinigame.SpawnDue.
//   Visitors (their own visit times + a per-visitor offset) and Weak Spots (runs while a skill is trained) schedule themselves differently and
//   are not part of this.
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.Serialization;

public abstract class PixelScheduledMinigame : PixelMinigame
{
    [Header("Round schedule")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] protected bool startRunning = false;
    [Tooltip("Is the minigame running? (The shop turns this on when its pack is bought.)")]
    [SerializeField] protected bool running = false;
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before its first round.")]
    [FormerlySerializedAs("firstHoleDelay"), FormerlySerializedAs("firstBombDelay"), FormerlySerializedAs("firstGhostDelay"),
     FormerlySerializedAs("firstMeteorDelay"), FormerlySerializedAs("firstPadDelay"), FormerlySerializedAs("firstDelay")]
    [SerializeField] protected float firstRoundDelay = 60f;
    [Min(1f)]
    [Tooltip("Shortest wait between rounds (seconds).")]
    [SerializeField] protected float minInterval = 120f;
    [Min(1f)]
    [Tooltip("Longest wait between rounds (seconds). Each wait is random between shortest and longest.")]
    [SerializeField] protected float maxInterval = 240f;

    /// <summary>Seconds until the next round may start (counted down in the minigame's Update with SpawnDue).</summary>
    protected float spawnTimer;

    public override bool Running => running;

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstRoundDelay;
    }

    public override void Deactivate() => running = false;

    /// <summary>Awake: start running if the Inspector says so and set the clock to the first round.</summary>
    protected void StartSchedule()
    {
        if (startRunning) running = true;
        spawnTimer = firstRoundDelay;
    }

    /// <summary>The next round comes minInterval..maxInterval seconds from now.</summary>
    protected void ScheduleNext() => spawnTimer = RandomBetween(minInterval, maxInterval);
}
