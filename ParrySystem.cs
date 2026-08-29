using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────
//  ENUMS & DATA TYPES
// ─────────────────────────────────────────────

public enum ParryState
{
    Idle,
    Anticipating,   // optional wind-up before the parry window opens
    Active,         // the actual parry window — inputs are checked here
    Success,        // a parry was landed; play counter animation, grant invincibility, etc.
    Cooldown,       // prevents spam after a parry attempt (hit or miss)
}

public enum ParryResult
{
    None,
    Success,
    PerfectParry,   // optional: tighter timing window for bonus reward
    Failure,        // window missed or wrong button
}

/// <summary>Data describing an incoming attack that can be parried.</summary>
public struct AttackData
{
    public float Damage;
    public float KnockbackForce;
    public bool  IsParryable;
    public bool  IsUnblockable;     // e.g. grabs — cannot be parried at all
    public string AttackType;       // "melee", "projectile", etc. — lets you filter
}

/// <summary>Fired whenever a parry attempt resolves.</summary>
public class ParryEventArgs : EventArgs
{
    public ParryResult Result     { get; init; }
    public AttackData  Attack     { get; init; }
    public float       WindowUsed { get; init; } // seconds into the active window
}

// ─────────────────────────────────────────────
//  PARRY SETTINGS  (tweak in inspector / config)
// ─────────────────────────────────────────────

[Serializable]
public class ParrySettings
{
    /// <summary>Seconds between pressing parry and the window opening.</summary>
    public float AnticipationDuration = 0.05f;

    /// <summary>Duration of the active parry window in seconds.</summary>
    public float ActiveWindowDuration = 0.18f;

    /// <summary>
    /// Portion of the active window (from the start) considered a "perfect" parry.
    /// Set to 0 to disable perfect parries.
    /// </summary>
    public float PerfectParryFraction = 0.35f;

    /// <summary>Cooldown applied after any attempt (success or failure).</summary>
    public float CooldownDuration = 0.5f;

    /// <summary>
    /// If true, the player must hold the parry button; releasing early cancels.
    /// If false, a single tap is enough to enter the window.
    /// </summary>
    public bool RequireHold = false;

    /// <summary>Which attack types this component can parry. Empty = all types.</summary>
    public List<string> AllowedAttackTypes = new();
}

// ─────────────────────────────────────────────
//  PARRY SYSTEM CORE
// ─────────────────────────────────────────────

/// <summary>
/// Drop this component on any entity that needs a parry system.
/// Wire up the events and call the public methods from your input / hit-detection code.
/// </summary>
public class ParrySystem
{
    // ── public state ──────────────────────────
    public ParryState   State          { get; private set; } = ParryState.Idle;
    public ParryResult  LastResult     { get; private set; } = ParryResult.None;

    // ── events ────────────────────────────────
    public event EventHandler<ParryEventArgs>? OnParryResolved;   // success OR failure
    public event EventHandler?                 OnParrySuccess;
    public event EventHandler?                 OnPerfectParry;
    public event EventHandler?                 OnParryFailure;
    public event EventHandler?                 OnWindowOpened;
    public event EventHandler?                 OnWindowClosed;

    // ── internal ──────────────────────────────
    private readonly ParrySettings _settings;
    private float _stateTimer;             // seconds spent in the current state
    private bool  _buttonHeld;
    private bool  _paused;

    // ─────────────────────────────────────────
    public ParrySystem(ParrySettings? settings = null)
    {
        _settings = settings ?? new ParrySettings();
    }

    // ─────────────────────────────────────────
    //  PUBLIC API — call from game loop / input
    // ─────────────────────────────────────────

    /// <summary>Call every frame / fixed update with the elapsed time in seconds.</summary>
    public void Tick(float deltaTime)
    {
        if (_paused) return;

        _stateTimer += deltaTime;

        switch (State)
        {
            case ParryState.Anticipating:
                if (_settings.RequireHold && !_buttonHeld)
                    TransitionTo(ParryState.Cooldown);         // released too early
                else if (_stateTimer >= _settings.AnticipationDuration)
                    OpenParryWindow();
                break;

            case ParryState.Active:
                if (_settings.RequireHold && !_buttonHeld)
                    TransitionTo(ParryState.Cooldown);         // released during window
                else if (_stateTimer >= _settings.ActiveWindowDuration)
                    CloseParryWindow(missed: true);
                break;

            case ParryState.Success:
            case ParryState.Cooldown:
                if (_stateTimer >= _settings.CooldownDuration)
                    TransitionTo(ParryState.Idle);
                break;
        }
    }

    /// <summary>Call when the player presses the parry input.</summary>
    public void OnParryInputPressed()
    {
        _buttonHeld = true;

        if (State == ParryState.Idle)
        {
            float anticipation = _settings.AnticipationDuration;
            TransitionTo(anticipation > 0f ? ParryState.Anticipating : ParryState.Active);

            if (State == ParryState.Active)
                OpenParryWindow();
        }
    }

