using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Combat;

/// <summary>
/// Hitstop, shake, stride, vital pulses, swing buffer, and the sounds a blow makes.
///
/// Game1 still owns what a click does when it is talking to someone rather than swinging.
/// This type must not take a <c>Game1</c> reference.
/// </summary>
internal sealed class CombatFeel
{
    public const float PulseSeconds = 0.7f;
    public const float StrideMetres = 1.9f;
    public const double StoneDropChance = 0.5;

    public float Hitstop;
    public float Shake;
    public float ShakeStrength;
    public float HealthPulse;
    public float PranaPulse;
    public float LastHealth;
    public float LastPrana;
    public float SwingBuffered;
    public float Stride;

    public void TickRealTime(float realSeconds)
    {
        if (Hitstop > 0f) Hitstop = MathF.Max(0f, Hitstop - realSeconds);
        if (Shake > 0f) Shake = MathF.Max(0f, Shake - realSeconds);
    }

    public void Impact(float weight)
    {
        var w = MathHelper.Clamp(weight, 0f, 1f);
        Hitstop = MathF.Max(Hitstop, 0.030f + 0.055f * w);
        Shake = MathF.Max(Shake, 0.10f + 0.14f * w);
        ShakeStrength = MathF.Max(ShakeStrength, 0.0022f + 0.0075f * w);
    }

    public (float Yaw, float Pitch) ShakeOffset(float clock)
    {
        if (Shake <= 0f) return (0f, 0f);

        var falloff = Shake * Shake;
        var strength = ShakeStrength * falloff * 60f;
        return (MathF.Sin(clock * 71f) * strength,
                MathF.Sin(clock * 53f) * strength * 0.75f);
    }

    public void TickVitalPulses(GameSession? session, float deltaSeconds)
    {
        if (session is null) return;

        var vitals = session.Player.Vitals;
        if (vitals.Health > LastHealth + 1f) HealthPulse = 1f;
        if (vitals.Prana > LastPrana + 1f) PranaPulse = 1f;
        LastHealth = vitals.Health;
        LastPrana = vitals.Prana;

        var fade = deltaSeconds / PulseSeconds;
        HealthPulse = MathF.Max(0f, HealthPulse - fade);
        PranaPulse = MathF.Max(0f, PranaPulse - fade);
    }

    public void ReportAttack(AttackOutcome outcome, GameSession? session, SoundBank? sfx)
    {
        if (outcome.Result == AttackResult.Exhausted)
        {
            session?.ShowToast("Too exhausted.");
            sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        if (outcome.Result == AttackResult.NoAmmunition)
        {
            session?.ShowToast("Out of arrows.");
            sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        if (outcome.Swung)
        {
            sfx?.Play(Sfx.Swing, SwingWeight(session), volumeScale: 0.75f);
            session?.Player.Spells.Encumber(session.Player.Equipment.Weapon.CastDelaySeconds);
        }

        if (outcome.Result != AttackResult.Hit) return;

        var weight = MathHelper.Clamp(outcome.Damage / 45f, 0.25f, 1f);
        if (outcome.WasOpening) weight = MathF.Min(1f, weight * 1.4f);
        sfx?.Play(Sfx.HitFlesh, weight);
        Impact(weight);
    }

    public void ReportCast(CastOutcome outcome, GameSession session, SoundBank? sfx)
    {
        if (outcome.Result == CastResult.Landed || outcome.Result == CastResult.Missed)
            sfx?.Play(Sfx.Cast, 0.5f, volumeScale: 0.85f);

        switch (outcome.Result)
        {
            case CastResult.NoCharge:
                session.ShowToast("No prana, and no jiva stone to draw on.");
                sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
                break;
            case CastResult.Shouldering:
                session.ShowToast($"Both hands are on the {session.Player.Equipment.Weapon.DisplayName}.");
                sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
                break;
            case CastResult.Landed when outcome.Spell?.Effect == SpellEffect.Heal:
                session.ShowToast($"{outcome.Spell.DisplayName} — restored.");
                break;
            case CastResult.Landed when outcome.Spell?.Effect == SpellEffect.Light:
                session.ShowToast($"{outcome.Spell.DisplayName} — the dark pulls back.");
                break;
        }
    }

    public void OfferStone(GameSession? session, RunRuntime? run, Random drops, int mineDepth,
        SoundBank? sfx, Coach coach)
    {
        if (session is null || run is null) return;
        if (drops.NextDouble() > StoneDropChance) return;

        var available = StoneCatalog.AvailableAt(mineDepth);
        if (available.Count == 0) return;

        var stone = available[drops.Next(available.Count)];
        session.Player.Stones.Found(stone.Id);
        session.ShowToast($"{stone.DisplayName} found.  {stone.Description}");
        sfx?.Play(Sfx.Chime, 0.4f);
        coach.Teach(Lessons.Stones, Lessons.TextOf(Lessons.Stones));
    }

    public void WatchSession(GameSession session, SoundBank? sfx) =>
        session.Player.Vitals.LevelGained += _ => sfx?.Play(Sfx.Chime, 0.5f);

    public void WatchEncounter(Encounter encounter, SoundBank? sfx)
    {
        encounter.EnemyDefeated += enemy =>
        {
            sfx?.Play(Sfx.Death, Weight(enemy));
            Impact(0.85f);
        };

        encounter.SpellLanded += (_, _, _) =>
        {
            sfx?.Play(Sfx.HitFlesh, 0.45f, volumeScale: 0.8f);
            Impact(0.35f);
        };

        encounter.PlayerStruck += (damage, guarded) =>
        {
            var weight = MathHelper.Clamp(damage / 30f, 0.2f, 1f);
            sfx?.Play(guarded ? Sfx.Block : Sfx.Hurt, weight);
            Impact(guarded ? weight * 0.5f : MathF.Min(1f, weight * 1.15f));
        };

        static float Weight(Enemy enemy) =>
            MathHelper.Clamp(enemy.Archetype.MaxHealth / 260f, 0.35f, 1f);
    }

    public void Step(float metres, KeyboardState keyboard, bool inWorld, bool grounded,
        bool crouching, SoundBank? sfx)
    {
        if (!inWorld || !grounded) return;

        Stride += metres;
        var sprinting = keyboard.IsKeyDown(Keys.LeftShift);
        var length = StrideMetres * (crouching ? 0.72f : sprinting ? 1.25f : 1f);
        if (Stride < length) return;

        Stride = 0f;
        sfx?.Play(Sfx.Step, sprinting ? 0.55f : 0.3f, volumeScale: crouching ? 0.28f : 0.5f);
    }

    private static float SwingWeight(GameSession? session) => session?.Player.Equipment.Weapon.Class switch
    {
        WeaponClass.TwoHanded => 0.9f,
        WeaponClass.Ranged => 0.15f,
        _ => 0.35f
    };
}

internal enum CombatAction { None, OpenShop, Talk, SelectSpell }

internal readonly record struct CombatCommand(CombatAction Action, SpeakingActor? Actor = null,
    string? SpellId = null)
{
    public static CombatCommand Idle => new(CombatAction.None);
}

/// <summary>
/// Enemies act, then the player does. Blocking is held, attacking drops the guard.
///
/// Returns a command when a click is talking rather than swinging. Game1 still opens the
/// stall and the conversation. This type must not take a <c>Game1</c> reference.
/// </summary>
internal sealed class CombatDirector
{
    private readonly CombatFeel _feel;

    public CombatDirector(CombatFeel feel) => _feel = feel;

    public CombatFeel Feel => _feel;

    public CombatCommand Tick(
        float step,
        KeyboardState keyboard,
        MouseState mouse,
        bool clicked,
        bool mouseLook,
        bool helpOpen,
        bool moving,
        GameSession session,
        Encounter encounter,
        WorldRuntime? world,
        RunRuntime? run,
        DialogueRuntime? dialogue,
        Shop? shop,
        FirstPersonView camera,
        WeaponView weapon,
        Coach coach,
        PlayRecorder recorder,
        SoundBank? sfx,
        InputRouter input)
    {
        coach.Tick(step);
        _feel.TickVitalPulses(session, step);
        encounter.Update(step, camera.Position, camera.Yaw);
        if (world is not null) run?.Update(world, camera.Position, encounter);
        weapon.Update(step, moving, session.Player.Combat.IsBlocking);

        if (!mouseLook || helpOpen) return CombatCommand.Idle;

        session.Player.Combat.SetBlocking(mouse.RightButton == ButtonState.Pressed);

        if (_feel.SwingBuffered > 0f) _feel.SwingBuffered -= step;
        if (clicked && !session.Player.Combat.IsReady && encounter.Focused is not null)
            _feel.SwingBuffered = session.Player.Combat.ActiveWeapon.Cooldown;

        var releaseBuffered = _feel.SwingBuffered > 0f && session.Player.Combat.IsReady;
        if (releaseBuffered) _feel.SwingBuffered = 0f;

        if (clicked || releaseBuffered)
        {
            var actor = releaseBuffered
                ? null
                : dialogue?.FindActor(
                    new WorldPoint(camera.Position.X, camera.Position.Y, camera.Position.Z),
                    camera.Yaw);

            if (actor is not null)
            {
                if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                    && shop is not null)
                    return new CombatCommand(CombatAction.OpenShop, actor);
                return new CombatCommand(CombatAction.Talk, actor);
            }

            var outcome = encounter.PlayerAttack();
            var struck = encounter.Focused;
            if (outcome.Result is AttackResult.OnCooldown or AttackResult.Exhausted)
            {
                if (_feel.SwingBuffered <= 0f)
                    recorder.Record(PlayEventKind.MeleeBalked,
                        outcome.Result == AttackResult.OnCooldown ? "too soon" : "no stamina",
                        0f, 0f, session.Player.Vitals.Health, session.Player.Vitals.Prana);
            }
            else
            {
                recorder.Record(PlayEventKind.MeleeSwing,
                    session.Player.Combat.ActiveWeapon.DisplayName,
                    outcome.Damage, outcome.Result == AttackResult.Hit ? 1f : 0f,
                    session.Player.Vitals.Health, session.Player.Vitals.Prana,
                    struck?.Archetype.DisplayName ?? string.Empty,
                    struck is null ? 0f : encounter.PlayerPosition.FlatDistanceTo(struck.Position));
            }

            if (outcome.Swung)
                weapon.Swing(session.Player.Combat.ActiveWeapon, session.Player.Combat.WeaponSweeps);
            _feel.ReportAttack(outcome, session, sfx);
        }

        if (input.Pressed(keyboard, Keys.Q))
        {
            var cast = encounter.PlayerCast(camera.Position, camera.Yaw, camera.Forward);
            if (cast.WasCast)
            {
                weapon.Cast();
                var aimed = encounter.Focused;
                recorder.Record(PlayEventKind.SpellCast, cast.Spell?.DisplayName ?? "spell",
                    cast.Spell?.Power ?? 0f, 0f,
                    session.Player.Vitals.Health, session.Player.Vitals.Prana,
                    aimed?.Archetype.DisplayName ?? string.Empty,
                    encounter.NearestEnemyRange());
            }
            else
            {
                recorder.Record(PlayEventKind.CastFailed, cast.Spell?.DisplayName ?? "spell",
                    cast.Spell?.BaseCost ?? 0f, 0f,
                    session.Player.Vitals.Health, session.Player.Vitals.Prana);
            }
            _feel.ReportCast(cast, session, sfx);
        }

        if (input.Pressed(keyboard, Keys.D4)) return new CombatCommand(CombatAction.SelectSpell, SpellId: SpellCatalog.FireId);
        if (input.Pressed(keyboard, Keys.D5)) return new CombatCommand(CombatAction.SelectSpell, SpellId: SpellCatalog.FrostId);
        if (input.Pressed(keyboard, Keys.D6)) return new CombatCommand(CombatAction.SelectSpell, SpellId: SpellCatalog.ShockId);
        if (input.Pressed(keyboard, Keys.D7)) return new CombatCommand(CombatAction.SelectSpell, SpellId: SpellCatalog.HealId);
        if (input.Pressed(keyboard, Keys.D8)) return new CombatCommand(CombatAction.SelectSpell, SpellId: SpellCatalog.LightId);
        return CombatCommand.Idle;
    }
}