    /// <summary>Call when the player releases the parry input.</summary>
    public void OnParryInputReleased()
    {
        _buttonHeld = false;
        // RequireHold cancellation is handled inside Tick()
    }

    /// <summary>
    /// Call this when an attack hitbox overlaps the character.
    /// Returns the parry result so the caller can suppress damage on success.
    /// </summary>
    public ParryResult TryParry(AttackData attack)
    {
        if (State != ParryState.Active)
            return ParryResult.None;

        if (!attack.IsParryable || attack.IsUnblockable)
            return ParryResult.None;

        if (!IsAttackTypeAllowed(attack.AttackType))
            return ParryResult.None;

        // Determine success tier
        float fraction = _stateTimer / _settings.ActiveWindowDuration;
        bool  perfect  = _settings.PerfectParryFraction > 0f
                         && fraction <= _settings.PerfectParryFraction;

        ParryResult result = perfect ? ParryResult.PerfectParry : ParryResult.Success;
        ResolveParry(result, attack);
        return result;
    }

    /// <summary>Pause / resume internal ticking (e.g. during cutscenes).</summary>
    public void SetPaused(bool paused) => _paused = paused;

    /// <summary>Force the system back to Idle (e.g. on stagger / death).</summary>
    public void ForceReset() => TransitionTo(ParryState.Idle);

    // ─────────────────────────────────────────
    //  INTERNAL HELPERS
    // ─────────────────────────────────────────

    private void OpenParryWindow()
    {
        TransitionTo(ParryState.Active);
        OnWindowOpened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseParryWindow(bool missed)
    {
        if (missed)
        {
            LastResult = ParryResult.Failure;
            OnParryFailure?.Invoke(this, EventArgs.Empty);
            OnParryResolved?.Invoke(this, new ParryEventArgs
            {
                Result     = ParryResult.Failure,
                WindowUsed = _stateTimer,
            });
        }
        OnWindowClosed?.Invoke(this, EventArgs.Empty);
        TransitionTo(ParryState.Cooldown);
    }

    private void ResolveParry(ParryResult result, AttackData attack)
    {
        LastResult = result;

        var args = new ParryEventArgs
        {
            Result     = result,
            Attack     = attack,
            WindowUsed = _stateTimer,
        };

        OnParryResolved?.Invoke(this, args);
        OnParrySuccess?.Invoke(this, EventArgs.Empty);

        if (result == ParryResult.PerfectParry)
            OnPerfectParry?.Invoke(this, EventArgs.Empty);

        OnWindowClosed?.Invoke(this, EventArgs.Empty);
        TransitionTo(ParryState.Success);   // play success animation, then cooldown
    }

    private void TransitionTo(ParryState next)
    {
        State       = next;
        _stateTimer = 0f;
    }

    private bool IsAttackTypeAllowed(string attackType)
    {
        if (_settings.AllowedAttackTypes is not { Count: > 0 } allowed)
            return true;                            // no filter — allow everything

        return allowed.Contains(attackType);
    }
}

// ─────────────────────────────────────────────
//  USAGE EXAMPLE  (Unity-style pseudo-code)
// ─────────────────────────────────────────────
/*

public class PlayerController : MonoBehaviour
{
    private ParrySystem _parry;

    void Start()
    {
        var settings = new ParrySettings
        {
            ActiveWindowDuration = 0.2f,
            PerfectParryFraction = 0.30f,
            CooldownDuration     = 0.6f,
        };

        _parry = new ParrySystem(settings);

        // Wire events
        _parry.OnParrySuccess  += (_, _) => PlayAnimation("parry_success");
        _parry.OnPerfectParry  += (_, _) => { GrantSlowMo(0.5f); SpawnRing(); };
        _parry.OnParryFailure  += (_, _) => PlayAnimation("parry_fail");
        _parry.OnWindowOpened  += (_, _) => ShowParryVFX(true);
        _parry.OnWindowClosed  += (_, _) => ShowParryVFX(false);

        _parry.OnParryResolved += (_, e) =>
        {
            Debug.Log($"Parry result: {e.Result} at {e.WindowUsed:F3}s into the window");
        };
    }

    void Update()
    {
        _parry.Tick(Time.deltaTime);

        if (Input.GetButtonDown("Parry")) _parry.OnParryInputPressed();
        if (Input.GetButtonUp("Parry"))   _parry.OnParryInputReleased();
    }

    // Called by hit detection (e.g. OnTriggerEnter)
    public void OnHit(AttackData attack)
    {
        var result = _parry.TryParry(attack);

        if (result == ParryResult.None)
            TakeDamage(attack.Damage);          // parry missed or not attempted
        // On success / perfect, damage is suppressed
    }
}

*/
